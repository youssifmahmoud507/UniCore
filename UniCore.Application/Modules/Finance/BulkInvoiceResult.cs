namespace UniCore.Application.Modules.Finance
{
    // Skipped: the student had no pending charge left when its turn came (someone invoiced it meanwhile).
    public sealed record BulkInvoiceResult(int StudentsInvoiced, int StudentsSkipped, int StudentsFailed);

}
