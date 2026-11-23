using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Grading
{
    public static class GradeChangeErrors
    {
        public static readonly Error NotFound = Error.NotFound("GRADE_CHANGE_NOT_FOUND", "The grade change request was not found.");
        public static readonly Error DataInvalid = Error.Validation("GRADE_CHANGE_DATA_INVALID", "A grade change needs a grade, one of its components, a valid new score and a reason.");
        public static readonly Error GradeNotFinal = Error.BusinessRule("GRADE_CHANGE_GRADE_NOT_FINAL", "Only a published or locked grade needs a change request; a grade still in the workflow is changed by entering scores.");
        public static readonly Error NoChange = Error.BusinessRule("GRADE_CHANGE_NO_CHANGE", "The new score is the same as the current score.");
        public static readonly Error AlreadyPending = Error.Conflict("GRADE_CHANGE_ALREADY_PENDING", "This grade already has a pending change request.");
        public static readonly Error InvalidState = Error.BusinessRule("GRADE_CHANGE_INVALID_STATE", "The grade change request is not pending.");
    }
}
