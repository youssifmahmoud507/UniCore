namespace UniCore.Domain.Modules.Transcripts
{
    // What happens when a student takes the same course more than once. Only ONE attempt of a course counts in the GPA and the credits;
    // the others stay on the transcript, marked as not counted.
    public enum RepeatPolicy
    {
        // The most recent attempt replaces the earlier ones (the default).
        LatestAttempt,

        // The attempt with the most grade points counts (the latest wins a tie).
        HighestGrade
    }
}
