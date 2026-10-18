using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Finance.Specifications;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Students.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Finance
{
    public sealed class StudentScholarshipHandler(IPermissionAuthorizer authorizer, ICurrentUser user, IAggregateRepository<StudentScholarship> awards, IAggregateRepository<Scholarship> scholarships, IAggregateRepository<Student> students, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<StudentScholarship> _awards = awards;
        private readonly IAggregateRepository<Scholarship> _scholarships = scholarships;
        private readonly IAggregateRepository<Student> _students = students;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private Task<Result> RequireAsync(string permission, CancellationToken cancellationToken) => _authorizer.AuthorizeAsync(permission, GlobalResource, cancellationToken);

        private Task AuditAsync(string action, Guid entityId, RequestInfo request, string? oldValues, string? newValues, CancellationToken cancellationToken) => _audit.WriteAsync(
            new AuditEntry(action, _user.UserId, "StudentScholarship", entityId.ToString(), request.IpAddress, request.UserAgent, oldValues, newValues),
            cancellationToken);

        public async Task<Result<StudentScholarshipDto>> AwardAsync(AwardScholarshipCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Scholarships.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<StudentScholarshipDto>.Fail(auth.Errors);
            }

            if (command.StudentId is not { } studentId || studentId == Guid.Empty
                || command.ScholarshipId is not { } scholarshipId || scholarshipId == Guid.Empty)
            {
                return FinanceErrors.ScholarshipAwardInvalid;
            }

            if (!await _students.AnyAsync(new StudentByIdSpecification(studentId), cancellationToken))
            {
                return StudentErrors.NotFound;
            }

            var scholarship = await _scholarships.FirstOrDefaultAsync(new ScholarshipByIdSpecification(scholarshipId), cancellationToken);
            if (scholarship is null)
            {
                return FinanceErrors.ScholarshipNotFound;
            }

            if (!scholarship.IsActive)
            {
                return FinanceErrors.ScholarshipInactive;
            }

            if (await _awards.AnyAsync(new ActiveAwardSpecification(studentId, scholarshipId), cancellationToken))
            {
                return FinanceErrors.ScholarshipAwardExists;
            }

            var created = StudentScholarship.Award(studentId, scholarshipId, command.ValidFrom ?? _clock.UtcToday, command.ValidTo, command.Reason, _user.UserId, _clock.UtcNow);
            if (!created.TryGetValue(out var award))
            {
                return Result<StudentScholarshipDto>.Fail(created.Errors);
            }

            _awards.Add(award);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return saved.Error?.Code == CommonErrors.DuplicateResource.Code
                    ? FinanceErrors.ScholarshipAwardExists
                    : Result<StudentScholarshipDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.ScholarshipAwarded, award.Id, request, null, $"student {studentId} scholarship {scholarship.Code} from {award.ValidFrom}", cancellationToken);

            return ReductionsMapper.ToDto(award);
        }

        public async Task<Result<StudentScholarshipDto>> RevokeAsync(Guid awardId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Scholarships.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<StudentScholarshipDto>.Fail(auth.Errors);
            }

            var award = await _awards.FirstOrDefaultAsync(new StudentScholarshipByIdSpecification(awardId, tracking: true), cancellationToken);
            if (award is null)
            {
                return FinanceErrors.ScholarshipAwardNotFound;
            }

            var revoked = award.Revoke(_user.UserId, _clock.UtcNow);
            if (revoked.IsFailure)
            {
                return Result<StudentScholarshipDto>.Fail(revoked.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<StudentScholarshipDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.ScholarshipRevoked, award.Id, request, StudentScholarshipStatus.Active.ToString(), StudentScholarshipStatus.Revoked.ToString(), cancellationToken);

            return ReductionsMapper.ToDto(award);
        }

        public async Task<Result<PagedResult<StudentScholarshipDto>>> ListAsync(PaginationQuery query, Guid? studentId, StudentScholarshipStatus? status, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Finance.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<StudentScholarshipDto>>.Fail(auth.Errors);
            }

            var specification = new StudentScholarshipsListSpecification(query, studentId, status);

            var items = await _awards.ListAsync(specification, cancellationToken);
            var total = await _awards.CountAsync(specification, cancellationToken);

            return PagedResult<StudentScholarshipDto>.Create(items.Select(a => ReductionsMapper.ToDto(a)).ToList(), query, total);
        }
    }






}
