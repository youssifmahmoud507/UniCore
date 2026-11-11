namespace UniCore.Application.Modules.Reporting
{
    public sealed record ReportPage(int Skip, int Take)
    {
        public static ReportPage First(int take) => new(0, take);
    }




}
