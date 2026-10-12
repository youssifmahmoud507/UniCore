using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using UniCore.Application.Modules.Students;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Students;
using UniCore.Infrastructure.Persistence;

namespace UniCore.Infrastructure.Modules.Students
{
    public sealed class StudentNumberGenerator(AppDbContext db, ILogger<StudentNumberGenerator> logger) : IStudentNumberGenerator
    {
        private static readonly Regex CodePattern = new("^[A-Z0-9]{2,10}$", RegexOptions.Compiled);

        // One statement: it creates the row or increments it, and returns the new value.
        // HOLDLOCK makes two parallel callers for a new key safe.
        private const string IncrementSql = """
        MERGE [students].[StudentNumberCounters] WITH (HOLDLOCK) AS target
        USING (SELECT @key AS CounterKey) AS source
        ON target.CounterKey = source.CounterKey
        WHEN MATCHED THEN UPDATE SET target.LastValue = target.LastValue + 1
        WHEN NOT MATCHED THEN INSERT (CounterKey, LastValue) VALUES (source.CounterKey, 1)
        OUTPUT inserted.LastValue;
        """;

        private readonly AppDbContext _db = db;
        private readonly ILogger<StudentNumberGenerator> _logger = logger;

        public async Task<Result<string>> NextAsync(
            int admissionYear, string? programCode, CancellationToken cancellationToken = default)
        {
            var code = programCode?.Trim().ToUpperInvariant();

            if (admissionYear is < 2000 or > 2100 || string.IsNullOrEmpty(code) || !CodePattern.IsMatch(code))
            {
                return StudentErrors.DataInvalid;
            }

            var key = $"{admissionYear}-{code}";

            try
            {
                var connection = _db.Database.GetDbConnection();
                var openedHere = connection.State != ConnectionState.Open;

                if (openedHere)
                {
                    await connection.OpenAsync(cancellationToken);
                }

                try
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = IncrementSql;

                    var parameter = command.CreateParameter();
                    parameter.ParameterName = "@key";
                    parameter.Value = key;
                    command.Parameters.Add(parameter);

                    var scalar = await command.ExecuteScalarAsync(cancellationToken);

                    if (scalar is null || scalar is DBNull)
                    {
                        return StudentErrors.NumberGenerationFailed;
                    }

                    var value = Convert.ToInt32(scalar, CultureInfo.InvariantCulture);

                    return $"{admissionYear}{code}{value.ToString("D4", CultureInfo.InvariantCulture)}";
                }
                finally
                {
                    if (openedHere)
                    {
                        await connection.CloseAsync();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return CommonErrors.OperationCancelled;
            }
            catch (Exception ex)
            {
                _logger.LogError("Generating a student number failed ({ExceptionType}).", ex.GetType().Name);
                return StudentErrors.NumberGenerationFailed;
            }
        }
    }
}
