namespace UniCore.Infrastructure.Modules.Maintenance
{
    public sealed record ExpiredRequestsResult(int Cancelled, int Failed);

}
