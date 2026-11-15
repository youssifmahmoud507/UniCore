using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Transcripts
{
    public static class TranscriptErrors
    {
        public static readonly Error StudentNotFound = Error.NotFound("TRANSCRIPT_STUDENT_NOT_FOUND", "The student was not found.");
        public static readonly Error SnapshotNotFound = Error.NotFound("TRANSCRIPT_NOT_FOUND", "The transcript was not found.");
        public static readonly Error SnapshotInvalid = Error.Validation("TRANSCRIPT_INVALID", "An official transcript needs a student, a student number, an issuer, a verification code and at least one entry.");
        public static readonly Error ActorRequired = Error.Forbidden("TRANSCRIPT_ACTOR_REQUIRED", "An official transcript must be issued by a signed-in user.");
        public static readonly Error NoGrades = Error.BusinessRule("TRANSCRIPT_NO_GRADES", "The student has no published grades yet.");
        public static readonly Error SemesterNotFound = Error.NotFound("TRANSCRIPT_SEMESTER_NOT_FOUND", "A semester of the student's grades was not found.");
        public static readonly Error CourseNotFound = Error.NotFound("TRANSCRIPT_COURSE_NOT_FOUND", "A course of the student's grades was not found.");
    }
}
