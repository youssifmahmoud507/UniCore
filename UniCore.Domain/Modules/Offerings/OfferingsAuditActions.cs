namespace UniCore.Domain.Modules.Offerings
{
    // Audit action names of the Offerings module (kept apart from AuditActions so the two developers
    // do not edit the same file).
    public static class OfferingsAuditActions
    {
        public const string OfferingCreated = "COURSE_OFFERING_CREATED";
        public const string OfferingUpdated = "COURSE_OFFERING_UPDATED";
        public const string OfferingOpened = "COURSE_OFFERING_OPENED";
        public const string OfferingClosed = "COURSE_OFFERING_CLOSED";
        public const string OfferingCancelled = "COURSE_OFFERING_CANCELLED";

        public const string SectionCreated = "COURSE_SECTION_CREATED";
        public const string SectionUpdated = "COURSE_SECTION_UPDATED";
        public const string SectionClosed = "COURSE_SECTION_CLOSED";
        public const string SectionReopened = "COURSE_SECTION_REOPENED";
        public const string SectionCancelled = "COURSE_SECTION_CANCELLED";

        public const string InstructorAssigned = "INSTRUCTOR_ASSIGNED";
        public const string InstructorAssignmentEnded = "INSTRUCTOR_ASSIGNMENT_ENDED";
    }
}
