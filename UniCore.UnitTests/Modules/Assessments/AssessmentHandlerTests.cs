using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Assessments;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Assessments;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Assessments
{
    public class AssessmentHandlerTests
    {
        private static readonly RequestInfo Request = AssessmentsTestEnv.Request;

        private static void AssertFailed<T>(Result<T> result, Error expected)
        {
            Assert.True(result.IsFailure, "Expected " + expected.Code + " but it succeeded.");
            Assert.Equal(expected.Code, result.Error?.Code);
        }

        // ---------- create ----------

        [Fact]
        public async Task Scheme_is_created_by_the_department_head_or_the_assigned_instructor_only()
        {
            var env = new AssessmentsTestEnv();
            var section = env.SeedSection();
            var command = new CreateAssessmentCommand(section.SectionId);

            env.AsHead(Guid.NewGuid());
            Assert.Equal(ErrorType.Forbidden, (await env.Handler.CreateAsync(command, Request)).Error?.ErrorType);

            env.AsInstructor(Guid.NewGuid());
            Assert.Equal(ErrorType.Forbidden, (await env.Handler.CreateAsync(command, Request)).Error?.ErrorType);

            env.AsRegistrar();
            Assert.Equal(ErrorType.Forbidden, (await env.Handler.CreateAsync(command, Request)).Error?.ErrorType);

            env.AsStudent();
            Assert.Equal(ErrorType.Forbidden, (await env.Handler.CreateAsync(command, Request)).Error?.ErrorType);

            Assert.Empty(env.Assessments.Items);

            env.AsInstructor(section.SectionId);
            Assert.True((await env.Handler.CreateAsync(command, Request)).IsSuccess);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == AssessmentAuditActions.Created);
        }

        [Fact]
        public async Task One_live_scheme_per_section_and_a_cancelled_one_frees_the_slot()
        {
            var env = new AssessmentsTestEnv();
            var section = env.SeedSection();
            var first = await env.CreateOkAsync(section);

            env.AsHead();
            AssertFailed(await env.Handler.CreateAsync(new CreateAssessmentCommand(section.SectionId), Request), AssessmentErrors.Exists);

            Assert.True((await env.Handler.CancelAsync(first.Id, Request)).IsSuccess);
            Assert.True((await env.Handler.CreateAsync(new CreateAssessmentCommand(section.SectionId), Request)).IsSuccess);
        }

        [Fact]
        public async Task Unknown_section_and_closed_semester_are_refused()
        {
            var env = new AssessmentsTestEnv();
            env.AsHead();

            AssertFailed(await env.Handler.CreateAsync(new CreateAssessmentCommand(Guid.NewGuid()), Request), AssessmentErrors.SectionNotFound);

            var closed = env.SeedSection(env.SeedSemester(closed: true));
            AssertFailed(await env.Handler.CreateAsync(new CreateAssessmentCommand(closed.SectionId), Request), AssessmentErrors.SemesterClosed);
        }

        // ---------- components ----------

        [Fact]
        public async Task Component_due_date_must_be_inside_the_semester()
        {
            var env = new AssessmentsTestEnv();
            var scheme = await env.CreateOkAsync(env.SeedSection());

            env.AsHead();
            AssertFailed(await env.Handler.AddComponentAsync(scheme.Id, AssessmentsTestEnv.Quiz(due: new DateOnly(2026, 8, 31)), Request), AssessmentErrors.DueDateOutsideSemester);
            AssertFailed(await env.Handler.AddComponentAsync(scheme.Id, AssessmentsTestEnv.Quiz(due: new DateOnly(2027, 2, 1)), Request), AssessmentErrors.DueDateOutsideSemester);

            var added = await env.Handler.AddComponentAsync(scheme.Id, AssessmentsTestEnv.Quiz(due: new DateOnly(2026, 9, 1)), Request);
            Assert.True(added.IsSuccess, added.Error?.Code);
        }

        [Fact]
        public async Task Midterm_and_final_must_be_due_inside_their_exam_window()
        {
            var env = new AssessmentsTestEnv();
            var scheme = await env.CreateOkAsync(env.SeedSection());

            env.AsHead();
            AssertFailed(await env.Handler.AddComponentAsync(scheme.Id, new ComponentCommand("Midterm", ComponentType.Midterm, 30, 50, new DateOnly(2026, 10, 15)), Request), AssessmentErrors.DueDateOutsideWindow);
            AssertFailed(await env.Handler.AddComponentAsync(scheme.Id, new ComponentCommand("Final", ComponentType.Final, 50, 100, new DateOnly(2027, 1, 25)), Request), AssessmentErrors.DueDateOutsideWindow);

            Assert.True((await env.Handler.AddComponentAsync(scheme.Id, new ComponentCommand("Midterm", ComponentType.Midterm, 30, 50, new DateOnly(2026, 11, 10)), Request)).IsSuccess);
        }

        [Fact]
        public async Task Add_update_and_remove_are_audited_with_old_and_new_values()
        {
            var env = new AssessmentsTestEnv();
            var scheme = await env.CreateOkAsync(env.SeedSection());

            var added = await env.AddOkAsync(scheme, AssessmentsTestEnv.Quiz("Quiz 1", 10));
            var componentId = added.Components.Single().Id;

            var updated = await env.Handler.UpdateComponentAsync(scheme.Id, componentId, AssessmentsTestEnv.Quiz("Quiz 1", 15), Request);
            Assert.True(updated.IsSuccess, updated.Error?.Code);
            Assert.Equal(15, updated.Value!.TotalWeight);

            var removed = await env.Handler.RemoveComponentAsync(scheme.Id, componentId, Request);
            Assert.True(removed.IsSuccess, removed.Error?.Code);
            Assert.Empty(removed.Value!.Components);

            var actions = env.Rbac.Audit.Entries.Select(e => e.Action).ToList();
            Assert.Contains(AssessmentAuditActions.ComponentAdded, actions);
            Assert.Contains(AssessmentAuditActions.ComponentUpdated, actions);
            Assert.Contains(AssessmentAuditActions.ComponentRemoved, actions);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == AssessmentAuditActions.ComponentUpdated && e.OldValues!.Contains("TotalWeight=10") && e.NewValues!.Contains("TotalWeight=15"));
        }

        [Fact]
        public async Task A_failed_change_saves_and_audits_nothing()
        {
            var env = new AssessmentsTestEnv();
            var scheme = await env.CreateOkAsync(env.SeedSection());
            var saves = env.Rbac.Uow.SaveCalls;
            var audits = env.Rbac.Audit.Entries.Count;

            env.AsHead();
            AssertFailed(await env.Handler.AddComponentAsync(scheme.Id, AssessmentsTestEnv.Quiz(weight: 101), Request), AssessmentErrors.ComponentInvalid);

            Assert.Equal(saves, env.Rbac.Uow.SaveCalls);
            Assert.Equal(audits, env.Rbac.Audit.Entries.Count);
        }

        // ---------- lifecycle ----------

        [Fact]
        public async Task Publish_needs_weights_of_100_then_locks_the_scheme()
        {
            var env = new AssessmentsTestEnv();
            var scheme = await env.CreateOkAsync(env.SeedSection());
            await env.AddOkAsync(scheme, AssessmentsTestEnv.Quiz("Quiz", 40));

            env.AsHead();
            AssertFailed(await env.Handler.PublishAsync(scheme.Id, Request), AssessmentErrors.WeightNotHundred);

            await env.AddOkAsync(scheme, new ComponentCommand("Final", ComponentType.Final, 60, 100, new DateOnly(2027, 1, 10)));
            env.AsHead();
            var published = await env.Handler.PublishAsync(scheme.Id, Request);

            Assert.True(published.IsSuccess, published.Error?.Code);
            Assert.Equal(AssessmentStatus.Published, published.Value!.Status);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == AssessmentAuditActions.Published);
            Assert.Contains(env.Assessments.Items.Single().DomainEvents, e => e is AssessmentPublished);

            AssertFailed(await env.Handler.AddComponentAsync(scheme.Id, AssessmentsTestEnv.Quiz("Extra", 1), Request), AssessmentErrors.InvalidState);
            AssertFailed(await env.Handler.PublishAsync(scheme.Id, Request), AssessmentErrors.InvalidState);
        }

        [Fact]
        public async Task Close_and_cancel_follow_the_lifecycle()
        {
            var env = new AssessmentsTestEnv();
            var scheme = await env.FillToHundredAsync(await env.CreateOkAsync(env.SeedSection()));

            env.AsHead();
            AssertFailed(await env.Handler.CloseAsync(scheme.Id, Request), AssessmentErrors.InvalidState);
            Assert.True((await env.Handler.PublishAsync(scheme.Id, Request)).IsSuccess);
            Assert.True((await env.Handler.CloseAsync(scheme.Id, Request)).IsSuccess);
            AssertFailed(await env.Handler.CancelAsync(scheme.Id, Request), AssessmentErrors.InvalidState);

            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == AssessmentAuditActions.Closed);
        }

        [Fact]
        public async Task A_closed_semester_blocks_changes()
        {
            var env = new AssessmentsTestEnv();
            var scheme = await env.CreateOkAsync(env.SeedSection());

            foreach (var status in new[] { UniCore.Domain.Modules.Calendar.SemesterStatus.RegistrationOpen, UniCore.Domain.Modules.Calendar.SemesterStatus.InProgress, UniCore.Domain.Modules.Calendar.SemesterStatus.Examinations, UniCore.Domain.Modules.Calendar.SemesterStatus.GradingOpen, UniCore.Domain.Modules.Calendar.SemesterStatus.Closed })
            {
                ResultAssert.Succeeded(env.Semester.AdvanceTo(status, env.Clock.UtcNow));
            }

            env.AsHead();
            AssertFailed(await env.Handler.AddComponentAsync(scheme.Id, AssessmentsTestEnv.Quiz(), Request), AssessmentErrors.SemesterClosed);
        }

        [Fact]
        public async Task Another_department_cannot_change_the_scheme()
        {
            var env = new AssessmentsTestEnv();
            var scheme = await env.CreateOkAsync(env.SeedSection());

            env.AsHead(Guid.NewGuid());
            Assert.Equal(ErrorType.Forbidden, (await env.Handler.AddComponentAsync(scheme.Id, AssessmentsTestEnv.Quiz(), Request)).Error?.ErrorType);
            Assert.Equal(ErrorType.Forbidden, (await env.Handler.PublishAsync(scheme.Id, Request)).Error?.ErrorType);
        }

        // ---------- read ----------

        [Fact]
        public async Task An_instructor_reads_only_the_schemes_of_their_own_sections()
        {
            var env = new AssessmentsTestEnv();
            var mine = env.SeedSection();
            var theirs = env.SeedSection();
            var myScheme = await env.CreateOkAsync(mine);
            var otherScheme = await env.CreateOkAsync(theirs);

            env.AsInstructor(mine.SectionId);
            Assert.True((await env.Handler.GetAsync(myScheme.Id)).IsSuccess);
            Assert.Equal(ErrorType.Forbidden, (await env.Handler.GetAsync(otherScheme.Id)).Error?.ErrorType);
            Assert.True((await env.Handler.ListAsync(new(1, 10), mine.SectionId, null)).IsSuccess);
            Assert.Equal(ErrorType.Forbidden, (await env.Handler.ListAsync(new(1, 10), theirs.SectionId, null)).Error?.ErrorType);
            Assert.True((await env.Handler.ListAsync(new(1, 10), null, null)).IsFailure);
        }

        [Fact]
        public async Task The_registrar_reads_a_scheme_but_a_student_cannot()
        {
            var env = new AssessmentsTestEnv();
            var scheme = await env.CreateOkAsync(env.SeedSection());

            env.AsRegistrar();
            Assert.True((await env.Handler.GetAsync(scheme.Id)).IsSuccess);

            env.AsStudent();
            Assert.Equal(ErrorType.Forbidden, (await env.Handler.GetAsync(scheme.Id)).Error?.ErrorType);
        }

        [Fact]
        public async Task A_missing_scheme_is_not_found()
        {
            var env = new AssessmentsTestEnv();
            env.AsHead();

            AssertFailed(await env.Handler.GetAsync(Guid.NewGuid()), AssessmentErrors.NotFound);
            AssertFailed(await env.Handler.PublishAsync(Guid.NewGuid(), Request), AssessmentErrors.NotFound);
        }

        // ---------- the contract for Grading (Week 14) ----------

        [Fact]
        public async Task Read_service_returns_only_published_or_closed_schemes()
        {
            var env = new AssessmentsTestEnv();
            var section = env.SeedSection();
            var scheme = await env.FillToHundredAsync(await env.CreateOkAsync(section));

            AssertFailed(await env.Reader.GetSectionAssessmentAsync(section.SectionId), AssessmentErrors.NotFound);

            env.AsHead();
            Assert.True((await env.Handler.PublishAsync(scheme.Id, Request)).IsSuccess);
            var published = await env.Reader.GetSectionAssessmentAsync(section.SectionId);
            Assert.True(published.IsSuccess, published.Error?.Code);
            Assert.Equal(3, published.Value!.Components.Count);
            Assert.Equal(100m, published.Value.Components.Sum(c => c.Weight));

            Assert.True((await env.Handler.CloseAsync(scheme.Id, Request)).IsSuccess);
            Assert.Equal(AssessmentStatus.Closed, (await env.Reader.GetSectionAssessmentAsync(section.SectionId)).Value!.Status);
        }

        [Fact]
        public async Task Percentage_is_the_weighted_sum_and_rejects_impossible_scores()
        {
            var env = new AssessmentsTestEnv();
            var section = env.SeedSection();
            var scheme = await env.FillToHundredAsync(await env.CreateOkAsync(section));
            env.AsHead();
            await env.Handler.PublishAsync(scheme.Id, Request);

            var info = (await env.Reader.GetSectionAssessmentAsync(section.SectionId)).Value!;
            var quiz = info.Components.Single(c => c.Type == ComponentType.Quiz);
            var midterm = info.Components.Single(c => c.Type == ComponentType.Midterm);
            var final = info.Components.Single(c => c.Type == ComponentType.Final);

            // Quiz 10/20 of 20% = 10; midterm 40/50 of 30% = 24; final 90/100 of 50% = 45 -> 79.
            var scores = new Dictionary<Guid, decimal> { [quiz.ComponentId] = 10, [midterm.ComponentId] = 40, [final.ComponentId] = 90 };
            Assert.Equal(79m, env.Reader.CalculatePercentage(info, scores).Value);

            // A missing component counts as 0.
            Assert.Equal(45m, env.Reader.CalculatePercentage(info, new Dictionary<Guid, decimal> { [final.ComponentId] = 90 }).Value);

            AssertFailed(env.Reader.CalculatePercentage(info, new Dictionary<Guid, decimal> { [quiz.ComponentId] = 21 }), AssessmentErrors.ScoreInvalid);
            AssertFailed(env.Reader.CalculatePercentage(info, new Dictionary<Guid, decimal> { [quiz.ComponentId] = -1 }), AssessmentErrors.ScoreInvalid);
            AssertFailed(env.Reader.CalculatePercentage(info, new Dictionary<Guid, decimal> { [Guid.NewGuid()] = 5 }), AssessmentErrors.ScoreInvalid);
        }
    }
}
