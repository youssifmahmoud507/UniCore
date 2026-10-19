using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Workflow.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Workflow
{
    // Managing definitions needs Settings.Manage. Reading them needs Requests.Read (a student can see what a request goes through).
    public sealed class WorkflowDefinitionHandler(IPermissionAuthorizer authorizer, ICurrentUser user, IAggregateRepository<WorkflowDefinition> definitions, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<WorkflowDefinition> _definitions = definitions;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        // Publishes a NEW VERSION: the next number for the code, active at once. The version that was active is
        // deactivated (its running instances finish on it). A definition is never edited.
        public async Task<Result<WorkflowDefinitionDto>> CreateAsync(CreateWorkflowDefinitionCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Settings.Manage, GlobalResource, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<WorkflowDefinitionDto>.Fail(auth.Errors);
            }

            var code = WorkflowDefinition.NormalizeCode(command.Code);
            if (code is null || command.Steps is null)
            {
                return WorkflowErrors.DefinitionDataInvalid;
            }

            var versions = await _definitions.ListAsync(new WorkflowDefinitionsByCodeSpecification(code, tracking: true), cancellationToken);
            var next = versions.Count == 0 ? 1 : versions.Max(v => v.Version) + 1;

            var created = WorkflowDefinition.Create(code, command.Name, next, command.Steps, _clock.UtcNow);
            if (!created.TryGetValue(out var definition))
            {
                return Result<WorkflowDefinitionDto>.Fail(created.Errors);
            }

            foreach (var old in versions.Where(v => v.IsActive))
            {
                old.Deactivate(_clock.UtcNow);
            }

            _definitions.Add(definition);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                // Two people publishing the same code at the same time: the unique indexes let one win.
                return saved.Error?.Code == CommonErrors.DuplicateResource.Code
                    ? WorkflowErrors.DefinitionAlreadyExists
                    : Result<WorkflowDefinitionDto>.Fail(saved.Errors);
            }

            await _audit.WriteAsync(
                new AuditEntry(WorkflowAuditActions.DefinitionCreated, _user.UserId, "WorkflowDefinition", definition.Id.ToString(), request.IpAddress, request.UserAgent, null, $"{definition.Code} v{definition.Version}, {definition.Steps.Count} steps"),
                cancellationToken);

            return WorkflowMapper.ToDto(definition);
        }

        // Stops new requests from using this version. Running instances finish normally.
        public async Task<Result<WorkflowDefinitionDto>> DeactivateAsync(Guid definitionId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Settings.Manage, GlobalResource, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<WorkflowDefinitionDto>.Fail(auth.Errors);
            }

            var definition = await _definitions.FirstOrDefaultAsync(new WorkflowDefinitionByIdSpecification(definitionId, tracking: true), cancellationToken);
            if (definition is null)
            {
                return WorkflowErrors.DefinitionNotFound;
            }

            definition.Deactivate(_clock.UtcNow);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<WorkflowDefinitionDto>.Fail(saved.Errors);
            }

            await _audit.WriteAsync(
                new AuditEntry(WorkflowAuditActions.DefinitionDeactivated, _user.UserId, "WorkflowDefinition", definition.Id.ToString(), request.IpAddress, request.UserAgent, null, $"{definition.Code} v{definition.Version}"),
                cancellationToken);

            return WorkflowMapper.ToDto(definition);
        }

        public async Task<Result<PagedResult<WorkflowDefinitionDto>>> ListAsync(PaginationQuery query, string? code, bool? isActive, CancellationToken cancellationToken = default)
        {
            var auth = await CanReadAsync(cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<WorkflowDefinitionDto>>.Fail(auth.Errors);
            }

            var cleanCode = string.IsNullOrWhiteSpace(code) ? null : WorkflowDefinition.NormalizeCode(code) ?? "?";
            var specification = new WorkflowDefinitionsListSpecification(query, cleanCode, isActive);

            var items = await _definitions.ListAsync(specification, cancellationToken);
            var total = await _definitions.CountAsync(specification, cancellationToken);

            return PagedResult<WorkflowDefinitionDto>.Create(items.Select(WorkflowMapper.ToDto).ToList(), query, total);
        }

        public async Task<Result<WorkflowDefinitionDto>> GetAsync(Guid definitionId, CancellationToken cancellationToken = default)
        {
            var auth = await CanReadAsync(cancellationToken);
            if (auth.IsFailure)
            {
                return Result<WorkflowDefinitionDto>.Fail(auth.Errors);
            }

            var definition = await _definitions.FirstOrDefaultAsync(new WorkflowDefinitionByIdSpecification(definitionId), cancellationToken);

            return definition is null ? WorkflowErrors.DefinitionNotFound : WorkflowMapper.ToDto(definition);
        }

        private async Task<Result> CanReadAsync(CancellationToken cancellationToken)
        {
            if (!_user.IsAuthenticated || _user.UserId is not { } userId)
            {
                return Result.Fail(Error.Unauthorized());
            }

            return await _authorizer.AuthorizeAsync(Permissions.Requests.Read, ResourceContext.OwnedBy(userId), cancellationToken);
        }
    }



}
