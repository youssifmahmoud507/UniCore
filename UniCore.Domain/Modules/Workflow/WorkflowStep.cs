using UniCore.Domain.Common.Entities;

namespace UniCore.Domain.Modules.Workflow
{
    // One step of a definition: a role that must approve it. Only WorkflowDefinition creates steps.
    public sealed class WorkflowStep : BaseEntity
    {
        public const int MaxNameLength = 100;

        private WorkflowStep()
        {
        }

        public Guid WorkflowDefinitionId { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public int Order { get; private set; }
        public string RequiredRole { get; private set; } = string.Empty;

        internal static WorkflowStep Create(Guid definitionId, string name, int order, string requiredRole) => new()
        {
            Id = Guid.NewGuid(),
            WorkflowDefinitionId = definitionId,
            Name = name,
            Order = order,
            RequiredRole = requiredRole
        };
    }

}
