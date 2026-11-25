using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Finance;
using UniCore.Application.Modules.Grading;
using UniCore.Application.Modules.StudentServices;
using UniCore.Application.Modules.Transcripts;
using UniCore.Application.Modules.Workflow;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Grading;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.StudentServices;
using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.Transcripts;
using UniCore.Domain.Modules.University;
using UniCore.Domain.Modules.Workflow;
using UniCore.UnitTests.Common;
using UniCore.UnitTests.Modules.Admissions;
using UniCore.UnitTests.Modules.Identity;
using UniCore.UnitTests.Modules.People;
using UniCore.UnitTests.Modules.Registrations;
using UniCore.UnitTests.Modules.University;

namespace UniCore.UnitTests.Modules.StudentServices
{
    // The workflow engine (Developer 2) answered in memory: it records what was asked and can be told to fail.
    internal sealed class FakeWorkflowService(IClock clock) : IWorkflowService
    {
        public List<StartWorkflowCommand> Started { get; } = [];
        public List<Guid> Cancelled { get; } = [];
        public List<Guid> Resubmitted { get; } = [];
        public Error? StartError { get; set; }
        public Error? CancelError { get; set; }
        public WorkflowInstanceStatus Status { get; set; } = WorkflowInstanceStatus.InProgress;
        public Guid LastInstanceId { get; private set; }

        public Task<Result<WorkflowInstanceDto>> StartAsync(StartWorkflowCommand command, RequestInfo? request = null, CancellationToken cancellationToken = default)
        {
            if (StartError is { } error)
            {
                return Task.FromResult(Result<WorkflowInstanceDto>.Fail(error));
            }

            Started.Add(command);
            LastInstanceId = Guid.NewGuid();

            return Task.FromResult(Result<WorkflowInstanceDto>.Ok(Dto(LastInstanceId, command.ReferenceType ?? string.Empty, command.ReferenceId ?? Guid.Empty)));
        }

        public Task<Result<WorkflowInstanceDto>> StartAuthorizedAsync(StartWorkflowCommand command, RequestInfo? request = null, CancellationToken cancellationToken = default)
            => StartAsync(command, request, cancellationToken);

        public Task<Result<WorkflowInstanceDto>> CancelAsync(Guid instanceId, string? comment, RequestInfo? request = null, CancellationToken cancellationToken = default)
        {
            if (CancelError is { } error)
            {
                return Task.FromResult(Result<WorkflowInstanceDto>.Fail(error));
            }

            Cancelled.Add(instanceId);

            return Task.FromResult(Result<WorkflowInstanceDto>.Ok(Dto(instanceId, string.Empty, Guid.Empty)));
        }

        public Task<Result<WorkflowInstanceDto>> ResubmitAsync(Guid instanceId, string? comment, RequestInfo? request = null, CancellationToken cancellationToken = default)
        {
            Resubmitted.Add(instanceId);
            Status = WorkflowInstanceStatus.InProgress;

            return Task.FromResult(Result<WorkflowInstanceDto>.Ok(Dto(instanceId, string.Empty, Guid.Empty)));
        }

        public Task<Result<WorkflowInstanceDto>> GetAsync(Guid instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<WorkflowInstanceDto>.Ok(Dto(instanceId, string.Empty, Guid.Empty)));

        public Task<Result<WorkflowInstanceDto>> ApproveAsync(Guid instanceId, string? comment, RequestInfo? request = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<WorkflowInstanceDto>.Fail(CommonErrors.DatabaseUpdateFailed));

        public Task<Result<WorkflowInstanceDto>> RejectAsync(Guid instanceId, string? comment, RequestInfo? request = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<WorkflowInstanceDto>.Fail(CommonErrors.DatabaseUpdateFailed));

        public Task<Result<WorkflowInstanceDto>> ReturnAsync(Guid instanceId, string? comment, RequestInfo? request = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<WorkflowInstanceDto>.Fail(CommonErrors.DatabaseUpdateFailed));

        private WorkflowInstanceDto Dto(Guid id, string referenceType, Guid referenceId)
            => new(id, Guid.NewGuid(), "DEF", 1, Guid.NewGuid(), null, null, null, referenceType, referenceId, Status, 1, "Step 1", "Registrar", 1, clock.UtcNow, null, [], []);
    }

    // IGradeReadService answered from a list the test fills.
    internal sealed class FakeGradeRead : IGradeReadService
    {
        public List<GradeRecordInfo> Published { get; } = [];

        public Task<Result<IReadOnlyList<GradeRecordInfo>>> GetPublishedGradesAsync(Guid studentId, Guid? semesterId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<GradeRecordInfo>>.Ok([.. Published]));

        public void Publish(Guid gradeId)
            => Published.Add(new GradeRecordInfo(gradeId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3, 70m, "C-", 1.7m, true, GradeStatus.Published, DateTimeOffset.UtcNow));
    }

    // IStudentChargeService: idempotent by (student, reference) like the real one; can be told to fail.
    internal sealed class FakeCharges : IStudentChargeService
    {
        public List<ServiceChargeRequest> Requests { get; } = [];
        public Error? Error { get; set; }

        public Task<Result<ServiceChargeResult>> ChargeAsync(ServiceChargeRequest request, CancellationToken cancellationToken = default)
        {
            if (Error is { } error)
            {
                return Task.FromResult(Result<ServiceChargeResult>.Fail(error));
            }

            Requests.Add(request);
            var charge = new StudentChargeDto(Guid.NewGuid(), request.StudentId, null, Guid.NewGuid(), request.FeeCode ?? string.Empty, 50m, new DateOnly(2027, 7, 20), ChargeStatus.Pending, null, DateTimeOffset.UtcNow, request.Reference);

            return Task.FromResult(Result<ServiceChargeResult>.Ok(new ServiceChargeResult(charge, true)));
        }
    }

