namespace UniCore.Domain.Modules.Examination
{
    // The two exam periods of a semester (their date windows are on AcademicSemester). Stored as text.
    public enum ExamType
    {
        Midterm,
        Final
    }

    // Open -> Closed. Exams can only be created or changed in an Open session. Stored as text.
    public enum ExamSessionStatus
    {
        Open,
        Closed
    }

    // Draft -> Published -> Cancelled (Draft may also be cancelled). Stored as text.
    public enum ExamStatus
    {
        Draft,
        Published,
        Cancelled
    }

    // Chief: responsible for the room (one per room). Assistant: helps. Stored as text.
    public enum InvigilatorRole
    {
        Chief,
        Assistant
    }
}
