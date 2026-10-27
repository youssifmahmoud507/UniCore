using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Offerings.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Offerings;

namespace UniCore.Application.Modules.Offerings
{
    // Sections of an offering. Reading needs Courses.Read; every change needs Courses.Manage inside the offered
    // course's department. Nothing changes in a closed semester or in a cancelled offering.
    public sealed class CourseSectionHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user, OfferingAccess access,
        IAggregateRepository<CourseSection> sections, IAggregateRepository<CourseOffering> offerings, IAggregateRepository<InstructorAssignment> assignments,
        ISectionEnrollmentCounter enrollments, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly OfferingAccess _access = access;
        private readonly IAggregateRepository<CourseSection> _sections = sections;
        private readonly IAggregateRepository<CourseOffering> _offerings = offerings;
        private readonly IAggregateRepository<InstructorAssignment> _assignments = assignments;
        private readonly ISectionEnrollmentCounter _enrollments = enrollments;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private static string Describe(CourseSection s) => $"Offering={s.CourseOfferingId};Code={s.SectionCode};Capacity={s.Capacity};Status={s.Status}";

        private async Task<Result<SectionSummary>> CommitAsync(CourseSection section, string action, string? before, RequestInfo request, CancellationToken cancellationToken, string? extra = null)
        {
            var entry = new AuditEntry(action, _user.UserId, "CourseSection", section.Id.ToString(), request.IpAddress, request.UserAgent, before, Describe(section) + extra);
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<SectionSummary>.Fail(committed.Errors) : OfferingMapper.ToSummary(section);
        }

        // Loads a section for a change: permission, scope, and the checks every change shares.
        private async Task<Result<OfferingAccess.SectionContext>> LoadForChangeAsync(Guid sectionId, CancellationToken cancellationToken)
        {
            var loaded = await _access.LoadSectionAsync(sectionId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return loaded;
            }

            var semester = await _access.OpenSemesterAsync(context.Offering.AcademicSemesterId, cancellationToken);
            if (semester.IsFailure)
            {
                return Result<OfferingAccess.SectionContext>.Fail(semester.Errors);
            }

            return context;
        }

        // ---------- queries ----------

        public async Task<Result<IReadOnlyList<SectionSummary>>> ListAsync(Guid offeringId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Courses.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<IReadOnlyList<SectionSummary>>.Fail(auth.Errors);
            }

            if (await _offerings.GetByIdAsync(offeringId, cancellationToken) is null)
            {
                return OfferingsErrors.OfferingNotFound;
            }

            var items = await _sections.ListAsync(new SectionsOfOfferingSpecification(offeringId, activeOnly: false), cancellationToken);

            return Result<IReadOnlyList<SectionSummary>>.Ok([.. items.Select(s => OfferingMapper.ToSummary(s))]);
        }

        public async Task<Result<SectionSummary>> GetAsync(Guid sectionId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Courses.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<SectionSummary>.Fail(auth.Errors);
            }

            var section = await _sections.GetByIdAsync(sectionId, cancellationToken);

            return section is null ? OfferingsErrors.SectionNotFound : OfferingMapper.ToSummary(section);
        }

        // ---------- changes ----------

        public async Task<Result<SectionSummary>> CreateAsync(Guid offeringId, CreateSectionCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await _access.LoadOfferingAsync(offeringId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<SectionSummary>.Fail(loaded.Errors);
            }

            var semester = await _access.OpenSemesterAsync(context.Offering.AcademicSemesterId, cancellationToken);
            if (semester.IsFailure)
            {
                return Result<SectionSummary>.Fail(semester.Errors);
            }

            if (context.Offering.Status == CourseOfferingStatus.Cancelled)
            {
                return OfferingsErrors.OfferingInvalidState;
            }

            var created = CourseSection.Create(offeringId, command.SectionCode, command.Capacity, _clock.UtcNow);
            if (!created.TryGetValue(out var section))
            {
                return Result<SectionSummary>.Fail(created.Errors);
            }

            if (await _sections.AnyAsync(new SectionByCodeSpecification(offeringId, section.SectionCode), cancellationToken))
            {
                return OfferingsErrors.SectionCodeExists;
            }

            if (context.Offering.MaxSections is { } max
                && await _sections.CountAsync(new SectionsOfOfferingSpecification(offeringId, activeOnly: true), cancellationToken) >= max)
            {
                return OfferingsErrors.MaxSectionsReached;
            }

            _sections.Add(section);

            return await CommitAsync(section, OfferingsAuditActions.SectionCreated, null, request, cancellationToken);
        }

        public async Task<Result<SectionSummary>> UpdateAsync(Guid sectionId, UpdateSectionCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForChangeAsync(sectionId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<SectionSummary>.Fail(loaded.Errors);
            }

            var enrolled = await _enrollments.CountEnrolledAsync(sectionId, cancellationToken);
            var before = Describe(context.Section);

            var changed = context.Section.ChangeCapacity(command.Capacity, enrolled, _clock.UtcNow);
            if (changed.IsFailure)
            {
                return Result<SectionSummary>.Fail(changed.Errors);
            }

            return await CommitAsync(context.Section, OfferingsAuditActions.SectionUpdated, before, request, cancellationToken);
        }

        public async Task<Result<SectionSummary>> CloseAsync(Guid sectionId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForChangeAsync(sectionId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<SectionSummary>.Fail(loaded.Errors);
            }

            var before = Describe(context.Section);

            var closed = context.Section.Close(_clock.UtcNow);
            if (closed.IsFailure)
            {
                return Result<SectionSummary>.Fail(closed.Errors);
            }

            return await CommitAsync(context.Section, OfferingsAuditActions.SectionClosed, before, request, cancellationToken);
        }

        public async Task<Result<SectionSummary>> ReopenAsync(Guid sectionId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForChangeAsync(sectionId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<SectionSummary>.Fail(loaded.Errors);
            }

            if (context.Offering.Status == CourseOfferingStatus.Cancelled)
            {
                return OfferingsErrors.OfferingInvalidState;
            }

            var before = Describe(context.Section);

            var reopened = context.Section.Reopen(_clock.UtcNow);
            if (reopened.IsFailure)
            {
                return Result<SectionSummary>.Fail(reopened.Errors);
            }

            return await CommitAsync(context.Section, OfferingsAuditActions.SectionReopened, before, request, cancellationToken);
        }

        // Final. Refused while students are enrolled. Ends the section's active instructor assignments in the same save.
        public async Task<Result<SectionSummary>> CancelAsync(Guid sectionId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForChangeAsync(sectionId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<SectionSummary>.Fail(loaded.Errors);
            }

            var now = _clock.UtcNow;
            var enrolled = await _enrollments.CountEnrolledAsync(sectionId, cancellationToken);
            var before = Describe(context.Section);

            var cancelled = context.Section.Cancel(enrolled, now);
            if (cancelled.IsFailure)
            {
                return Result<SectionSummary>.Fail(cancelled.Errors);
            }

            var active = await _assignments.ListAsync(new AssignmentsOfSectionSpecification(sectionId, activeOnly: true, tracking: true), cancellationToken);
            foreach (var assignment in active)
            {
                var ended = assignment.End(now);
                if (ended.IsFailure)
                {
                    return Result<SectionSummary>.Fail(ended.Errors);
                }
            }

            return await CommitAsync(context.Section, OfferingsAuditActions.SectionCancelled, before, request, cancellationToken, $";AssignmentsEnded={active.Count}");
        }
    }
}