    // ITranscriptSnapshotService: issues a new snapshot id per call, or fails (a financial hold, for example).
    internal sealed class FakeSnapshots : ITranscriptSnapshotService
    {
        public List<Guid> IssuedFor { get; } = [];
        public Error? Error { get; set; }

        public Task<Result<TranscriptSummary>> IssueOfficialAsync(Guid studentId, Guid issuedByUserId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            if (Error is { } error)
            {
                return Task.FromResult(Result<TranscriptSummary>.Fail(error));
            }

            IssuedFor.Add(studentId);
            var summary = new TranscriptSummary(Guid.NewGuid(), "VERIFY-1", true, studentId, "2023CS0001", DateTimeOffset.UtcNow, RepeatPolicy.LatestAttempt, new GpaFigures(3m, 6, 6, null), 0, []);

            return Task.FromResult(Result<TranscriptSummary>.Ok(summary));
        }
    }

    internal sealed class StudentServicesTestEnv
    {
        public static readonly RequestInfo Request = new("127.0.0.1", "tests");
        public static readonly Guid DepartmentId = Guid.NewGuid();
        public static readonly DateTimeOffset Start = new(2027, 6, 20, 9, 0, 0, TimeSpan.Zero);

        public FakeClock Clock { get; } = new(Start);
        public RbacTestEnv Rbac { get; } = new();
        public FakeWorkflowService Workflows { get; }
        public FakeCharges Charges { get; } = new();
        public FakeSnapshots Snapshots { get; } = new();
        public FakeAggregateRepository<Student> Students { get; } = new();
        public FakeEntityRepository<AcademicProgram> Programs { get; } = new();
        public FakeAggregateRepository<CertificateRequest> Certificates { get; } = new();
        public FakeAggregateRepository<TranscriptRequest> Transcripts { get; } = new();
        public FakeAggregateRepository<Complaint> Complaints { get; } = new();
        public FakeAggregateRepository<Appeal> Appeals { get; } = new();
        public FakeAggregateRepository<GradeAppeal> GradeAppeals { get; } = new();
        public FakeGradeRead GradeRead { get; } = new();
        public FakeAggregateRepository<WorkflowInstance> Instances { get; } = new();

        public CertificateRequestHandler CertificateHandler { get; }
        public TranscriptRequestHandler TranscriptHandler { get; }
        public ComplaintHandler ComplaintHandler { get; }
        public AppealHandler AppealHandler { get; }
        public GradeAppealHandler GradeAppealHandler { get; }
        public ServiceRequestWorkflowReactions Reactions { get; }

        public AcademicProgram Program { get; }

        public StudentServicesTestEnv()
        {
            Workflows = new FakeWorkflowService(Clock);
            Program = ResultAssert.Succeeded(AcademicProgram.Create(DepartmentId, "BSC-CS", "BSc Computer Science", DegreeType.Bachelor, 4, 6, Clock.UtcNow));
            Programs.Items.Add(Program);

            var access = new ServiceRequestAccess(Rbac.Authorizer, Students, Programs);
            CertificateHandler = new CertificateRequestHandler(access, Certificates, Workflows, Charges, Rbac.User, Rbac.Uow, Clock, Rbac.Audit);
            TranscriptHandler = new TranscriptRequestHandler(access, Transcripts, Workflows, Charges, Rbac.User, Rbac.Uow, Clock, Rbac.Audit);
            ComplaintHandler = new ComplaintHandler(access, Complaints, Workflows, Rbac.User, Rbac.Uow, Clock, Rbac.Audit);
            AppealHandler = new AppealHandler(access, Appeals, Workflows, Rbac.User, Rbac.Uow, Clock, Rbac.Audit);
            GradeAppealHandler = new GradeAppealHandler(access, GradeAppeals, Workflows, GradeRead, Rbac.User, Rbac.Uow, Clock, Rbac.Audit);
            Reactions = new ServiceRequestWorkflowReactions(Certificates, Transcripts, Complaints, Appeals, GradeAppeals, Instances, Snapshots, Rbac.Uow, Clock, Rbac.Audit);
        }

        public Student SeedStudent()
        {
            var student = ResultAssert.Succeeded(Student.Create(
                Guid.NewGuid(), "2023CS" + Random.Shared.Next(1000, 9999), new DateOnly(2023, 9, 1), null, Guid.NewGuid(), Program.Id, Guid.NewGuid(), null, Clock.UtcNow));
            ResultAssert.Succeeded(student.LinkAccount(Guid.NewGuid(), Clock.UtcNow));
            Students.Items.Add(student);

            return student;
        }

        public void AsUser(Guid userId, string role, string[] permissions, params ScopeGrant[] grants)
            => Rbac.SignIn(userId, TestGrants.Role(role, permissions, grants));

        public void AsStudent(Student student)
            => AsUser(student.UserId!.Value, RoleNames.Student, [Permissions.Requests.Submit, Permissions.Requests.Read], new ScopeGrant(ScopeType.Department, Guid.NewGuid()));

        public void AsRegistrar()
            => AsUser(Guid.NewGuid(), RoleNames.Registrar, [Permissions.Requests.Read, Permissions.Requests.Process], new ScopeGrant(ScopeType.Global, null));

        public void AsHead(Guid? departmentId = null)
            => AsUser(Guid.NewGuid(), RoleNames.DepartmentHead, [Permissions.Requests.Read, Permissions.Requests.Process], new ScopeGrant(ScopeType.Department, departmentId ?? DepartmentId));
    }
}
