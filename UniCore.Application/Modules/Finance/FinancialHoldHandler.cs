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
    public sealed class FinancialHoldHandler(IPermissionAuthorizer authorizer, ICurrentUser user, IAggregateRepository<FinancialHold> holds, IAggregateRepository<Student> students, FinancialHoldEvaluator evaluator, IFinancialHoldRunner runner, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<FinancialHold> _holds = holds;
        private readonly IAggregateRepository<Student> _students = students;
        private readonly FinancialHoldEvaluator _evaluator = evaluator;
        private readonly IFinancialHoldRunner _runner = runner;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private Task<Result> RequireAsync(string permission, CancellationToken cancellationToken) => _authorizer.AuthorizeAsync(permission, GlobalResource, cancellationToken);

        private Task AuditAsync(string action, Guid entityId, RequestInfo request, string? oldValues, string? newValues, CancellationToken cancellationToken) => _audit.WriteAsync(
            new AuditEntry(action, _user.UserId, "FinancialHold", entityId.ToString(), request.IpAddress, request.UserAgent, oldValues, newValues),
            cancellationToken);

        public async Task<Result<FinancialHoldDto>> PlaceAsync(PlaceHoldCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Holds.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<FinancialHoldDto>.Fail(auth.Errors);
            }

            if (command.StudentId is not { } studentId || studentId == Guid.Empty
                || command.Areas is not { Count: > 0 } areaList)
            {
                return FinanceErrors.FinancialHoldDataInvalid;
            }

            var areas = areaList.Aggregate(FinancialHoldArea.None, (all, area) => all | area);

            if (!await _students.AnyAsync(new StudentByIdSpecification(studentId), cancellationToken))
            {
                return StudentErrors.NotFound;
            }

            var created = FinancialHold.PlaceManual(studentId, areas, command.Note, _user.UserId, _clock.UtcNow);
            if (!created.TryGetValue(out var hold))
            {
                return Result<FinancialHoldDto>.Fail(created.Errors);
            }

            _holds.Add(hold);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<FinancialHoldDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.FinancialHoldPlaced, hold.Id, request, null, $"student {studentId} areas {hold.Areas}", cancellationToken);

            return FinancialHoldMapper.ToDto(hold);
        }

        // A person must say why. Releasing an automatic hold is a waiver: it is not placed again for a while.
        public async Task<Result<FinancialHoldDto>> ReleaseAsync(Guid holdId, string? note, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Holds.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<FinancialHoldDto>.Fail(auth.Errors);
            }

            var hold = await _holds.FirstOrDefaultAsync(new HoldByIdSpecification(holdId, tracking: true), cancellationToken);
            if (hold is null)
            {
                return FinanceErrors.FinancialHoldNotFound;
            }

            var released = hold.Release(_user.UserId, note, _clock.UtcNow);
            if (released.IsFailure)
            {
                return Result<FinancialHoldDto>.Fail(released.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<FinancialHoldDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.FinancialHoldReleased, hold.Id, request, FinancialHoldStatus.Active.ToString(), hold.ReleaseNote, cancellationToken);

            return FinancialHoldMapper.ToDto(hold);
        }

        public async Task<Result<PagedResult<FinancialHoldDto>>> ListAsync(PaginationQuery query, Guid? studentId, FinancialHoldStatus? status, FinancialHoldReason? reason, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Finance.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<FinancialHoldDto>>.Fail(auth.Errors);
            }

            var specification = new HoldsListSpecification(query, studentId, status, reason);

            var items = await _holds.ListAsync(specification, cancellationToken);
            var total = await _holds.CountAsync(specification, cancellationToken);

            return PagedResult<FinancialHoldDto>.Create(items.Select(h => FinancialHoldMapper.ToDto(h)).ToList(), query, total);
        }

        public async Task<Result<FinancialHoldDto>> GetAsync(Guid holdId, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Finance.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<FinancialHoldDto>.Fail(auth.Errors);
            }

            var hold = await _holds.FirstOrDefaultAsync(new HoldByIdSpecification(holdId), cancellationToken);

            return hold is null ? FinanceErrors.FinancialHoldNotFound : FinancialHoldMapper.ToDto(hold);
        }

        // Runs the same evaluation as the daily job, now: for one student, or for everybody.
        public async Task<Result<HoldEvaluationResult>> EvaluateAsync(Guid? studentId, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Holds.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<HoldEvaluationResult>.Fail(auth.Errors);
            }

            var today = _clock.UtcToday;

            if (studentId is { } id)
            {
                return await _evaluator.EvaluateStudentAsync(id, today, cancellationToken);
            }

            return await _runner.RunAsync(today, cancellationToken);
        }
    }




}
