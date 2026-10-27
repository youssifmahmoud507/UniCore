using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Modules.Courses;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Offerings.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Courses;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Offerings;

namespace UniCore.Application.Modules.Offerings
{
    // Course offerings: a course version offered in a semester. Reading needs Courses.Read; every change needs
    // Courses.Manage inside the offered course's department. Nothing changes in a closed semester.
    public sealed class CourseOfferingHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user, OfferingAccess access,
        IAggregateRepository<CourseOffering> offerings, IAggregateRepository<CourseSection> sections, IAggregateRepository<InstructorAssignment> assignments,
        ISectionEnrollmentCounter enrollments, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly OfferingAccess _access = access;
        private readonly IAggregateRepository<CourseOffering> _offerings = offerings;
        private readonly IAggregateRepository<CourseSection> _sections = sections;
        private readonly IAggregateRepository<InstructorAssignment> _assignments = assignments;
        private readonly ISectionEnrollmentCounter _enrollments = enrollments;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private static string Describe(CourseOffering o) => $"CourseVersion={o.CourseVersionId};Semester={o.AcademicSemesterId};Status={o.Status};MaxSections={o.MaxSections}";

        private async Task<Result<OfferingSummary>> CommitAsync(CourseOffering offering, string action, string? before, RequestInfo request, CancellationToken cancellationToken, string? extra = null)
        {
            var entry = new AuditEntry(action, _user.UserId, "CourseOffering", offering.Id.ToString(), request.IpAddress, request.UserAgent, before, Describe(offering) + extra);
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<OfferingSummary>.Fail(committed.Errors) : OfferingMapper.ToSummary(offering);
        }

        // ---------- queries ----------

        public async Task<Result<PagedResult<OfferingSummary>>> ListAsync(PaginationQuery query, string? sort, Guid? semesterId, Guid? courseVersionId, CourseOfferingStatus? status, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Courses.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<OfferingSummary>>.Fail(auth.Errors);
            }

            var parsed = ListQueryRules.ParseSort(sort, OfferingsListSpecification.SortFields, "createdAt");
            if (!parsed.TryGetValue(out var sortOption))
            {
                return Result<PagedResult<OfferingSummary>>.Fail(parsed.Errors);
            }

            var specification = new OfferingsListSpecification(query, semesterId, courseVersionId, status, sortOption);

            var items = await _offerings.ListAsync(specification, cancellationToken);
            var total = await _offerings.CountAsync(specification, cancellationToken);

