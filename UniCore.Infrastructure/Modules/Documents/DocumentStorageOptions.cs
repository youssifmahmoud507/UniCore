using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using UniCore.Application.Modules.Admissions;
using UniCore.Application.Modules.Documents;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Admissions;
using UniCore.Domain.Modules.Documents;
using UniCore.Infrastructure.Persistence;

namespace UniCore.Infrastructure.Modules.Documents
{
    // appsettings: "Documents": { "StoragePath": "", "MaxFileSizeBytes": 10485760 }
    // An empty StoragePath means "<app folder>/storage/documents". Keep it OUTSIDE any public folder.
    public sealed class DocumentStorageOptions
    {
        public const string SectionName = "Documents";

        public string StoragePath { get; set; } = string.Empty;

        public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

        public static bool IsValid(DocumentStorageOptions options) => options.MaxFileSizeBytes is > 0 and <= 100L * 1024 * 1024;
    }

    public sealed class DocumentPolicy(IOptions<DocumentStorageOptions> options) : IDocumentPolicy
    {
        private readonly DocumentStorageOptions _options = options.Value;

        public long MaxFileSizeBytes => _options.MaxFileSizeBytes;
    }

    // Files on the server disk, outside any public folder. A file's name on disk is its random key (32 hex
    // characters), inside a folder named after the first two. Written to a temporary name first, then moved, so a
    // half-written file is never readable. Nothing a user types ever becomes part of a path.
    public sealed class LocalFileStorage(IOptions<DocumentStorageOptions> options, IHostEnvironment environment, ILogger<LocalFileStorage> logger) : IFileStorage
    {
        private readonly string _root = ResolveRoot(options.Value, environment);
        private readonly ILogger<LocalFileStorage> _logger = logger;

        public async Task<Result> SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default)
        {
            if (!IsValidKey(storageKey))
            {
                return Result.Fail(DocumentErrors.DataInvalid);
            }

            var path = PathOf(storageKey);
            var temporary = path + ".tmp";

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? _root);

                await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    await content.CopyToAsync(file, cancellationToken);
                }

                File.Move(temporary, path, overwrite: false);

                return Result.Ok();
            }
            catch (OperationCanceledException)
            {
                TryDelete(temporary);
                return Result.Fail(CommonErrors.OperationCancelled);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Type only: a message can contain the folder path.
                _logger.LogError("A document file could not be stored ({ExceptionType}).", ex.GetType().Name);
                TryDelete(temporary);
                return Result.Fail(DocumentErrors.StorageUnavailable);
            }
        }

        public Task<Result<Stream>> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            if (!IsValidKey(storageKey))
            {
                return Task.FromResult(Result<Stream>.Fail(DocumentErrors.FileMissing));
            }

            var path = PathOf(storageKey);

            if (!File.Exists(path))
            {
                return Task.FromResult(Result<Stream>.Fail(DocumentErrors.FileMissing));
            }

            try
            {
                Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

                return Task.FromResult(Result<Stream>.Ok(stream));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError("A document file could not be opened ({ExceptionType}).", ex.GetType().Name);
                return Task.FromResult(Result<Stream>.Fail(DocumentErrors.StorageUnavailable));
            }
        }

        public Task<Result> DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            if (!IsValidKey(storageKey))
            {
                return Task.FromResult(Result.Fail(DocumentErrors.DataInvalid));
            }

            return Task.FromResult(TryDelete(PathOf(storageKey)) ? Result.Ok() : Result.Fail(DocumentErrors.StorageUnavailable));
        }

        private string PathOf(string key) => Path.Combine(_root, key[..2], key);

        private static bool IsValidKey(string key) => key.Length == Document.StorageKeyLength && key.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

        private bool TryDelete(string path)
        {
            try
            {
                File.Delete(path);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning("A document file could not be deleted ({ExceptionType}).", ex.GetType().Name);
                return false;
            }
        }

        private static string ResolveRoot(DocumentStorageOptions options, IHostEnvironment environment)
            => Path.GetFullPath(string.IsNullOrWhiteSpace(options.StoragePath)
                ? Path.Combine(environment.ContentRootPath, "storage", "documents")
                : options.StoragePath);
    }


    // The local default: every file is accepted. Put an antivirus here when there is one (return a failure to reject).
    public sealed class NoOpFileScanner : IFileScanner
    {
        public Task<Result> ScanAsync(byte[] content, string fileName, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Ok());
    }

    public sealed class UserDirectory(AppDbContext db) : IUserDirectory
    {
        private readonly AppDbContext _db = db;

        public async Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive, cancellationToken);
            }
            catch (Exception ex) when (ex is DbException or TimeoutException or InvalidOperationException or OperationCanceledException)
            {
                return false;
            }
        }
    }

    // Replaces Developer 1's UnverifiedAdmissionDocumentVerifier: an admission requirement can only point to a
    // document that exists and is not deleted.
    public sealed class AdmissionDocumentVerifier(IDocumentService documents) : IAdmissionDocumentVerifier
    {
        private readonly IDocumentService _documents = documents;

        public async Task<Result> VerifyAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            var linkable = await _documents.VerifyLinkableAsync(documentId, cancellationToken);

            return linkable.IsSuccess ? Result.Ok() : Result.Fail(AdmissionsErrors.DocumentInvalid);
        }
    }

    // Seeds the document types when the application starts. It never stops the application from starting:
    // if the database is not ready, it logs and the types can be created later through the API.
    public sealed class DocumentTypeSeedService(IServiceScopeFactory scopes, ILogger<DocumentTypeSeedService> logger) : IHostedService
    {
        private readonly IServiceScopeFactory _scopes = scopes;
        private readonly ILogger<DocumentTypeSeedService> _logger = logger;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var seeder = scope.ServiceProvider.GetRequiredService<DocumentTypeSeeder>();

                var result = await seeder.SeedAsync(cancellationToken);

                if (result.IsFailure)
                {
                    _logger.LogWarning("Document types were not seeded ({ErrorCode}).", result.Error?.Code);
                }
            }
            catch (Exception ex) when (ex is DbException or InvalidOperationException or TimeoutException)
            {
                _logger.LogWarning("Document types were not seeded: the database is not ready ({ExceptionType}).", ex.GetType().Name);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public static class DocumentsServiceCollectionExtensions
    {
        public static IServiceCollection AddDocumentsModule(this IServiceCollection services)
        {
            services.AddOptions<DocumentStorageOptions>()
                .BindConfiguration(DocumentStorageOptions.SectionName)
                .Validate(DocumentStorageOptions.IsValid, "Documents has an invalid value.")
                .ValidateOnStart();

            services.AddSingleton<IDocumentPolicy, DocumentPolicy>();
            services.AddSingleton<IFileStorage, LocalFileStorage>();
            services.AddSingleton<IFileScanner, NoOpFileScanner>();

            services.AddScoped<IUserDirectory, UserDirectory>();
            services.AddScoped<IDocumentService, DocumentService>();
            services.AddScoped<DocumentAccessEvaluator>();
            services.AddScoped<DocumentHandler>();
            services.AddScoped<DocumentTypeHandler>();
            services.AddScoped<DocumentTypeSeeder>();

            services.AddHostedService<DocumentTypeSeedService>();

            // Developer 1's admissions registered a verifier that accepts anything; this one checks the document.
            services.Replace(ServiceDescriptor.Scoped<IAdmissionDocumentVerifier, AdmissionDocumentVerifier>());

            return services;
        }
    }

}
