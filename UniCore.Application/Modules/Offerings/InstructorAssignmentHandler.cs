using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Offerings.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Offerings;
using UniCore.Domain.Modules.People;

namespace UniCore.Application.Modules.Offerings
{
    // Instructors of a section (primary, co-instructor, teaching assistant). Reading needs Courses.Read; changes need
    // Courses.Manage inside the offered course's department. An instructor has at most one active assignment per
    // section, and a section has at most one active primary. Ending an assignment keeps its row (end-dating).
    public sealed class InstructorAssignmentHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user, OfferingAccess access,
        IAggregateRepository<InstructorAssignment> assignments, IAggregateRepository<CourseSection> sections, IRepository<Instructor> instructors,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly OfferingAccess _access = access;
        private readonly IAggregateRepository<InstructorAssignment> _assignments = assignments;
        private readonly IAggregateRepository<CourseSection> _sections = sections;
        private readonly IRepository<Instructor> _instructors = instructors;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private static string Describe(InstructorAssignment a) => $"Section={a.CourseSectionId};Instructor={a.InstructorId};Role={a.Role};AssignedAt={a.AssignedAt:O};EndedAt={a.EndedAt:O}";

        private async Task<Result<AssignmentSummary>> CommitAsync(InstructorAssignment assignment, string action, string? before, RequestInfo request, CancellationToken cancellationToken)
        {
            var entry = new AuditEntry(action, _user.UserId, "InstructorAssignment", assignment.Id.ToString(), request.IpAddress, request.UserAgent, before, Describe(assignment));
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<AssignmentSummary>.Fail(committed.Errors) : OfferingMapper.ToSummary(assignment);
        }

        // ---------- queries ----------

        public async Task<Result<IReadOnlyList<AssignmentSummary>>> ListAsync(Guid sectionId, bool activeOnly, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Courses.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<IReadOnlyList<AssignmentSummary>>.Fail(auth.Errors);
            }

            if (await _sections.GetByIdAsync(sectionId, cancellationToken) is null)
            {
                return OfferingsErrors.SectionNotFound;
            }

            var items = await _assignments.ListAsync(new AssignmentsOfSectionSpecification(sectionId, activeOnly), cancellationToken);

            return Result<IReadOnlyList<AssignmentSummary>>.Ok([.. items.Select(a => OfferingMapper.ToSummary(a))]);
        }

        // ---------- changes ----------

        public async Task<Result<AssignmentSummary>> AssignAsync(Guid sectionId, AssignInstructorCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await _access.LoadSectionAsync(sectionId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<AssignmentSummary>.Fail(loaded.Errors);
            }

            if (context.Section.Status == CourseSectionStatus.Cancelled || context.Offering.Status == CourseOfferingStatus.Cancelled)
            {
                return OfferingsErrors.SectionNotAssignable;
            }

            var created = InstructorAssignment.Create(sectionId, command.InstructorId, command.Role, _clock.UtcNow);
            if (!created.TryGetValue(out var assignment))
            {
                return Result<AssignmentSummary>.Fail(created.Errors);
            }

            var instructor = await _instructors.GetByIdAsync(command.InstructorId, cancellationToken);
            if (instructor is null)
            {
                return OfferingsErrors.InstructorNotFound;
            }

            if (!instructor.IsActive)
            {
                return OfferingsErrors.InstructorInactive;
            }

            if (await _assignments.AnyAsync(new ActiveAssignmentSpecification(sectionId, command.InstructorId), cancellationToken))
            {
                return OfferingsErrors.AssignmentOverlap;
            }

            if (assignment.Role == AssignmentRole.Primary && await _assignments.AnyAsync(new ActivePrimarySpecification(sectionId), cancellationToken))
            {
                return OfferingsErrors.PrimaryExists;
            }

            _assignments.Add(assignment);

            return await CommitAsync(assignment, OfferingsAuditActions.InstructorAssigned, null, request, cancellationToken);
        }

        public async Task<Result<AssignmentSummary>> EndAsync(Guid sectionId, Guid assignmentId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await _access.LoadSectionAsync(sectionId, cancellationToken);
            if (!loaded.TryGetValue(out _))
            {
                return Result<AssignmentSummary>.Fail(loaded.Errors);
            }

            var assignment = await _assignments.FirstOrDefaultAsync(new AssignmentByIdSpecification(assignmentId), cancellationToken);
            if (assignment is null || assignment.CourseSectionId != sectionId)
            {
                return OfferingsErrors.AssignmentNotFound;
            }

            var before = Describe(assignment);

            var ended = assignment.End(_clock.UtcNow);
            if (ended.IsFailure)
            {
                return Result<AssignmentSummary>.Fail(ended.Errors);
            }

            return await CommitAsync(assignment, OfferingsAuditActions.InstructorAssignmentEnded, before, request, cancellationToken);
        }
    }
}
