using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.UnitTests.Modules.Workflow
{
    public class WorkflowInstanceTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);

        private readonly Guid _requester = Guid.NewGuid();
        private readonly Guid _approver = Guid.NewGuid();

        private static WorkflowDefinition Definition(string code = "WITHDRAWAL_REQUEST")
            => WorkflowDefinition.Create(code, "Withdrawal", 1, [new("Department head", RoleNames.DepartmentHead), new("Dean", RoleNames.Dean), new("Registrar", RoleNames.Registrar)], Now).Value!;

        private WorkflowInstance Start(WorkflowDefinition definition, ScopeType? scope = null, Guid? scopeId = null)
            => WorkflowInstance.Start(definition, _requester, "WithdrawalRequest", Guid.NewGuid(), null, scope, scopeId, Now).Value!;

        [Fact]
        public void A_new_instance_waits_at_the_first_step_and_raises_started()
        {
            var instance = Start(Definition(), ScopeType.Department, Guid.NewGuid());

            Assert.Equal(WorkflowInstanceStatus.InProgress, instance.Status);
            Assert.Equal(1, instance.CurrentStepOrder);
            Assert.Equal(RoleNames.DepartmentHead, instance.CurrentRole);
            Assert.Equal(3, instance.TotalSteps);
            Assert.Single(instance.Actions);
            Assert.IsType<WorkflowStarted>(Assert.Single(instance.DomainEvents));
        }

        [Fact]
        public void Approving_every_step_completes_the_instance()
        {
            var definition = Definition();
            var instance = Start(definition);

            Assert.True(instance.Approve(definition, _approver, " fine ", Now).IsSuccess);
            Assert.Equal(RoleNames.Dean, instance.CurrentRole);

            Assert.True(instance.Approve(definition, _approver, null, Now).IsSuccess);
            Assert.Equal(RoleNames.Registrar, instance.CurrentRole);

            Assert.True(instance.Approve(definition, _approver, null, Now).IsSuccess);
            Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
            Assert.Null(instance.CurrentRole);
            Assert.NotNull(instance.CompletedAt);
            Assert.Equal(3, instance.Approvals.Count);
            Assert.Equal(2, instance.DomainEvents.OfType<WorkflowStepAdvanced>().Count());
            Assert.Single(instance.DomainEvents.OfType<WorkflowCompleted>());
        }

        [Fact]
        public void The_requester_cannot_decide_their_own_request()
        {
            var definition = Definition();
            var instance = Start(definition);

            Assert.Equal(WorkflowErrors.SelfApproval.Code, instance.Approve(definition, _requester, null, Now).Error?.Code);
            Assert.Equal(WorkflowErrors.SelfApproval.Code, instance.Reject(definition, _requester, "no", Now).Error?.Code);
            Assert.Equal(1, instance.CurrentStepOrder);
        }

        [Fact]
        public void Reject_and_return_need_a_comment()
        {
            var definition = Definition();
            var instance = Start(definition);

            Assert.Equal(WorkflowErrors.CommentRequired.Code, instance.Reject(definition, _approver, "  ", Now).Error?.Code);
            Assert.Equal(WorkflowErrors.CommentRequired.Code, instance.Return(definition, _approver, null, Now).Error?.Code);
            Assert.Equal(WorkflowInstanceStatus.InProgress, instance.Status);

            Assert.True(instance.Reject(definition, _approver, "not allowed", Now).IsSuccess);
            Assert.Equal(WorkflowInstanceStatus.Rejected, instance.Status);
            Assert.Single(instance.DomainEvents.OfType<WorkflowRejected>());
        }

        [Fact]
        public void A_returned_instance_restarts_at_step_one_when_its_requester_resubmits()
        {
            var definition = Definition();
            var instance = Start(definition);
            Assert.True(instance.Approve(definition, _approver, null, Now).IsSuccess);

            Assert.True(instance.Return(definition, _approver, "fix the dates", Now).IsSuccess);
            Assert.Equal(WorkflowInstanceStatus.Returned, instance.Status);
            Assert.Null(instance.CurrentRole);
            Assert.Equal(WorkflowErrors.InvalidState.Code, instance.Approve(definition, _approver, null, Now).Error?.Code);

            Assert.Equal(WorkflowErrors.NotRequester.Code, instance.Resubmit(definition, _approver, null, Now).Error?.Code);

            Assert.True(instance.Resubmit(definition, _requester, "fixed", Now).IsSuccess);
            Assert.Equal(WorkflowInstanceStatus.InProgress, instance.Status);
            Assert.Equal(1, instance.CurrentStepOrder);
            Assert.Equal(RoleNames.DepartmentHead, instance.CurrentRole);

            Assert.Equal(WorkflowErrors.InvalidState.Code, instance.Resubmit(definition, _requester, null, Now).Error?.Code);
        }

        [Fact]
        public void Only_the_requester_or_an_admin_can_cancel_and_a_finished_instance_cannot_be_cancelled()
        {
            var definition = Definition();

            var stranger = Start(definition);
            Assert.Equal(WorkflowErrors.NotRequester.Code, stranger.Cancel(_approver, false, null, Now).Error?.Code);
            Assert.True(stranger.Cancel(_approver, true, "expired", Now).IsSuccess);
            Assert.Equal(WorkflowInstanceStatus.Cancelled, stranger.Status);
            Assert.Single(stranger.DomainEvents.OfType<WorkflowCancelled>());

            var own = Start(definition);
            Assert.True(own.Cancel(_requester, false, null, Now).IsSuccess);
            Assert.Equal(WorkflowErrors.InvalidState.Code, own.Cancel(_requester, false, null, Now).Error?.Code);
        }

        [Fact]
        public void An_instance_stays_on_the_definition_it_started_with()
        {
            var instance = Start(Definition());

            Assert.Equal(WorkflowErrors.InvalidState.Code, instance.Approve(Definition("OTHER_FLOW"), _approver, null, Now).Error?.Code);
        }

        [Fact]
        public void An_inactive_definition_cannot_start_and_bad_request_data_is_refused()
        {
            var inactive = Definition();
            inactive.Deactivate(Now);

            Assert.Equal(WorkflowErrors.DefinitionInactive.Code, WorkflowInstance.Start(inactive, _requester, "X", Guid.NewGuid(), null, null, null, Now).Error?.Code);

            var definition = Definition();
            Assert.True(WorkflowInstance.Start(definition, _requester, "X", Guid.Empty, null, null, null, Now).IsFailure);
            Assert.True(WorkflowInstance.Start(definition, _requester, " ", Guid.NewGuid(), null, null, null, Now).IsFailure);
            Assert.True(WorkflowInstance.Start(definition, Guid.Empty, "X", Guid.NewGuid(), null, null, null, Now).IsFailure);
            Assert.True(WorkflowInstance.Start(definition, _requester, "X", Guid.NewGuid(), null, ScopeType.Department, null, Now).IsFailure);
            Assert.True(WorkflowInstance.Start(definition, _requester, "X", Guid.NewGuid(), null, ScopeType.Global, Guid.NewGuid(), Now).IsFailure);
        }

        [Fact]
        public void A_comment_longer_than_the_limit_is_refused()
        {
            var definition = Definition();
            var instance = Start(definition);

            Assert.True(instance.Approve(definition, _approver, new string('x', WorkflowAction.MaxCommentLength + 1), Now).IsFailure);
            Assert.Equal(1, instance.CurrentStepOrder);
        }
    }

}