            return Result<PagedResult<OfferingSummary>>.Ok(PagedResult<OfferingSummary>.Create([.. items.Select(o => OfferingMapper.ToSummary(o))], query, total));
        }

        public async Task<Result<OfferingSummary>> GetAsync(Guid offeringId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Courses.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<OfferingSummary>.Fail(auth.Errors);
            }

            var offering = await _offerings.GetByIdAsync(offeringId, cancellationToken);

            return offering is null ? OfferingsErrors.OfferingNotFound : OfferingMapper.ToSummary(offering);
        }

        // ---------- changes ----------

        public async Task<Result<OfferingSummary>> CreateAsync(CreateOfferingCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var authorized = await _access.AuthorizeForVersionAsync(command.CourseVersionId, cancellationToken);
            if (!authorized.TryGetValue(out var version))
            {
                return Result<OfferingSummary>.Fail(authorized.Errors);
            }

            var semesterResult = await _access.OpenSemesterAsync(command.AcademicSemesterId, cancellationToken);
            if (!semesterResult.TryGetValue(out var semester))
            {
                return Result<OfferingSummary>.Fail(semesterResult.Errors);
            }

            // The version valid at the semester start (a draft is never valid).
            var effective = version.Status != CourseVersionStatus.Draft
                && version.EffectiveFrom <= semester.StartDate
                && (version.EffectiveTo is null || version.EffectiveTo >= semester.StartDate);
            if (!effective)
            {
                return OfferingsErrors.CourseVersionNotEffective;
            }

            var created = CourseOffering.Create(version.CourseVersionId, semester.Id, command.MaxSections, _clock.UtcNow);
            if (!created.TryGetValue(out var offering))
            {
                return Result<OfferingSummary>.Fail(created.Errors);
            }

            if (await _offerings.AnyAsync(new OfferingByKeySpecification(offering.CourseVersionId, offering.AcademicSemesterId), cancellationToken))
            {
                return OfferingsErrors.OfferingExists;
            }

            _offerings.Add(offering);

            return await CommitAsync(offering, OfferingsAuditActions.OfferingCreated, null, request, cancellationToken);
        }

        public async Task<Result<OfferingSummary>> UpdateAsync(Guid offeringId, UpdateOfferingCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await _access.LoadOfferingAsync(offeringId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<OfferingSummary>.Fail(loaded.Errors);
            }

            var semester = await _access.OpenSemesterAsync(context.Offering.AcademicSemesterId, cancellationToken);
            if (semester.IsFailure)
            {
                return Result<OfferingSummary>.Fail(semester.Errors);
            }

            var active = await _sections.CountAsync(new SectionsOfOfferingSpecification(offeringId, activeOnly: true), cancellationToken);

            var before = Describe(context.Offering);

            var changed = context.Offering.ChangeMaxSections(command.MaxSections, active, _clock.UtcNow);
            if (changed.IsFailure)
            {
                return Result<OfferingSummary>.Fail(changed.Errors);
            }

            return await CommitAsync(context.Offering, OfferingsAuditActions.OfferingUpdated, before, request, cancellationToken);
        }

        public async Task<Result<OfferingSummary>> OpenAsync(Guid offeringId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await _access.LoadOfferingAsync(offeringId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<OfferingSummary>.Fail(loaded.Errors);
            }

            var semester = await _access.OpenSemesterAsync(context.Offering.AcademicSemesterId, cancellationToken);
            if (semester.IsFailure)
            {
                return Result<OfferingSummary>.Fail(semester.Errors);
            }

            var active = await _sections.CountAsync(new SectionsOfOfferingSpecification(offeringId, activeOnly: true), cancellationToken);

            var before = Describe(context.Offering);

            var opened = context.Offering.Open(active, _clock.UtcNow);
            if (opened.IsFailure)
            {
                return Result<OfferingSummary>.Fail(opened.Errors);
            }

            return await CommitAsync(context.Offering, OfferingsAuditActions.OfferingOpened, before, request, cancellationToken);
        }

        public async Task<Result<OfferingSummary>> CloseAsync(Guid offeringId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await _access.LoadOfferingAsync(offeringId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<OfferingSummary>.Fail(loaded.Errors);
            }

            var semester = await _access.OpenSemesterAsync(context.Offering.AcademicSemesterId, cancellationToken);
            if (semester.IsFailure)
            {
                return Result<OfferingSummary>.Fail(semester.Errors);
            }

            var before = Describe(context.Offering);

            var closed = context.Offering.Close(_clock.UtcNow);
            if (closed.IsFailure)
            {
                return Result<OfferingSummary>.Fail(closed.Errors);
            }

            return await CommitAsync(context.Offering, OfferingsAuditActions.OfferingClosed, before, request, cancellationToken);
        }

        // Final. Cancels every section that is not cancelled yet and ends their active instructor assignments,
        // all in one save. Refused while any section still has enrolled students.
        public async Task<Result<OfferingSummary>> CancelAsync(Guid offeringId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await _access.LoadOfferingAsync(offeringId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<OfferingSummary>.Fail(loaded.Errors);
            }

            var semester = await _access.OpenSemesterAsync(context.Offering.AcademicSemesterId, cancellationToken);
            if (semester.IsFailure)
            {
                return Result<OfferingSummary>.Fail(semester.Errors);
            }

            if (context.Offering.Status == CourseOfferingStatus.Cancelled)
            {
                return OfferingsErrors.OfferingInvalidState;
            }

            var now = _clock.UtcNow;
            var open = await _sections.ListAsync(new SectionsOfOfferingSpecification(offeringId, activeOnly: true, tracking: true), cancellationToken);

            // Check every section before changing any, so a refusal leaves nothing half cancelled.
            foreach (var section in open)
            {
                if (await _enrollments.CountEnrolledAsync(section.Id, cancellationToken) > 0)
                {
                    return OfferingsErrors.SectionHasEnrollments;
                }
            }

            foreach (var section in open)
            {
                var cancelled = section.Cancel(0, now);
                if (cancelled.IsFailure)
                {
                    return Result<OfferingSummary>.Fail(cancelled.Errors);
                }

                var active = await _assignments.ListAsync(new AssignmentsOfSectionSpecification(section.Id, activeOnly: true, tracking: true), cancellationToken);
                foreach (var assignment in active)
                {
                    var ended = assignment.End(now);
                    if (ended.IsFailure)
                    {
                        return Result<OfferingSummary>.Fail(ended.Errors);
                    }
                }
            }

            var before = Describe(context.Offering);

            var done = context.Offering.Cancel(now);
            if (done.IsFailure)
            {
                return Result<OfferingSummary>.Fail(done.Errors);
            }

            return await CommitAsync(context.Offering, OfferingsAuditActions.OfferingCancelled, before, request, cancellationToken, $";SectionsCancelled={open.Count}");
        }
    }
}
