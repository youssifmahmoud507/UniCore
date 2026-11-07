using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Calendar;
using UniCore.Application.Modules.Courses;
using UniCore.Application.Modules.Curriculum;
using UniCore.Application.Modules.Finance;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Offerings;
using UniCore.Application.Modules.Registrations;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Courses;
using UniCore.Domain.Modules.Curriculum;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Offerings;
using UniCore.Domain.Modules.Registrations;
using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.University;
using UniCore.UnitTests.Common;
using UniCore.UnitTests.Modules.Admissions;
using UniCore.UnitTests.Modules.Identity;
using UniCore.UnitTests.Modules.Offerings;
using UniCore.UnitTests.Modules.People;
using UniCore.UnitTests.Modules.University;

namespace UniCore.UnitTests.Modules.Registrations
{
    internal sealed class FakeFinance : IFinancialEligibilityService
    {
        public bool Hold { get; set; }

        public Task<Result<bool>> HasBlockingHoldAsync(Guid studentId, CancellationToken cancellationToken = default) => Task.FromResult(Result<bool>.Ok(Hold));

        public Task<Result<bool>> HasBlockingHoldAsync(Guid studentId, FinancialHoldArea area, CancellationToken cancellationToken = default) => Task.FromResult(Result<bool>.Ok(Hold));

        public Task<Result<IReadOnlyList<FinancialHoldDto>>> GetActiveHoldsAsync(Guid studentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<FinancialHoldDto>>.Ok([]));

        public Task<Result<FinancialBalance>> GetBalanceAsync(Guid studentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<FinancialBalance>.Fail(CommonErrors.DatabaseUpdateFailed));
    }

    // The study plan answered from a set of course version ids.
    internal sealed class FakePlans : IStudyPlanReadService
    {
        public HashSet<Guid> CourseVersions { get; } = [];

        public Task<Result<StudyPlanVersionInfo>> GetVersionAsync(Guid versionId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<StudyPlanVersionInfo>.Fail(CommonErrors.DatabaseUpdateFailed));

        public Task<Result<StudyPlanVersionInfo>> GetEffectiveVersionAsync(Guid studyPlanId, DateOnly date, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<StudyPlanVersionInfo>.Fail(CommonErrors.DatabaseUpdateFailed));

        public Task<Result<bool>> ContainsCourseAsync(Guid versionId, Guid courseVersionId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<bool>.Ok(CourseVersions.Contains(courseVersionId)));

        public Task<Result<IReadOnlyList<PlanCourseInfo>>> GetCoursesAsync(Guid versionId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<PlanCourseInfo>>.Ok([.. CourseVersions.Select(v => new PlanCourseInfo(v, CourseRequirementType.Required, null, null, null))]));

        public Task<Result> VerifyVersionForProgramAsync(Guid versionId, Guid academicProgramId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Ok());
    }

    internal sealed class FakeOfferingRead(FakeAggregateRepository<CourseSection> sections) : IOfferingReadService
    {
        public sealed record Meta(Guid OfferingId, Guid CourseVersionId, Guid CourseId, Guid SemesterId, int Credits, bool HasPrimary);

        public Dictionary<Guid, Meta> Sections { get; } = [];

        public Task<Result<OfferingInfo>> GetOfferingAsync(Guid offeringId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<OfferingInfo>.Fail(OfferingsErrors.OfferingNotFound));

        public Task<Result<SectionInfo>> GetSectionAsync(Guid sectionId, CancellationToken cancellationToken = default)
        {
            var section = sections.Items.FirstOrDefault(s => s.Id == sectionId);
            if (section is null || !Sections.TryGetValue(sectionId, out var meta))
            {
                return Task.FromResult<Result<SectionInfo>>(OfferingsErrors.SectionNotFound);
            }

            return Task.FromResult<Result<SectionInfo>>(new SectionInfo(
                section.Id, meta.OfferingId, meta.CourseVersionId, meta.CourseId, meta.SemesterId, section.SectionCode, section.Capacity,
                section.Status, CourseOfferingStatus.Open, meta.Credits, section.IsOpen));
        }

        public Task<Result<IReadOnlyList<SectionInstructorInfo>>> GetActiveInstructorsAsync(Guid sectionId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<SectionInstructorInfo> list = Sections.TryGetValue(sectionId, out var meta) && meta.HasPrimary
                ? [new SectionInstructorInfo(Guid.NewGuid(), AssignmentRole.Primary, DateTimeOffset.UtcNow)]
                : [];

            return Task.FromResult(Result<IReadOnlyList<SectionInstructorInfo>>.Ok(list));
        }
    }

    internal sealed class FakePrerequisites : IPrerequisiteEvaluator
    {
        public HashSet<Guid> Unmet { get; } = [];
        public Dictionary<Guid, List<Guid>> Corequisites { get; } = [];

        public Task<Result<bool>> ArePrerequisitesMetAsync(Guid studentId, Guid courseVersionId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<bool>.Ok(!Unmet.Contains(courseVersionId)));

