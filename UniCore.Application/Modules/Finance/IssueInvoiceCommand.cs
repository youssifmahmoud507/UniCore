namespace UniCore.Application.Modules.Finance
{

    // Invoices every pending charge of the student (of one semester when AcademicSemesterId is set).
    // DueDate = null means the latest due date of the charges (or today if that is already past).
    // DiscountIds: discounts to apply (at most 5). The student's active scholarships apply by themselves.
    public sealed record IssueInvoiceCommand(Guid? StudentId, Guid? AcademicSemesterId, DateOnly? DueDate, IReadOnlyList<Guid>? DiscountIds = null);


}
