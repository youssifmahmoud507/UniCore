namespace UniCore.Domain.Modules.Courses
{
    // Draft -> Active -> Retired. Only a Draft can be edited; an Active or Retired version is history that
    // offerings, enrollments, grades and transcripts point at (academic-lifecycle.md section 7). Stored as text.
    public enum CourseVersionStatus
    {
        Draft,
        Active,
        Retired
    }
}