        public Task<Result<IReadOnlyList<Guid>>> GetCorequisiteCourseIdsAsync(Guid courseVersionId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<Guid>>.Ok(Corequisites.TryGetValue(courseVersionId, out var ids) ? [.. ids] : []));
    }

    internal sealed class FakeHistory : IStudentCourseHistory
    {
        public Dictionary<Guid, CourseAttemptSummary> Attempts { get; } = [];

        public Task<Result<CourseAttemptSummary>> GetAttemptsAsync(Guid studentId, Guid courseId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<CourseAttemptSummary>.Ok(Attempts.TryGetValue(courseId, out var summary) ? summary : new CourseAttemptSummary(false, null, 0)));
    }

    internal sealed class FakeSchedule : IScheduleConflictService
    {
        public bool Conflict { get; set; }

        public Task<Result<bool>> HasConflictAsync(Guid studentId, Guid semesterId, Guid courseSectionId, IReadOnlyCollection<Guid> enrolledSectionIds, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<bool>.Ok(Conflict));
    }

    internal sealed class FakeSettings : IRegistrationSettings
    {
        public int RegularCreditLimit { get; set; } = 18;
        public int ProbationCreditLimit { get; set; } = 12;
        public int SummerCreditLimit { get; set; } = 9;
        public string SummerTermCode { get; set; } = "SUMMER";
        public bool AllowGradeImprovement { get; set; } = true;
        public decimal ImprovementMaxGradePoint { get; set; } = 2.0m;
    }

    // The real registration handler, pipeline and calendar service and the real PermissionAuthorizer, wired to fakes.
    // Fall 2026: registration 1-10 Sep, add/drop 5-20 Sep, withdrawal until 15 Nov.
    internal sealed class RegistrationsTestEnv
    {
        public static readonly DateTimeOffset Start = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);
        public static readonly RequestInfo Request = new("127.0.0.1", "tests");
        public const string Key = "key-0000-0001";

        public FakeClock Clock { get; } = new(Start);
        public FakeAcademicClock AcademicClock { get; } = new() { Today = new DateOnly(2026, 9, 2) };
        public RbacTestEnv Rbac { get; } = new();

        public FakeAggregateRepository<Registration> Registrations { get; } = new();
        public FakeAggregateRepository<CourseSection> Sections { get; } = new();
        public FakeAggregateRepository<Student> Students { get; } = new();
        public FakeEntityRepository<AcademicSemester> Semesters { get; } = new();
        public FakeEntityRepository<AcademicTerm> Terms { get; } = new();
        public FakeEntityRepository<AcademicYear> Years { get; } = new();
        public FakeEntityRepository<AcademicProgram> Programs { get; } = new();

        public FakeFinance Finance { get; } = new();
        public FakePlans Plans { get; } = new();
        public FakeCourseCatalog Catalog { get; } = new();
        public FakeOfferingRead Offerings { get; }
        public FakePrerequisites Prerequisites { get; } = new();
        public FakeHistory History { get; } = new();
        public FakeSchedule Schedule { get; } = new();
        public FakeSettings Settings { get; } = new();

        public AcademicCalendarService Calendar { get; }
        public RegistrationPipeline Pipeline { get; }
        public RegistrationAccess Access { get; }
        public RegistrationHandler Handler { get; }
        public RegistrationCompletionService Completion { get; }
        public EnrollmentReadService Reads { get; }

        public AcademicSemester Semester { get; private set; } = null!;
        public Guid PlanVersionId { get; } = Guid.NewGuid();

        public RegistrationsTestEnv()
        {
            Offerings = new FakeOfferingRead(Sections);
            Calendar = new AcademicCalendarService(Years, Semesters, AcademicClock);
            Access = new RegistrationAccess(Rbac.Authorizer, Students, Programs);
            Pipeline = new RegistrationPipeline(Offerings, Calendar, Finance, Plans, Catalog, Prerequisites, History, Schedule, Settings, Terms);
            Handler = new RegistrationHandler(Rbac.Authorizer, Rbac.User, Access, Pipeline, Registrations, Sections, Students, Semesters, Calendar, Prerequisites, Rbac.Uow, Clock, Rbac.Audit);
            Completion = new RegistrationCompletionService(Semesters, Registrations, Rbac.Uow, Clock);
            Reads = new EnrollmentReadService(Registrations);

            Semester = SeedSemester();
        }

        // ---------- who is calling ----------

        // The student signs in: Registration.Read + Register, and no scope except what ownership gives.
        public void AsStudent(Student student)
            => Rbac.SignIn(student.UserId!.Value, TestGrants.Role(RoleNames.Student, [Permissions.Registration.Read, Permissions.Registration.Register], new ScopeGrant(ScopeType.Department, Guid.NewGuid())));

        // Another student: same permissions, not the owner.
        public void AsStranger()
            => Rbac.SignIn(Guid.NewGuid(), TestGrants.Role(RoleNames.Student, [Permissions.Registration.Read, Permissions.Registration.Register], new ScopeGrant(ScopeType.Department, Guid.NewGuid())));

