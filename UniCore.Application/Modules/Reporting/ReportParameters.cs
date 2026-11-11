namespace UniCore.Application.Modules.Reporting
{
    // The parameter definitions several reports share.
    public static class ReportParameters
    {
        public static ReportParameter From(string label = "From date", bool required = false)
            => new("from", label, ReportParameterType.Date, required, Description: "yyyy-MM-dd, included");

        public static ReportParameter To(string label = "To date", bool required = false)
            => new("to", label, ReportParameterType.Date, required, Description: "yyyy-MM-dd, included");
    }




}
