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

        public sealed record TypeInfo(string Code, string Name, bool Mandatory);

        public static readonly IReadOnlyList<TypeInfo> All = [new(WorkflowActionRequired, "A request is waiting for your approval", false), new(RequestStatusChanged, "The status of your request changed", false), new(PaymentReceived, "A payment was received", false), new(FinancialHoldPlaced, "A financial hold was placed on your account", true), new(FinancialHoldReleased, "A financial hold was released", false)];

        public static TypeInfo? Find(string? code)
        {
            var clean = code?.Trim().ToUpperInvariant();

            return All.FirstOrDefault(t => t.Code == clean);
        }
    }


}
