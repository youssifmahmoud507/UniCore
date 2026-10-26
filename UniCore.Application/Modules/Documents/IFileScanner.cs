using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Documents
{
    // A hook for an antivirus. The local default accepts everything. Return a failure to reject a file.
    public interface IFileScanner
    {
        Task<Result> ScanAsync(byte[] content, string fileName, CancellationToken cancellationToken = default);
    }


}
