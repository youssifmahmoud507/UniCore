namespace UniCore.Domain.Modules.Curriculum
{
    // Draft -> Active -> Retired. Only a Draft can be edited; Active and Retired versions are history that
    // students are graded and graduated under (academic-lifecycle.md section 117). Stored as text.
    public enum StudyPlanVersionStatus
    {
        Draft,
        Active,
        Retired
    }
}
