using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.UnitTests.Modules.Workflow
{
    public class WorkflowDefinitionTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);

        private static List<WorkflowStepInput> Steps() => [new("Department head", RoleNames.DepartmentHead), new("Dean", RoleNames.Dean)];

        [Fact]
        public void A_valid_definition_is_active_and_orders_its_steps()
        {
            var result = WorkflowDefinition.Create("  withdrawal_request ", " Withdrawal ", 1, Steps(), Now);

            Assert.True(result.TryGetValue(out var definition));
            Assert.Equal("WITHDRAWAL_REQUEST", definition!.Code);
            Assert.Equal("Withdrawal", definition.Name);
            Assert.True(definition.IsActive);
            Assert.Equal(2, definition.Steps.Count);
            Assert.Equal([1, 2], definition.Steps.Select(s => s.Order));
            Assert.Equal(RoleNames.Dean, definition.Steps[1].RequiredRole);
            Assert.Equal(definition.Steps[1].Id, definition.NextAfter(1)?.Id);
            Assert.Null(definition.NextAfter(2));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("ab")]
        [InlineData("has space")]
        [InlineData("with-dash")]
        public void A_bad_code_is_refused(string? code)
        {
            Assert.Null(WorkflowDefinition.NormalizeCode(code));
            Assert.Equal(WorkflowErrors.DefinitionDataInvalid.Code, WorkflowDefinition.Create(code, "Name", 1, Steps(), Now).Error?.Code);
        }

        [Fact]
        public void Students_instructors_super_admin_and_unknown_roles_cannot_be_steps()
        {
            foreach (var role in new[] { RoleNames.Student, RoleNames.Instructor, RoleNames.SuperAdmin, "Nobody", null })
            {
                var result = WorkflowDefinition.Create("A_FLOW", "Flow", 1, [new("Step one", role)], Now);

                Assert.Equal(WorkflowErrors.DefinitionDataInvalid.Code, result.Error?.Code);
            }
        }

        [Fact]
        public void The_number_of_steps_name_and_version_are_checked()
        {
            Assert.True(WorkflowDefinition.Create("A_FLOW", "Flow", 1, [], Now).IsFailure);

            var tooMany = Enumerable.Range(0, WorkflowDefinition.MaxSteps + 1).Select(i => new WorkflowStepInput($"Step {i}", RoleNames.Registrar)).ToList();
            Assert.True(WorkflowDefinition.Create("A_FLOW", "Flow", 1, tooMany, Now).IsFailure);

            Assert.True(WorkflowDefinition.Create("A_FLOW", " ", 1, Steps(), Now).IsFailure);
            Assert.True(WorkflowDefinition.Create("A_FLOW", "Flow", 0, Steps(), Now).IsFailure);
            Assert.True(WorkflowDefinition.Create("A_FLOW", "Flow", 1, [new(" ", RoleNames.Registrar)], Now).IsFailure);
        }

        [Fact]
        public void A_definition_can_be_deactivated_and_activated_again()
        {
            var definition = WorkflowDefinition.Create("A_FLOW", "Flow", 1, Steps(), Now).Value!;

            definition.Deactivate(Now);
            Assert.False(definition.IsActive);

            definition.Activate(Now);
            Assert.True(definition.IsActive);
        }

        [Fact]
        public void The_first_step_is_found_by_order_even_when_the_steps_come_back_in_another_order()
        {
            var definition = WorkflowDefinition.Create("SHUFFLED_FLOW", "Shuffled", 1, [new("Department head", RoleNames.DepartmentHead), new("Dean", RoleNames.Dean), new("Registrar", RoleNames.Registrar)], Now).Value!;

            // The database returns the children in any order.
            var field = typeof(WorkflowDefinition).GetField("_steps", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(field);
            var steps = Assert.IsType<List<WorkflowStep>>(field.GetValue(definition));
            steps.Reverse();

            Assert.Equal(3, definition.Steps[0].Order);
            Assert.Equal(1, definition.FirstStep.Order);
            Assert.Equal(RoleNames.DepartmentHead, definition.FirstStep.RequiredRole);
            Assert.Equal(RoleNames.Dean, definition.NextAfter(1)?.RequiredRole);
        }
    }



}
