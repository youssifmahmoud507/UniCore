namespace UniCore.Domain.Modules.Offerings
{
    // The part an instructor plays in a section. A section has at most one active Primary. Stored as text.
    public enum AssignmentRole
    {
        Primary,
        CoInstructor,
        TeachingAssistant
    }
}
