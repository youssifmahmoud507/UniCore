using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Documents.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Documents;

namespace UniCore.Application.Modules.Documents
{
    // The document types the platform needs from the first day. Safe to run on every start: it adds only what is
    // missing and never touches a type that already exists (an administrator may have changed it).
    public sealed class DocumentTypeSeeder(IAggregateRepository<DocumentType> types, IUnitOfWork unitOfWork, IClock clock)
    {
        private sealed record Definition(string Code, string Name, bool IsSensitive, DocumentFormat Formats);

        private static readonly Definition[] Defaults =
        [
            new(DocumentTypeCodes.AdmissionDocument, "Admission document", true, DocumentFormat.Pdf | DocumentFormat.Images),
            new(DocumentTypeCodes.AttendanceExcuse, "Attendance excuse", true, DocumentFormat.Pdf | DocumentFormat.Images),
            new(DocumentTypeCodes.Certificate, "Certificate", false, DocumentFormat.Pdf),
            new(DocumentTypeCodes.Transcript, "Transcript", true, DocumentFormat.Pdf),
            new(DocumentTypeCodes.General, "General document", false, DocumentFormat.All)
        ];

        private readonly IAggregateRepository<DocumentType> _types = types;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;

        public async Task<Result> SeedAsync(CancellationToken cancellationToken = default)
        {
            var added = 0;

            foreach (var definition in Defaults)
            {
                if (await _types.AnyAsync(new DocumentTypeByCodeSpecification(definition.Code), cancellationToken))
                {
                    continue;
                }

                var created = DocumentType.Create(definition.Code, definition.Name, definition.IsSensitive, definition.Formats, _clock.UtcNow);
                if (!created.TryGetValue(out var type))
                {
                    return Result.Fail(created.Errors);
                }

                _types.Add(type);
                added++;
            }

            if (added == 0)
            {
                return Result.Ok();
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Two instances starting together can both try: the loser sees a duplicate, which is fine.
            return saved.IsSuccess || saved.Error?.Code == CommonErrors.DuplicateResource.Code ? Result.Ok() : Result.Fail(saved.Errors);
        }
    }


}
