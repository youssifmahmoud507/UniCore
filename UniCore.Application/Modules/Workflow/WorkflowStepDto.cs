using System;
using System.Collections.Generic;
using System.Text;

namespace UniCore.Application.Modules.Workflow
{
    public sealed record WorkflowStepDto(Guid Id, int Order, string Name, string RequiredRole);



}
