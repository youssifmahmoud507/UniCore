using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Calendar
{
    // The date windows of a semester. All dates are inclusive and compared as plain dates.
    // Registration, add/drop, midterm and final-exam windows are given as both ends or not at all.
    public sealed record SemesterWindows(
        DateOnly? RegistrationStart,
        DateOnly? RegistrationEnd,
        DateOnly? AddDropStart,
        DateOnly? AddDropEnd,
        DateOnly? WithdrawalDeadline,
        DateOnly? MidtermStart,
        DateOnly? MidtermEnd,
        DateOnly? FinalExamStart,
        DateOnly? FinalExamEnd,
        DateOnly? GradeSubmissionDeadline,
        DateOnly? GradePublicationDate)
    {
        public static readonly SemesterWindows None = new(null, null, null, null, null, null, null, null, null, null, null);

        public bool HasRegistrationAndAddDrop => RegistrationStart is not null && RegistrationEnd is not null && AddDropStart is not null && AddDropEnd is not null;

        // Required order (see academic-lifecycle.md section 3):
        //   StartDate <= RegistrationStart < RegistrationEnd
        //   RegistrationStart <= AddDropStart < AddDropEnd <= EndDate
        //   AddDropEnd < WithdrawalDeadline <= EndDate
        //   MidtermStart <= MidtermEnd, both inside the semester
        //   FinalExamStart <= FinalExamEnd, inside the semester and after MidtermEnd
        //   FinalExamEnd <= GradeSubmissionDeadline <= GradePublicationDate (grade dates may fall after EndDate)
        public Result Validate(DateOnly start, DateOnly end)
        {
            var bad = Result.Fail(CalendarErrors.SemesterInvalidDates);

            if (!PairOk(RegistrationStart, RegistrationEnd, strict: true)
                || !PairOk(AddDropStart, AddDropEnd, strict: true)
                || !PairOk(MidtermStart, MidtermEnd, strict: false)
                || !PairOk(FinalExamStart, FinalExamEnd, strict: false))
            {
                return bad;
            }

            if (RegistrationStart is { } rs && (rs < start || RegistrationEnd > end))
            {
                return bad;
            }

            if (AddDropStart is { } ads)
            {
                if (ads < start || AddDropEnd > end || (RegistrationStart is { } regStart && ads < regStart))
                {
                    return bad;
                }
            }

            if (WithdrawalDeadline is { } wd)
            {
                if (wd > end || wd < start || (AddDropEnd is { } ade && wd <= ade))
                {
                    return bad;
                }
            }

            if (MidtermStart is { } ms && (ms < start || MidtermEnd > end))
            {
                return bad;
            }

            if (FinalExamStart is { } fs)
            {
                if (fs < start || FinalExamEnd > end || (MidtermEnd is { } me && fs <= me))
                {
                    return bad;
                }
            }

            if (GradeSubmissionDeadline is { } gs)
            {
                if (gs < start || (FinalExamEnd is { } fe && gs < fe))
                {
                    return bad;
                }
            }

            if (GradePublicationDate is { } gp)
            {
                if (gp < start
                    || (GradeSubmissionDeadline is { } gsd && gp < gsd)
                    || (GradeSubmissionDeadline is null && FinalExamEnd is { } fex && gp < fex))
                {
                    return bad;
                }
            }

            return Result.Ok();
        }

        private static bool PairOk(DateOnly? from, DateOnly? to, bool strict)
        {
            if (from is null && to is null)
            {
                return true;
            }

            if (from is null || to is null)
            {
                return false;
            }

            return strict ? from < to : from <= to;
        }
    }
}
