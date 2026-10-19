using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Workflow
{
    // The definitions every installation starts with (workflow.md section 5). The seeder creates the ones that do not exist yet.
    public static class WorkflowSeedCatalog
    {
        public sealed record SeedDefinition(string Code, string Name, IReadOnlyList<WorkflowStepInput> Steps);

        public static readonly IReadOnlyList<SeedDefinition> Definitions =
        [
            new("CERTIFICATE_REQUEST", "Certificate request", [new("Registrar review", RoleNames.Registrar)]),
            new("TRANSCRIPT_REQUEST", "Transcript request", [new("Registrar review", RoleNames.Registrar)]),
            new("WITHDRAWAL_REQUEST", "Withdrawal request", [new("Department head", RoleNames.DepartmentHead), new("Dean", RoleNames.Dean), new("Registrar", RoleNames.Registrar)]),
            new("LEAVE_REQUEST", "Leave request", [new("Department head", RoleNames.DepartmentHead), new("Dean", RoleNames.Dean)]),
            new("COMPLAINT", "Complaint", [new("Department head", RoleNames.DepartmentHead), new("Dean", RoleNames.Dean)]),
            new("APPEAL", "Appeal", [new("Department head", RoleNames.DepartmentHead), new("Dean", RoleNames.Dean)]),
            new("GRADE_APPEAL", "Grade appeal", [new("Department head", RoleNames.DepartmentHead), new("Dean", RoleNames.Dean)])
        ];
    }



}
