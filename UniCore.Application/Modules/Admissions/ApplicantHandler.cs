using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Modules.Admissions.Specifications;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Admissions;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Admissions
{
    // Applicant records are entered by admissions staff (Admissions.Review) and read with Admissions.Read.
    // They hold personal data, so audit values carry only the id. An applicant has no program yet,
    // so these checks are global, not scoped.
    public sealed class ApplicantHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user, IAggregateRepository<Applicant> applicants,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<Applicant> _applicants = applicants;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private Task AuditAsync(string action, Applicant applicant, RequestInfo request, CancellationToken cancellationToken)
            => _audit.WriteAsync(new AuditEntry(action, _user.UserId, "Applicant", applicant.Id.ToString(), request.IpAddress, request.UserAgent), cancellationToken);

        public async Task<Result<PagedResult<ApplicantSummary>>> ListAsync(PaginationQuery query, string? search, string? sort, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Admissions.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<ApplicantSummary>>.Fail(auth.Errors);
            }

            var parsed = ListQueryRules.ParseSort(sort, ApplicantsListSpecification.SortFields, "name");
            if (!parsed.TryGetValue(out var sortOption))
            {
                return Result<PagedResult<ApplicantSummary>>.Fail(parsed.Errors);
            }

            var specification = new ApplicantsListSpecification(query, ListQueryRules.NormalizeSearch(search), sortOption);

            var items = await _applicants.ListAsync(specification, cancellationToken);
            var total = await _applicants.CountAsync(specification, cancellationToken);

            return Result<PagedResult<ApplicantSummary>>.Ok(PagedResult<ApplicantSummary>.Create([.. items.Select(x => AdmissionsMapper.ToSummary(x))], query, total));
        }

        public async Task<Result<ApplicantSummary>> GetAsync(Guid applicantId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Admissions.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<ApplicantSummary>.Fail(auth.Errors);
            }

            var applicant = await _applicants.GetByIdAsync(applicantId, cancellationToken);

            return applicant is null ? AdmissionsErrors.ApplicantNotFound : AdmissionsMapper.ToSummary(applicant);
        }

        public async Task<Result<ApplicantSummary>> CreateAsync(CreateApplicantCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Admissions.Review, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<ApplicantSummary>.Fail(auth.Errors);
            }

            if (command.DateOfBirth is not { } dateOfBirth)
            {
                return AdmissionsErrors.InvalidDateOfBirth;
            }

            var created = Applicant.Create(command.FullName, command.Email, command.Phone, dateOfBirth, _clock.UtcToday, _clock.UtcNow);
            if (!created.TryGetValue(out var applicant))
            {
                return Result<ApplicantSummary>.Fail(created.Errors);
            }

            if (await _applicants.AnyAsync(new ApplicantByEmailSpecification(applicant.Email), cancellationToken))
            {
                return AdmissionsErrors.ApplicantEmailExists;
            }

            _applicants.Add(applicant);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<ApplicantSummary>.Fail(saved.Errors);
            }

            await AuditAsync(AdmissionsAuditActions.ApplicantCreated, applicant, request, cancellationToken);

            return AdmissionsMapper.ToSummary(applicant);
        }

        public async Task<Result<ApplicantSummary>> UpdateAsync(Guid applicantId, UpdateApplicantCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Admissions.Review, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<ApplicantSummary>.Fail(auth.Errors);
            }

            var applicant = await _applicants.FirstOrDefaultAsync(new ApplicantByIdSpecification(applicantId), cancellationToken);
            if (applicant is null)
            {
                return AdmissionsErrors.ApplicantNotFound;
            }

            if (command.DateOfBirth is not { } dateOfBirth)
            {
                return AdmissionsErrors.InvalidDateOfBirth;
            }

            var updated = applicant.UpdateDetails(command.FullName, command.Email, command.Phone, dateOfBirth, _clock.UtcToday, _clock.UtcNow);
            if (updated.IsFailure)
            {
                return Result<ApplicantSummary>.Fail(updated.Errors);
            }

            // The email is normalized by the domain, so the duplicate check runs after the update.
            var duplicate = await _applicants.AnyAsync(new ApplicantEmailTakenSpecification(applicant.Email, applicant.Id), cancellationToken);
            if (duplicate)
            {
                return AdmissionsErrors.ApplicantEmailExists;
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<ApplicantSummary>.Fail(saved.Errors);
            }

            await AuditAsync(AdmissionsAuditActions.ApplicantUpdated, applicant, request, cancellationToken);

            return AdmissionsMapper.ToSummary(applicant);
        }
    }
}
