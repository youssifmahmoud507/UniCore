using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Students.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Finance
{
    // The money position of one student: balance, active holds and what they block.
    public sealed class StudentFinanceHandler(IPermissionAuthorizer authorizer, IFinancialEligibilityService eligibility, IAggregateRepository<Student> students)
    {
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly IFinancialEligibilityService _eligibility = eligibility;
        private readonly IAggregateRepository<Student> _students = students;

        public async Task<Result<StudentFinanceDto>> GetAsync(Guid studentId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Finance.Read, GlobalResource, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<StudentFinanceDto>.Fail(auth.Errors);
            }

            if (!await _students.AnyAsync(new StudentByIdSpecification(studentId), cancellationToken))
            {
                return StudentErrors.NotFound;
            }

            var balance = await _eligibility.GetBalanceAsync(studentId, cancellationToken);
            if (!balance.TryGetValue(out var money))
            {
                return Result<StudentFinanceDto>.Fail(balance.Errors);
            }

            var holds = await _eligibility.GetActiveHoldsAsync(studentId, cancellationToken);
            if (!holds.TryGetValue(out var active))
            {
                return Result<StudentFinanceDto>.Fail(holds.Errors);
            }

            return new StudentFinanceDto(
                money,
                active,
                active.Any(h => h.Areas.Contains(FinancialHoldArea.Registration)),
                active.Any(h => h.Areas.Contains(FinancialHoldArea.Graduation)),
                active.Any(h => h.Areas.Contains(FinancialHoldArea.Services)));
        }
    }




}
