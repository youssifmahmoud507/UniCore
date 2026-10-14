using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Globalization;
using UniCore.Application.Modules.Finance;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Finance;
using UniCore.Infrastructure.Persistence;

namespace UniCore.Infrastructure.Modules.Finance
{
    public sealed class InvoiceNumberGenerator(AppDbContext db, ILogger<InvoiceNumberGenerator> logger) : IInvoiceNumberGenerator
    {
        // One statement: it creates the row or increments it, and returns the new value.
        private const string IncrementSql = """
        MERGE [finance].[InvoiceNumberCounters] WITH (HOLDLOCK) AS target
        USING (SELECT @key AS CounterKey) AS source
        ON target.CounterKey = source.CounterKey
        WHEN MATCHED THEN UPDATE SET target.LastValue = target.LastValue + 1
        WHEN NOT MATCHED THEN INSERT (CounterKey, LastValue) VALUES (source.CounterKey, 1)
        OUTPUT inserted.LastValue;
        """;

        private readonly AppDbContext _db = db;
        private readonly ILogger<InvoiceNumberGenerator> _logger = logger;

        public async Task<Result<string>> NextAsync(int year, CancellationToken cancellationToken = default)
        {
            if (year is < 2000 or > 2100)
            {
                return FinanceErrors.InvoiceDataInvalid;
            }

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
                    parameter.Value = year.ToString(CultureInfo.InvariantCulture);
                    command.Parameters.Add(parameter);

                    var scalar = await command.ExecuteScalarAsync(cancellationToken);

                    if (scalar is null || scalar is DBNull)
                    {
                        return FinanceErrors.InvoiceNumberGenerationFailed;
                    }

                    var value = Convert.ToInt32(scalar, CultureInfo.InvariantCulture);

                    return $"INV-{year}-{value.ToString("D6", CultureInfo.InvariantCulture)}";
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
                _logger.LogError("Generating an invoice number failed ({ExceptionType}).", ex.GetType().Name);
                return FinanceErrors.InvoiceNumberGenerationFailed;
            }
        }
    }




}
