using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Finance
{
    public static class FinanceErrors
    {
        // ----- money -----
        public static readonly Error AmountRequired = Error.Validation("FINANCE_AMOUNT_REQUIRED", "An amount is required.");
        public static readonly Error AmountOutOfRange = Error.Validation("FINANCE_AMOUNT_OUT_OF_RANGE", "The amount is outside the allowed range.");
        public static readonly Error AmountPrecisionInvalid = Error.Validation("FINANCE_AMOUNT_PRECISION_INVALID", "An amount can have at most 2 decimal places.");
        public static readonly Error AmountMustBePositive = Error.Validation("FINANCE_AMOUNT_MUST_BE_POSITIVE", "The amount must be greater than zero.");
        public static readonly Error PercentInvalid = Error.Validation("FINANCE_PERCENT_INVALID", "A percentage must be between 0 and 100.");

        // ----- fee definition -----
        public static readonly Error FeeDefinitionNotFound = Error.NotFound("FEE_DEFINITION_NOT_FOUND", "The fee definition was not found.");
        public static readonly Error FeeDefinitionDataInvalid = Error.Validation("FEE_DEFINITION_DATA_INVALID", "The fee definition data is invalid.");
        public static readonly Error FeeCodeInvalid = Error.Validation("FEE_CODE_INVALID", "The fee code must be 2 to 30 characters: letters A-Z, digits and underscore.");
        public static readonly Error FeeCodeAlreadyExists = Error.Conflict("FEE_CODE_ALREADY_EXISTS", "A fee with this code already exists for this university.");
        public static readonly Error FeeDefinitionInactive = Error.BusinessRule("FEE_DEFINITION_INACTIVE", "The fee definition is not active.");

        // ----- fee structure -----
        public static readonly Error FeeStructureNotFound = Error.NotFound("FEE_STRUCTURE_NOT_FOUND", "The fee structure was not found.");
        public static readonly Error FeeStructureDataInvalid = Error.Validation("FEE_STRUCTURE_DATA_INVALID", "The fee structure data is invalid.");
        public static readonly Error FeeStructureNotDraft = Error.BusinessRule("FEE_STRUCTURE_NOT_DRAFT", "Only a draft fee structure can be changed.");
        public static readonly Error FeeStructureNotActive = Error.BusinessRule("FEE_STRUCTURE_NOT_ACTIVE", "Only an active fee structure can be archived.");
        public static readonly Error FeeStructureEmpty = Error.BusinessRule("FEE_STRUCTURE_EMPTY", "A fee structure needs at least one item before it can be activated.");
        public static readonly Error FeeStructureTooManyItems = Error.BusinessRule("FEE_STRUCTURE_TOO_MANY_ITEMS", "A fee structure cannot have that many items.");
        public static readonly Error FeeStructureScopeTaken = Error.Conflict("FEE_STRUCTURE_SCOPE_TAKEN", "Another active fee structure already covers this program and semester.");
        public static readonly Error FeeItemNotFound = Error.NotFound("FEE_ITEM_NOT_FOUND", "The fee structure item was not found.");
        public static readonly Error FeeItemDuplicate = Error.Conflict("FEE_ITEM_DUPLICATE", "This fee is already in the fee structure.");


        // ----- student charge -----
        public static readonly Error ChargeNotFound = Error.NotFound("STUDENT_CHARGE_NOT_FOUND", "The student charge was not found.");
        public static readonly Error ChargeDataInvalid = Error.Validation("STUDENT_CHARGE_DATA_INVALID", "The student charge data is invalid.");
        public static readonly Error ChargeNotPending = Error.BusinessRule("STUDENT_CHARGE_NOT_PENDING", "Only a pending charge can be invoiced or cancelled.");
        public static readonly Error ChargeDueDateInvalid = Error.Validation("CHARGE_DUE_DATE_INVALID", "The due date cannot be in the past.");
        public static readonly Error ChargeGenerationConflict = Error.Conflict("CHARGE_GENERATION_CONFLICT", "Another charge generation ran at the same time. Run it again; it is safe to repeat.");

        // ----- invoice -----
        public static readonly Error InvoiceNotFound = Error.NotFound("INVOICE_NOT_FOUND", "The invoice was not found.");
        public static readonly Error InvoiceDataInvalid = Error.Validation("INVOICE_DATA_INVALID", "The invoice data is invalid.");
        public static readonly Error InvoiceTooManyLines = Error.BusinessRule("INVOICE_TOO_MANY_LINES", "An invoice cannot have that many lines.");
        public static readonly Error NoPendingCharges = Error.BusinessRule("NO_PENDING_CHARGES", "The student has no pending charges to invoice.");
        public static readonly Error InvoiceNumberGenerationFailed = Error.Failure("INVOICE_NUMBER_GENERATION_FAILED", "An invoice number could not be generated.");

        // ----- payments and cancellation -----
        public static readonly Error InvoiceCancelled = Error.BusinessRule("INVOICE_CANCELLED", "The invoice is cancelled.");
        public static readonly Error InvoiceAlreadyPaid = Error.BusinessRule("INVOICE_ALREADY_PAID", "The invoice is already fully paid.");
        public static readonly Error InvoiceHasPayments = Error.BusinessRule("INVOICE_HAS_PAYMENTS", "Only an invoice with no payments can be cancelled.");
        public static readonly Error PaymentExceedsBalance = Error.BusinessRule("PAYMENT_EXCEEDS_BALANCE", "The payment is more than the invoice balance.");
        public static readonly Error PaymentDateInvalid = Error.Validation("PAYMENT_DATE_INVALID", "The payment date cannot be in the future or before the invoice was issued.");
        public static readonly Error PaymentDataInvalid = Error.Validation("PAYMENT_DATA_INVALID", "The payment data is invalid.");
        public static readonly Error PaymentReferenceRequired = Error.Validation("PAYMENT_REFERENCE_REQUIRED", "A bank transfer or a cheque needs a reference.");
        public static readonly Error IdempotencyKeyInvalid = Error.Validation("IDEMPOTENCY_KEY_INVALID", "The Idempotency-Key header is required: 8 to 100 visible characters, no spaces.");
        public static readonly Error IdempotencyKeyReused = Error.Conflict("IDEMPOTENCY_KEY_REUSED", "This Idempotency-Key was already used for a different payment.");
        public static readonly Error ChargeNotInvoiced = Error.BusinessRule("STUDENT_CHARGE_NOT_INVOICED", "The charge is not on this invoice.");
    }


}