        public void AsRegistrar()
            => Rbac.SignIn(Guid.NewGuid(), TestGrants.Role(RoleNames.Registrar, [Permissions.Registration.Read, Permissions.Registration.Manage], new ScopeGrant(ScopeType.Global, null)));

        // ---------- seed data ----------

        public AcademicSemester SeedSemester(string termCode = "FALL", bool closed = false)
        {
            var term = ResultAssert.Succeeded(AcademicTerm.Create(termCode, termCode, 1));
            Terms.Items.Add(term);

            var windows = new SemesterWindows(
                RegistrationStart: new(2026, 9, 1), RegistrationEnd: new(2026, 9, 10),
                AddDropStart: new(2026, 9, 5), AddDropEnd: new(2026, 9, 20),
                WithdrawalDeadline: new(2026, 11, 15),
                MidtermStart: new(2026, 11, 1), MidtermEnd: new(2026, 11, 10),
                FinalExamStart: new(2027, 1, 5), FinalExamEnd: new(2027, 1, 20),
                GradeSubmissionDeadline: new(2027, 1, 28), GradePublicationDate: new(2027, 2, 3));

            var semester = ResultAssert.Succeeded(AcademicSemester.Create(Guid.NewGuid(), term.Id, new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 31), windows, new DateOnly(2026, 9, 1), new DateOnly(2027, 8, 31), Clock.UtcNow));

            if (closed)
            {
                foreach (var status in new[] { SemesterStatus.RegistrationOpen, SemesterStatus.InProgress, SemesterStatus.Examinations, SemesterStatus.GradingOpen, SemesterStatus.Closed })
                {
                    ResultAssert.Succeeded(semester.AdvanceTo(status, Clock.UtcNow));
                }
            }

            Semesters.Items.Add(semester);
            return semester;
        }

        public Student SeedStudent(AcademicStanding? standing = null)
        {
            var student = ResultAssert.Succeeded(Student.Create(Guid.NewGuid(), "2026CS" + Random.Shared.Next(1000, 9999), new DateOnly(2026, 8, 1), null, Guid.NewGuid(), Guid.NewGuid(), PlanVersionId, null, Clock.UtcNow));
            ResultAssert.Succeeded(student.LinkAccount(Guid.NewGuid(), Clock.UtcNow));

            if (standing is { } value)
            {
                ResultAssert.Succeeded(student.UpdateAcademicProfile(null, value, Clock.UtcNow));
            }

            Students.Items.Add(student);
            return student;
        }

        public void Suspend(Student student)
            => ResultAssert.Succeeded(student.ChangeStatus(StudentAcademicStatus.Suspended, new DateOnly(2026, 9, 2), "test", Guid.NewGuid(), new DateOnly(2026, 9, 2), Clock.UtcNow));

        public sealed record SeededSection(Guid SectionId, Guid CourseId, Guid CourseVersionId, int Credits);

        // A section of a new course. inPlan=false leaves the course out of the study plan.
        public SeededSection SeedSection(string code = "CS101", int credits = 3, int capacity = 30, bool inPlan = true, bool hasPrimary = true, AcademicSemester? semester = null, Guid? courseId = null)
        {
            var targetSemester = semester ?? Semester;
            var course = courseId ?? Guid.NewGuid();
            var version = new CourseVersionInfo(Guid.NewGuid(), course, code, Guid.NewGuid(), 1, "Title " + code, credits, 2, 2, CourseVersionStatus.Active, new DateOnly(2026, 1, 1), null);
            Catalog.Versions[version.CourseVersionId] = version;

            if (inPlan)
            {
                Plans.CourseVersions.Add(version.CourseVersionId);
            }

            var section = ResultAssert.Succeeded(CourseSection.Create(Guid.NewGuid(), "A", capacity, Clock.UtcNow));
            Sections.Items.Add(section);
            Offerings.Sections[section.Id] = new FakeOfferingRead.Meta(section.CourseOfferingId, version.CourseVersionId, course, targetSemester.Id, credits, hasPrimary);

            return new SeededSection(section.Id, course, version.CourseVersionId, credits);
        }

        public CourseSection SectionOf(SeededSection seeded) => Sections.Items.First(s => s.Id == seeded.SectionId);

        // ---------- calls ----------

        public Task<Result<RegisterOutcome>> RegisterAsync(Student student, SeededSection section, string key = Key, string? overrideReason = null, AcademicSemester? semester = null)
            => Handler.RegisterAsync(new RegisterCommand(student.Id, (semester ?? Semester).Id, section.SectionId, overrideReason), key, Request);

        // Registers as the student and returns the outcome (fails the test when it does not work).
        public async Task<RegisterOutcome> RegisterOkAsync(Student student, SeededSection section, string? key = null)
        {
            AsStudent(student);
            var result = await RegisterAsync(student, section, key ?? "key-" + Guid.NewGuid().ToString("N"));
            Assert.True(result.IsSuccess, result.Error?.Code);
            return result.Value!;
        }
    }
}
