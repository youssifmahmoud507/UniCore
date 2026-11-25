using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.StudentServices;
using UniCore.Domain.Modules.Workflow;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.StudentServices
{
    public class ServiceRequestDomainTests
    {
        private static readonly DateTimeOffset Now = new(2027, 6, 20, 9, 0, 0, TimeSpan.Zero);

        private static Complaint NewComplaint() => ResultAssert.Succeeded(Complaint.Create(Guid.NewGuid(), Guid.NewGuid(), ComplaintCategory.Academic, "Subject", "Details", Now));

        [Fact]
        public void A_request_needs_its_text_and_a_defined_type()
        {
            ResultAssert.Failed(CertificateRequest.Create(Guid.NewGuid(), Guid.NewGuid(), CertificateType.GoodStanding, "  ", Now), ServiceRequestErrors.TextRequired);
            ResultAssert.Failed(CertificateRequest.Create(Guid.NewGuid(), Guid.NewGuid(), (CertificateType)99, "Visa", Now), ServiceRequestErrors.Invalid);
            ResultAssert.Failed(CertificateRequest.Create(Guid.NewGuid(), Guid.NewGuid(), CertificateType.GoodStanding, new string('x', ServiceRequest.ShortTextMaxLength + 1), Now), ServiceRequestErrors.TextTooLong);
            ResultAssert.Failed(CertificateRequest.Create(Guid.Empty, Guid.NewGuid(), CertificateType.GoodStanding, "Visa", Now), ServiceRequestErrors.Invalid);
            ResultAssert.Failed(Complaint.Create(Guid.NewGuid(), Guid.NewGuid(), ComplaintCategory.Other, "Subject", null, Now), ServiceRequestErrors.TextRequired);
            ResultAssert.Failed(Appeal.Create(Guid.NewGuid(), Guid.NewGuid(), AppealTarget.Dismissal, null, "Reason", Now), ServiceRequestErrors.TextRequired);
            Assert.Equal(ServiceRequestStatus.Pending, NewComplaint().Status);
        }

        [Fact]
        public void Approve_reject_and_return_only_work_on_a_pending_request()
        {
            var approved = NewComplaint();
            ResultAssert.Succeeded(approved.Approve(Guid.NewGuid(), Now));
            ResultAssert.Failed(approved.Approve(Guid.NewGuid(), Now), ServiceRequestErrors.InvalidState);
            ResultAssert.Failed(approved.Reject(Guid.NewGuid(), "no", Now), ServiceRequestErrors.InvalidState);
            ResultAssert.Failed(approved.Return(Guid.NewGuid(), "fix", Now), ServiceRequestErrors.InvalidState);
            ResultAssert.Failed(approved.Cancel(Guid.NewGuid(), Now), ServiceRequestErrors.InvalidState);
        }

        [Fact]
        public void Reject_and_return_need_a_reason()
        {
            ResultAssert.Failed(NewComplaint().Reject(Guid.NewGuid(), " ", Now), ServiceRequestErrors.NoteRequired);
            ResultAssert.Failed(NewComplaint().Return(Guid.NewGuid(), null, Now), ServiceRequestErrors.NoteRequired);

            var rejected = NewComplaint();
            ResultAssert.Succeeded(rejected.Reject(Guid.NewGuid(), " Out of time ", Now));
            Assert.Equal(ServiceRequestStatus.Rejected, rejected.Status);
            Assert.Equal("Out of time", rejected.DecisionNote);
        }

        [Fact]
        public void A_returned_request_is_resubmitted_and_the_decision_is_cleared()
        {
            var request = NewComplaint();
            ResultAssert.Succeeded(request.Return(Guid.NewGuid(), "Add the dates", Now));
            Assert.True(request.IsOpen);

            ResultAssert.Succeeded(request.Resubmit(Now.AddHours(1)));

            Assert.Equal(ServiceRequestStatus.Pending, request.Status);
            Assert.Null(request.DecisionNote);
            Assert.Null(request.DecidedAt);
            ResultAssert.Failed(request.Resubmit(Now.AddHours(2)), ServiceRequestErrors.InvalidState);
        }

        [Fact]
        public void An_open_request_can_be_cancelled_a_decided_one_cannot()
        {
            var pending = NewComplaint();
            ResultAssert.Succeeded(pending.Cancel(Guid.NewGuid(), Now));
            Assert.Equal(ServiceRequestStatus.Cancelled, pending.Status);

            var rejected = NewComplaint();
            ResultAssert.Succeeded(rejected.Reject(Guid.NewGuid(), "no", Now));
            ResultAssert.Failed(rejected.Cancel(Guid.NewGuid(), Now), ServiceRequestErrors.InvalidState);
        }

        [Fact]
        public void The_workflow_attaches_once()
        {
            var request = NewComplaint();
            ResultAssert.Failed(request.AttachWorkflow(Guid.Empty), ServiceRequestErrors.Invalid);
            ResultAssert.Succeeded(request.AttachWorkflow(Guid.NewGuid()));
            ResultAssert.Failed(request.AttachWorkflow(Guid.NewGuid()), ServiceRequestErrors.WorkflowAlreadyAttached);
        }

        [Fact]
        public void A_transcript_is_issued_once_and_only_after_approval()
        {
            var request = ResultAssert.Succeeded(TranscriptRequest.Create(Guid.NewGuid(), Guid.NewGuid(), "Embassy", Now));
            var snapshot = Guid.NewGuid();

            ResultAssert.Failed(request.RecordIssued(snapshot, Now), ServiceRequestErrors.InvalidState);

            ResultAssert.Succeeded(request.Approve(Guid.NewGuid(), Now));
            ResultAssert.Succeeded(request.RecordIssued(snapshot, Now));
            ResultAssert.Succeeded(request.RecordIssued(snapshot, Now));
            ResultAssert.Failed(request.RecordIssued(Guid.NewGuid(), Now), ServiceRequestErrors.InvalidState);
            Assert.True(request.IsIssued);
        }
    }

    public class ServiceRequestHandlerTests
    {
        private static readonly RequestInfo Info = StudentServicesTestEnv.Request;

        [Fact]
        public async Task A_certificate_is_saved_charged_and_sent_to_the_workflow()
        {
            var env = new StudentServicesTestEnv();
            var student = env.SeedStudent();
            env.AsStudent(student);

            var submitted = ResultAssert.Succeeded(await env.CertificateHandler.SubmitAsync(student.Id, CertificateType.GoodStanding, "Visa", Info));

            var request = Assert.Single(env.Certificates.Items);
            Assert.Equal(ServiceRequestStatus.Pending, submitted.Status);
            Assert.Equal(env.Workflows.LastInstanceId, request.WorkflowInstanceId);
            Assert.NotNull(request.ChargeId);

            var charge = Assert.Single(env.Charges.Requests);
            Assert.Equal(ServiceRequestKinds.CertificateFeeCode, charge.FeeCode);
            Assert.Equal($"{ServiceRequestKinds.CertificateReference}:{request.Id}", charge.Reference);

            var started = Assert.Single(env.Workflows.Started);
            Assert.Equal(ServiceRequestKinds.CertificateDefinition, started.DefinitionCode);
            Assert.Equal(ServiceRequestKinds.CertificateReference, started.ReferenceType);
            Assert.Equal(request.Id, started.ReferenceId);
            Assert.Equal(student.Id, started.SubjectStudentId);
            Assert.Single(env.Rbac.AuditOf(ServiceRequestAuditActions.Submitted));
        }

        [Fact]
        public async Task One_open_certificate_per_type_but_another_type_is_allowed()
        {
            var env = new StudentServicesTestEnv();
            var student = env.SeedStudent();
            env.AsStudent(student);

            ResultAssert.Succeeded(await env.CertificateHandler.SubmitAsync(student.Id, CertificateType.GoodStanding, "Visa", Info));
            ResultAssert.Failed(await env.CertificateHandler.SubmitAsync(student.Id, CertificateType.GoodStanding, "Bank", Info), ServiceRequestErrors.AlreadyPending);
            ResultAssert.Succeeded(await env.CertificateHandler.SubmitAsync(student.Id, CertificateType.EnrollmentProof, "Bank", Info));
            Assert.Equal(2, env.Workflows.Started.Count);
        }

        [Fact]
        public async Task A_certificate_without_its_type_is_invalid()
        {
            var env = new StudentServicesTestEnv();
            var student = env.SeedStudent();
            env.AsStudent(student);

            ResultAssert.Failed(await env.CertificateHandler.SubmitAsync(student.Id, null, "Visa", Info), ServiceRequestErrors.Invalid);
            Assert.Empty(env.Certificates.Items);
        }

        [Fact]
        public async Task A_failed_charge_cancels_the_request_and_starts_no_workflow()
        {
            var env = new StudentServicesTestEnv();
            var student = env.SeedStudent();
            env.AsStudent(student);
            var failure = Error.NotFound("FEE_NOT_FOUND", "No fee.");
            env.Charges.Error = failure;

            ResultAssert.Failed(await env.CertificateHandler.SubmitAsync(student.Id, CertificateType.Graduation, "Job", Info), failure);

            Assert.Equal(ServiceRequestStatus.Cancelled, Assert.Single(env.Certificates.Items).Status);
            Assert.Empty(env.Workflows.Started);

            // The student can ask again once the fee exists: the cancelled one no longer blocks.
            env.Charges.Error = null;
            ResultAssert.Succeeded(await env.CertificateHandler.SubmitAsync(student.Id, CertificateType.Graduation, "Job", Info));
        }

        [Fact]
        public async Task A_failed_workflow_start_cancels_the_request()
        {
            var env = new StudentServicesTestEnv();
            var student = env.SeedStudent();
            env.AsStudent(student);
            var failure = Error.NotFound("WORKFLOW_DEFINITION_NOT_FOUND", "No definition.");
            env.Workflows.StartError = failure;

            ResultAssert.Failed(await env.ComplaintHandler.SubmitAsync(student.Id, ComplaintCategory.Facilities, "Lab", "The lab is closed", Info), failure);

            var request = Assert.Single(env.Complaints.Items);
            Assert.Equal(ServiceRequestStatus.Cancelled, request.Status);
            Assert.Null(request.WorkflowInstanceId);
        }

        [Fact]
        public async Task Complaints_and_appeals_cost_nothing_and_may_be_several()
        {
            var env = new StudentServicesTestEnv();
            var student = env.SeedStudent();
            env.AsStudent(student);

            ResultAssert.Succeeded(await env.ComplaintHandler.SubmitAsync(student.Id, ComplaintCategory.Academic, "A", "Details", Info));
            ResultAssert.Succeeded(await env.ComplaintHandler.SubmitAsync(student.Id, ComplaintCategory.Academic, "B", "Details", Info));
            ResultAssert.Succeeded(await env.AppealHandler.SubmitAsync(student.Id, AppealTarget.AcademicStanding, "Standing", "I was ill", Info));

            Assert.Empty(env.Charges.Requests);
            Assert.Equal(3, env.Workflows.Started.Count);
        }

        [Fact]
        public async Task A_transcript_request_is_charged_and_one_stays_open()
        {
            var env = new StudentServicesTestEnv();
            var student = env.SeedStudent();
            env.AsStudent(student);

            ResultAssert.Succeeded(await env.TranscriptHandler.SubmitAsync(student.Id, "Embassy", Info));
            ResultAssert.Failed(await env.TranscriptHandler.SubmitAsync(student.Id, "Again", Info), ServiceRequestErrors.AlreadyPending);

            Assert.Equal(ServiceRequestKinds.TranscriptFeeCode, Assert.Single(env.Charges.Requests).FeeCode);
        }

        [Fact]
        public async Task A_student_cannot_submit_for_someone_else()
        {
            var env = new StudentServicesTestEnv();
            var owner = env.SeedStudent();
            var other = env.SeedStudent();
            env.AsStudent(other);

            var result = await env.ComplaintHandler.SubmitAsync(owner.Id, ComplaintCategory.Other, "Subject", "Details", Info);

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorType.Forbidden, result.Error?.ErrorType);
            Assert.Empty(env.Complaints.Items);
        }

        [Fact]
        public async Task Someone_elses_request_is_not_found_and_so_is_one_outside_the_scope()
        {
            var env = new StudentServicesTestEnv();
            var owner = env.SeedStudent();
            var other = env.SeedStudent();
            env.AsStudent(owner);
            var created = ResultAssert.Succeeded(await env.ComplaintHandler.SubmitAsync(owner.Id, ComplaintCategory.Other, "Subject", "Details", Info));

            ResultAssert.Succeeded(await env.ComplaintHandler.GetAsync(created.Id));

            env.AsStudent(other);
            ResultAssert.Failed(await env.ComplaintHandler.GetAsync(created.Id), ServiceRequestErrors.NotFound);
            ResultAssert.Failed(await env.ComplaintHandler.CancelAsync(created.Id, Info), ServiceRequestErrors.NotFound);

            env.AsHead();
            ResultAssert.Succeeded(await env.ComplaintHandler.GetAsync(created.Id));

            env.AsHead(Guid.NewGuid());
            ResultAssert.Failed(await env.ComplaintHandler.GetAsync(created.Id), ServiceRequestErrors.NotFound);

            ResultAssert.Failed(await env.ComplaintHandler.GetAsync(Guid.NewGuid()), ServiceRequestErrors.NotFound);
        }

        [Fact]
        public async Task The_staff_list_needs_a_university_wide_scope()
        {
            var env = new StudentServicesTestEnv();
            var student = env.SeedStudent();
            env.AsStudent(student);
            ResultAssert.Succeeded(await env.ComplaintHandler.SubmitAsync(student.Id, ComplaintCategory.Other, "Subject", "Details", Info));

            Assert.True((await env.ComplaintHandler.ListAsync(new PaginationQuery(1, 10), null)).IsFailure);

            env.AsRegistrar();
            var page = ResultAssert.Succeeded(await env.ComplaintHandler.ListAsync(new PaginationQuery(1, 10), null));
            Assert.Single(page.Items);
        }

        [Fact]
        public async Task Cancel_stops_the_workflow_and_the_request()
        {
            var env = new StudentServicesTestEnv();
            var student = env.SeedStudent();
            env.AsStudent(student);
            var created = ResultAssert.Succeeded(await env.ComplaintHandler.SubmitAsync(student.Id, ComplaintCategory.Other, "Subject", "Details", Info));

            var cancelled = ResultAssert.Succeeded(await env.ComplaintHandler.CancelAsync(created.Id, Info));

            Assert.Equal(ServiceRequestStatus.Cancelled, cancelled.Status);
            Assert.Equal(env.Workflows.LastInstanceId, Assert.Single(env.Workflows.Cancelled));
            ResultAssert.Failed(await env.ComplaintHandler.CancelAsync(created.Id, Info), ServiceRequestErrors.InvalidState);
        }

        [Fact]
        public async Task A_workflow_that_refuses_to_cancel_leaves_the_request_open()
        {
            var env = new StudentServicesTestEnv();
            var student = env.SeedStudent();
            env.AsStudent(student);
            var created = ResultAssert.Succeeded(await env.ComplaintHandler.SubmitAsync(student.Id, ComplaintCategory.Other, "Subject", "Details", Info));
            var refusal = Error.BusinessRule("WORKFLOW_FINISHED", "Finished.");
            env.Workflows.CancelError = refusal;

            ResultAssert.Failed(await env.ComplaintHandler.CancelAsync(created.Id, Info), refusal);

            Assert.Equal(ServiceRequestStatus.Pending, Assert.Single(env.Complaints.Items).Status);
        }

        [Fact]
        public async Task Resubmit_restarts_the_workflow_only_when_it_is_still_returned()
        {
            var env = new StudentServicesTestEnv();
            var student = env.SeedStudent();
            env.AsStudent(student);
            var created = ResultAssert.Succeeded(await env.ComplaintHandler.SubmitAsync(student.Id, ComplaintCategory.Other, "Subject", "Details", Info));
            ResultAssert.Failed(await env.ComplaintHandler.ResubmitAsync(created.Id, Info), ServiceRequestErrors.InvalidState);

            env.Clock.Advance(TimeSpan.FromHours(1));
            var request = Assert.Single(env.Complaints.Items);
            ResultAssert.Succeeded(request.Return(Guid.NewGuid(), "Add details", env.Clock.UtcNow));
            env.Workflows.Status = WorkflowInstanceStatus.Returned;

            env.Clock.Advance(TimeSpan.FromHours(1));
            var resubmitted = ResultAssert.Succeeded(await env.ComplaintHandler.ResubmitAsync(created.Id, Info));

            Assert.Equal(ServiceRequestStatus.Pending, resubmitted.Status);
            Assert.Single(env.Workflows.Resubmitted);

            // A second attempt after only the local save had failed: the workflow is already running, so it is not asked again.
            ResultAssert.Succeeded(request.Return(Guid.NewGuid(), "Again", env.Clock.UtcNow));
            env.Workflows.Status = WorkflowInstanceStatus.InProgress;
            ResultAssert.Succeeded(await env.ComplaintHandler.ResubmitAsync(created.Id, Info));
            Assert.Single(env.Workflows.Resubmitted);
        }
    }

    public class ServiceRequestReactionTests
    {
        private static async Task<(StudentServicesTestEnv Env, Guid RequestId, Guid InstanceId, Guid StudentId, Guid UserId)> ComplaintAsync()
        {
            var env = new StudentServicesTestEnv();
            var student = env.SeedStudent();
            env.AsStudent(student);
            var created = ResultAssert.Succeeded(await env.ComplaintHandler.SubmitAsync(student.Id, ComplaintCategory.Other, "Subject", "Details", StudentServicesTestEnv.Request));

            return (env, created.Id, env.Workflows.LastInstanceId, student.Id, student.UserId!.Value);
        }

        private static WorkflowCompleted Completed(StudentServicesTestEnv env, Guid instance, Guid reference, Guid user, string type)
            => new(Guid.NewGuid(), env.Clock.UtcNow.AddMinutes(5), instance, "DEF", type, reference, user, null);

        private static WorkflowReturned Returned(StudentServicesTestEnv env, Guid instance, Guid reference, Guid user, DateTimeOffset at)
            => new(Guid.NewGuid(), at, instance, "DEF", ServiceRequestKinds.ComplaintReference, reference, user, null, Guid.NewGuid(), "Add the dates");

        [Fact]
        public async Task Completed_approves_the_request_and_a_second_delivery_changes_nothing()
        {
            var (env, requestId, instance, _, user) = await ComplaintAsync();
            var e = Completed(env, instance, requestId, user, ServiceRequestKinds.ComplaintReference);

            ResultAssert.Succeeded(await env.Reactions.OnCompletedAsync(e, default));
            ResultAssert.Succeeded(await env.Reactions.OnCompletedAsync(e, default));

            var request = Assert.Single(env.Complaints.Items);
            Assert.Equal(ServiceRequestStatus.Approved, request.Status);
            Assert.Single(env.Rbac.AuditOf(ServiceRequestAuditActions.Approved));
        }

        [Fact]
        public async Task An_event_of_another_kind_is_ignored()
        {
            var (env, requestId, instance, _, user) = await ComplaintAsync();

            ResultAssert.Succeeded(await env.Reactions.OnCompletedAsync(Completed(env, instance, requestId, user, "GradeChangeRequest"), default));
            ResultAssert.Succeeded(await env.Reactions.OnCompletedAsync(Completed(env, instance, Guid.NewGuid(), user, ServiceRequestKinds.ComplaintReference), default));

            Assert.Equal(ServiceRequestStatus.Pending, Assert.Single(env.Complaints.Items).Status);
        }

        [Fact]
        public async Task An_event_of_another_workflow_instance_is_ignored()
        {
            var (env, requestId, _, _, user) = await ComplaintAsync();

            ResultAssert.Succeeded(await env.Reactions.OnCompletedAsync(Completed(env, Guid.NewGuid(), requestId, user, ServiceRequestKinds.ComplaintReference), default));

            Assert.Equal(ServiceRequestStatus.Pending, Assert.Single(env.Complaints.Items).Status);
        }

        [Fact]
        public async Task Rejected_and_returned_keep_the_reason_of_the_approver()
        {
            var (env, requestId, instance, _, user) = await ComplaintAsync();
            var approver = Guid.NewGuid();
            env.Clock.Advance(TimeSpan.FromMinutes(10));

            var returned = new WorkflowReturned(Guid.NewGuid(), env.Clock.UtcNow, instance, "DEF", ServiceRequestKinds.ComplaintReference, requestId, user, null, approver, "Add the dates");
            ResultAssert.Succeeded(await env.Reactions.OnReturnedAsync(returned, default));

            var request = Assert.Single(env.Complaints.Items);
            Assert.Equal(ServiceRequestStatus.Returned, request.Status);
            Assert.Equal("Add the dates", request.DecisionNote);
            Assert.Equal(approver, request.DecidedByUserId);

            // A rejection after a return (the request is no longer pending) is ignored.
            var rejected = new WorkflowRejected(Guid.NewGuid(), env.Clock.UtcNow.AddMinutes(1), instance, "DEF", ServiceRequestKinds.ComplaintReference, requestId, user, null, approver, "No");
            ResultAssert.Succeeded(await env.Reactions.OnRejectedAsync(rejected, default));
            Assert.Equal(ServiceRequestStatus.Returned, request.Status);
        }

        [Fact]
        public async Task A_late_copy_of_an_old_returned_event_does_not_return_a_resubmitted_request()
        {
            var (env, requestId, instance, _, user) = await ComplaintAsync();
            env.Clock.Advance(TimeSpan.FromHours(1));
            var old = Returned(env, instance, requestId, user, env.Clock.UtcNow);
            ResultAssert.Succeeded(await env.Reactions.OnReturnedAsync(old, default));
            env.Workflows.Status = WorkflowInstanceStatus.Returned;

            env.Clock.Advance(TimeSpan.FromHours(1));
            ResultAssert.Succeeded(await env.ComplaintHandler.ResubmitAsync(requestId, StudentServicesTestEnv.Request));

            ResultAssert.Succeeded(await env.Reactions.OnReturnedAsync(old, default));

            Assert.Equal(ServiceRequestStatus.Pending, Assert.Single(env.Complaints.Items).Status);
        }

        [Fact]
        public async Task Cancelled_by_the_workflow_cancels_the_request_once()
        {
            var (env, requestId, instance, _, user) = await ComplaintAsync();
            var admin = Guid.NewGuid();
            var e = new WorkflowCancelled(Guid.NewGuid(), env.Clock.UtcNow.AddMinutes(5), instance, "DEF", ServiceRequestKinds.ComplaintReference, requestId, user, null, admin);

            ResultAssert.Succeeded(await env.Reactions.OnCancelledAsync(e, default));
            ResultAssert.Succeeded(await env.Reactions.OnCancelledAsync(e, default));

            var request = Assert.Single(env.Complaints.Items);
            Assert.Equal(ServiceRequestStatus.Cancelled, request.Status);
            Assert.Equal(admin, request.DecidedByUserId);
        }

        [Fact]
        public async Task An_approved_transcript_issues_one_official_snapshot()
        {
            var env = new StudentServicesTestEnv();
            var student = env.SeedStudent();
            env.AsStudent(student);
            var created = ResultAssert.Succeeded(await env.TranscriptHandler.SubmitAsync(student.Id, "Embassy", StudentServicesTestEnv.Request));
            var e = Completed(env, env.Workflows.LastInstanceId, created.Id, student.UserId!.Value, ServiceRequestKinds.TranscriptReference);

            ResultAssert.Succeeded(await env.Reactions.OnCompletedAsync(e, default));
            ResultAssert.Succeeded(await env.Reactions.OnCompletedAsync(e, default));

            var request = Assert.Single(env.Transcripts.Items);
            Assert.Equal(ServiceRequestStatus.Approved, request.Status);
            Assert.True(request.IsIssued);
            Assert.Equal(student.Id, Assert.Single(env.Snapshots.IssuedFor));
            Assert.Single(env.Rbac.AuditOf(ServiceRequestAuditActions.TranscriptIssued));
        }

        [Fact]
        public async Task A_blocked_issue_keeps_the_approval_and_the_retry_issues_it()
        {
            var env = new StudentServicesTestEnv();
            var student = env.SeedStudent();
            env.AsStudent(student);
            var created = ResultAssert.Succeeded(await env.TranscriptHandler.SubmitAsync(student.Id, "Embassy", StudentServicesTestEnv.Request));
            var e = Completed(env, env.Workflows.LastInstanceId, created.Id, student.UserId!.Value, ServiceRequestKinds.TranscriptReference);
            var hold = Error.BusinessRule("FINANCIAL_HOLD", "A financial hold blocks it.");
            env.Snapshots.Error = hold;

            ResultAssert.Failed(await env.Reactions.OnCompletedAsync(e, default), hold);

            var request = Assert.Single(env.Transcripts.Items);
            Assert.Equal(ServiceRequestStatus.Approved, request.Status);
            Assert.False(request.IsIssued);

            env.Snapshots.Error = null;
            ResultAssert.Succeeded(await env.Reactions.OnCompletedAsync(e, default));

            Assert.True(request.IsIssued);
            Assert.Single(env.Snapshots.IssuedFor);
        }
    }
}
