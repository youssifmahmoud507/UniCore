using System;
using System.Collections.Generic;
using System.Text;

namespace UniCore.Application.Modules.Notifications
{
    // Every notification type the system sends. A producer adds its type here. Mandatory types ignore the user's preferences.
    public static class NotificationTypes
    {
        public const string WorkflowActionRequired = "WORKFLOW_ACTION_REQUIRED";
        public const string RequestStatusChanged = "REQUEST_STATUS_CHANGED";
        public const string PaymentReceived = "PAYMENT_RECEIVED";
        public const string FinancialHoldPlaced = "FINANCIAL_HOLD_PLACED";
        public const string FinancialHoldReleased = "FINANCIAL_HOLD_RELEASED";
        public const string AttendanceWarning = "ATTENDANCE_WARNING";
        public const string GradePublished = "GRADE_PUBLISHED";
        public const string GradeCorrected = "GRADE_CORRECTED";
        public const string AssessmentPublished = "ASSESSMENT_PUBLISHED";
        public const string AssessmentCancelled = "ASSESSMENT_CANCELLED";
        public const string ExamPublished = "EXAM_PUBLISHED";
        public const string ExamCancelled = "EXAM_CANCELLED";
        public const string EnrollmentConfirmed = "ENROLLMENT_CONFIRMED";
        public const string EnrollmentDropped = "ENROLLMENT_DROPPED";
        public const string TranscriptIssued = "TRANSCRIPT_ISSUED";
        public const string SchedulePublished = "SCHEDULE_PUBLISHED";

        public sealed record TypeInfo(string Code, string Name, bool Mandatory);

        public static readonly IReadOnlyList<TypeInfo> All = [new(WorkflowActionRequired, "A request is waiting for your approval", false), new(RequestStatusChanged, "The status of your request changed", false), new(PaymentReceived, "A payment was received", false), new(FinancialHoldPlaced, "A financial hold was placed on your account", true), new(FinancialHoldReleased, "A financial hold was released", false),
        new(AttendanceWarning, "Your attendance in a course fell to a warning level", true), new(GradePublished, "A grade was published", false), new(GradeCorrected, "A published grade was corrected", false),
        new(AssessmentPublished, "The grading scheme of a course was published", false), new(AssessmentCancelled, "The grading scheme of a course was cancelled", false),
        new(ExamPublished, "An exam was scheduled", false), new(ExamCancelled, "An exam was cancelled", false),
        new(EnrollmentConfirmed, "You were registered in a course", false), new(EnrollmentDropped, "You were removed from a course", false), new(TranscriptIssued, "An official transcript was issued", false),
        new(SchedulePublished, "The timetable of your courses was published", false)];

        public static TypeInfo? Find(string? code)
        {
            var clean = code?.Trim().ToUpperInvariant();

            return All.FirstOrDefault(t => t.Code == clean);
        }
    }


}
