namespace UniCore.Domain.Modules.Curriculum
{
    // How a course counts toward graduation (graduation-rules.md): Required courses must be passed,
    // the other types are satisfied by credits.
    public enum CourseRequirementType
    {
        Required,
        Elective,
        FreeElective,
        GeneralEducation
    }
}
