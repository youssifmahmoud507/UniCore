using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Documents
{
    // Where the bytes live. The first implementation is a folder on the server disk, outside any public folder.
    // Keys are made by the system (32 hex characters): a user never chooses a path.
    public interface IFileStorage
    {
        Task<Result> SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default);

        Task<Result<Stream>> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);

        Task<Result> DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
    }


}
