using System;
using System.Collections.Generic;
using System.Text;

namespace UniCore.Domain.Modules.Audit
{
    public static class AuditActions
    {
        public const string AuthLoginSucceeded = "AUTH_LOGIN_SUCCEEDED";
        public const string AuthLoginFailed = "AUTH_LOGIN_FAILED";
        public const string AuthAccountLocked = "AUTH_ACCOUNT_LOCKED";
        public const string AuthLogout = "AUTH_LOGOUT";
        public const string AuthTokenRefreshed = "AUTH_TOKEN_REFRESHED";
        public const string AuthTokenReuseDetected = "AUTH_TOKEN_REUSE_DETECTED";
        public const string AuthPasswordResetRequested = "AUTH_PASSWORD_RESET_REQUESTED";
        public const string AuthPasswordResetThrottled = "AUTH_PASSWORD_RESET_THROTTLED";
        public const string AuthPasswordResetRequestFailed = "AUTH_PASSWORD_RESET_REQUEST_FAILED";
        public const string AuthOtpVerified = "AUTH_OTP_VERIFIED";
        public const string AuthOtpFailed = "AUTH_OTP_FAILED";
        public const string AuthOtpExpired = "AUTH_OTP_EXPIRED";
        public const string AuthOtpAttemptsExceeded = "AUTH_OTP_ATTEMPTS_EXCEEDED";
        public const string AuthPasswordResetCompleted = "AUTH_PASSWORD_RESET_COMPLETED";
        public const string EmailEnqueueFailed = "EMAIL_ENQUEUE_FAILED";
        public const string UserCreated = "USER_CREATED";
        public const string UserActivated = "USER_ACTIVATED";
        public const string UserDeactivated = "USER_DEACTIVATED";
        public const string RoleCreated = "ROLE_CREATED";
        public const string RoleDeleted = "ROLE_DELETED";
        public const string RoleAssigned = "ROLE_ASSIGNED";
        public const string RoleRemoved = "ROLE_REMOVED";
        public const string PermissionGranted = "PERMISSION_GRANTED";
        public const string PermissionRevoked = "PERMISSION_REVOKED";
        public const string ScopeAssigned = "SCOPE_ASSIGNED";
        public const string ScopeRemoved = "SCOPE_REMOVED";
        public const string StudentCreated = "STUDENT_CREATED";
        public const string StudentAdmitted = "STUDENT_ADMITTED";
        public const string StudentAccountCreated = "STUDENT_ACCOUNT_CREATED";
        public const string StudentAccountFailed = "STUDENT_ACCOUNT_PROVISION_FAILED";
        public const string StudentStatusChanged = "STUDENT_STATUS_CHANGED";
        public const string StudentProgramChanged = "STUDENT_PROGRAM_CHANGED";
        public const string StudentAcademicProfileUpdated = "STUDENT_ACADEMIC_PROFILE_UPDATED";

        public const string FeeDefinitionCreated = "FEE_DEFINITION_CREATED";
        public const string FeeDefinitionUpdated = "FEE_DEFINITION_UPDATED";
        public const string FeeDefinitionActivated = "FEE_DEFINITION_ACTIVATED";
        public const string FeeDefinitionDeactivated = "FEE_DEFINITION_DEACTIVATED";
        public const string FeeStructureCreated = "FEE_STRUCTURE_CREATED";
        public const string FeeStructureItemAdded = "FEE_STRUCTURE_ITEM_ADDED";
        public const string FeeStructureItemChanged = "FEE_STRUCTURE_ITEM_CHANGED";
        public const string FeeStructureItemRemoved = "FEE_STRUCTURE_ITEM_REMOVED";
        public const string FeeStructureActivated = "FEE_STRUCTURE_ACTIVATED";
        public const string FeeStructureArchived = "FEE_STRUCTURE_ARCHIVED";
        public const string ChargesGenerated = "CHARGES_GENERATED";
        public const string StudentChargeCancelled = "STUDENT_CHARGE_CANCELLED";
        public const string InvoiceIssued = "INVOICE_ISSUED";
        public const string PaymentRecorded = "PAYMENT_RECORDED";
        public const string InvoiceCancelled = "INVOICE_CANCELLED";

        public const string DiscountCreated = "DISCOUNT_CREATED";
        public const string DiscountUpdated = "DISCOUNT_UPDATED";
        public const string DiscountActivated = "DISCOUNT_ACTIVATED";
        public const string DiscountDeactivated = "DISCOUNT_DEACTIVATED";
        public const string ScholarshipCreated = "SCHOLARSHIP_CREATED";
        public const string ScholarshipUpdated = "SCHOLARSHIP_UPDATED";
        public const string ScholarshipActivated = "SCHOLARSHIP_ACTIVATED";
        public const string ScholarshipDeactivated = "SCHOLARSHIP_DEACTIVATED";
        public const string ScholarshipAwarded = "SCHOLARSHIP_AWARDED";
        public const string ScholarshipRevoked = "SCHOLARSHIP_REVOKED";
        public const string RefundRequested = "REFUND_REQUESTED";
        public const string RefundApproved = "REFUND_APPROVED";
        public const string RefundRejected = "REFUND_REJECTED";
        public const string RefundProcessed = "REFUND_PROCESSED";
        public const string FinancialHoldPlaced = "FINANCIAL_HOLD_PLACED";
        public const string FinancialHoldReleased = "FINANCIAL_HOLD_RELEASED";
    }



}
