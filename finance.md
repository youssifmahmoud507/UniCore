# Finance

Status: Draft v0.1. Owner: Developer 2. Values marked (proposed) need confirmation. There is no real payment gateway: payments are recorded by staff or simulated.

## 1. Flow

`FeeDefinition` -> `FeeStructure` -> `FeeStructureItem` -> `StudentCharge` -> `Invoice` (with `InvoiceLine`) -> `Payment` -> balance -> `FinancialHold`.
Reductions: `Scholarship` / `StudentScholarship`, `Discount`. Reversals: `Refund`.

## 2. Money rules

- `decimal(18,2)` everywhere, a `Money` value object. No `double` or `float`.
- Rounding: `MidpointRounding.AwayFromZero` to 2 decimals (proposed), applied per invoice line, then summed.
- One currency per university (a system setting). No currency conversion.
- Amounts are never negative except documented adjustments. Zero-amount payments are rejected.

## 3. Charges and invoices

- `FeeDefinition` is the catalogue (tuition, lab fee...). `FeeStructure` groups items per program and/or semester and has a status (draft, active, retired). Items hold the amount for that structure.
- Charge generation per semester and program creates one `StudentCharge` per student and structure item. It is idempotent: unique on (student, structure item), so rerunning creates nothing new.
- `Invoice`: `InvoiceNumber` (unique, `INV-<year>-<6 digit sequence>`, from a database sequence to be concurrency safe), `IssueDate`, `DueDate`, `Subtotal`, `DiscountAmount`, `TotalAmount`, `PaidAmount`, `Balance`, `Status`.
- Invariants: `TotalAmount = Subtotal - DiscountAmount`, `Balance = TotalAmount - PaidAmount - (refunded payments are added back)`, line amounts = quantity x unit price.
- `Invoice` is the aggregate root for its lines. Issued invoices are immutable except through status transitions, payments and credit notes.

### Invoice status machine

`Draft` -> `Issued` -> `PartiallyPaid` -> `Paid`.
`Issued` / `PartiallyPaid` -> `Overdue` (set by a job when past due with balance above zero), and back to `PartiallyPaid` or `Paid` when paid.
`Draft` / `Issued` (with no payments) -> `Cancelled`.
A `Paid` invoice reopens to `PartiallyPaid` if a refund returns the balance above zero.
Invalid transitions return `INVOICE_INVALID_STATE`.

## 4. Discounts and scholarships (proposed order)

1. Start from the invoice subtotal.
2. Apply scholarships (percentage first, then fixed amounts).
3. Apply discounts (percentage first, then fixed amounts).
4. Total reductions can never exceed the subtotal; the total floors at zero.

Each reduction has a validity window. Percentages are computed on the amount remaining at that step. The applied breakdown is stored on the invoice.

## 5. Payments

- `Payment`: `InvoiceId`, `Amount`, `PaymentMethod`, `Status`, `Reference?`, `PaidAt?`, `RecordedByUserId`.
- Partial payments are allowed. A payment cannot exceed the current balance (`PAYMENT_EXCEEDS_BALANCE`).
- Overpayment policy (proposed): rejected by default. A credit-on-account policy would need an extra entity (student credit) and a spec change, so it is out of scope until requested.
- Recording a payment requires the `Idempotency-Key` header. It updates `PaidAmount`, `Balance` and the invoice status atomically in one transaction with `RowVersion` on `Invoice` and `Payment`. A concurrent update returns `CONCURRENCY_CONFLICT`.
- Payment status: `Recorded`, `Voided`, `PartiallyRefunded`, `Refunded` (proposed). Voiding is allowed only same day by a user with `Payments.Record` plus approval (proposed) and is audited.

## 6. Refunds

- `Refund`: `PaymentId`, `Amount`, `Reason`, `Status`, `RequestedAt`, `ProcessedAt?`. Status: `Requested`, `Approved`, `Processed`, `Rejected`.
- Limits: total refunds for a payment cannot exceed the payment amount (`REFUND_EXCEEDS_PAYMENT`). A reason is mandatory.
- Approval by `Refunds.Approve`; requester and approver cannot be the same user.
- Processing recalculates the invoice balance and status and is audited.

## 7. Financial holds

- `FinancialHold`: `StudentId`, `Reason`, `Amount?`, `Status` (Active, Released), `CreatedAt`, `ReleasedAt?`.
- Created automatically by the `FinancialHoldEvaluation` job when a student has overdue balance above a threshold for more than N days (thresholds in `SystemSetting`, proposed: 0.00 and 14 days), or manually by `Holds.Manage`.
- Released automatically when the qualifying balance is cleared, or manually with a reason. Never delete holds; release them.
- What a hold blocks is configured per area in `SystemSetting`: registration, graduation, student services (proposed: all three).
- Contract `IFinancialEligibilityService`:
  - `HasBlockingHold(studentId, area)`: is there an active hold blocking that area.
  - `GetBalance(studentId)`: total outstanding balance.
  Developer 1 calls it from Registration, Graduation and Services. Tested end to end: hold, blocked, released, allowed.

## 8. Concurrency and idempotency

- `RowVersion` on `Invoice` and `Payment`. Balance changes are atomic updates inside a transaction.
- `Idempotency-Key` on payment recording: replay returns the stored response, same key with a different payload returns `409`.
- Unique indexes: `InvoiceNumber`, (student, structure item) on charges, (key, user) on idempotency records.

## 9. Error codes

`INVOICE_NOT_FOUND`, `INVOICE_INVALID_STATE`, `INVOICE_ALREADY_PAID`, `PAYMENT_EXCEEDS_BALANCE`, `PAYMENT_AMOUNT_INVALID`, `PAYMENT_NOT_FOUND`, `REFUND_EXCEEDS_PAYMENT`, `REFUND_NOT_ALLOWED`, `REFUND_SELF_APPROVAL`, `FEE_STRUCTURE_NOT_ACTIVE`, `CHARGE_ALREADY_GENERATED`, `FINANCIAL_HOLD` (blocking, used by Registration), `HOLD_NOT_FOUND`, `HOLD_ALREADY_RELEASED`, `DISCOUNT_NOT_VALID`, `SCHOLARSHIP_NOT_VALID`, `CONCURRENCY_CONFLICT`.

## 10. Permissions

`Finance.Read`, `Invoices.Manage`, `Payments.Record`, `Refunds.Approve`, `Holds.Manage`. Students read only their own invoices, payments and balance.

## 11. Audit and events

Audit: payment recorded or voided, refund requested/approved/processed, invoice issued/cancelled, hold placed/released, fee structure changes.
Domain events: `InvoiceIssued`, `PaymentRecorded`, `InvoicePaid`, `RefundProcessed`, `FinancialHoldPlaced`, `FinancialHoldReleased`. Notifications go out through the Outbox.

## 12. Jobs

`FinancialHoldEvaluation` (create and release holds), overdue marking. Both are idempotent and return Results.

## 13. Tests

Rounding, discount order, balance recalculation, partial payment, overpayment rejection, refund limits, state machine, hold create/release, concurrent payments on one invoice, idempotent replay, charge generation rerun.