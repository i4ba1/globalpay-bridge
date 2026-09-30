using System.Data.Common;
using Dapper;
using GlobalPay.Application.Abstractions.Idempotency;
using GlobalPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GlobalPay.Infrastructure.Idempotency;

internal sealed class SqlIdempotencyStore : IIdempotencyStore
{
    private const string SelectColumns =
        "Id, TenantId, OperationCode, IdempotencyKey, RequestFingerprint, Outcome, StatusCode, ResponseBody, CreatedAtUtc, CompletedAtUtc";

    private readonly GlobalPayDbContext _context;
    private readonly TimeProvider _timeProvider;

    public SqlIdempotencyStore(GlobalPayDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<IdempotencyRecord?> FindAsync(
        Guid tenantId,
        string operationCode,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT {SelectColumns}
            FROM dbo.IdempotencyRecord
            WHERE TenantId = @TenantId AND OperationCode = @OperationCode AND IdempotencyKey = @IdempotencyKey;
            """;

        var connection = _context.Database.GetDbConnection();

        var row = await connection.QuerySingleOrDefaultAsync<IdempotencyRow>(
            new CommandDefinition(
                sql,
                new { TenantId = tenantId, OperationCode = operationCode, IdempotencyKey = idempotencyKey },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return row?.ToRecord();
    }

    public async Task<IdempotencyBeginResult> BeginAsync(
        Guid tenantId,
        string operationCode,
        string idempotencyKey,
        string requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO dbo.IdempotencyRecord
                (Id, TenantId, OperationCode, IdempotencyKey, RequestFingerprint, Outcome, CreatedAtUtc)
            VALUES
                (@Id, @TenantId, @OperationCode, @IdempotencyKey, @RequestFingerprint, @Outcome, @CreatedAtUtc);
            """;

        var now = _timeProvider.GetUtcNow();
        var id = Guid.NewGuid();

        var connection = _context.Database.GetDbConnection();

        try
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    sql,
                    new
                    {
                        Id = id,
                        TenantId = tenantId,
                        OperationCode = operationCode,
                        IdempotencyKey = idempotencyKey,
                        RequestFingerprint = requestFingerprint,
                        Outcome = (int)IdempotencyOutcome.InProgress,
                        CreatedAtUtc = now,
                    },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (DbException)
        {
            var existing = await FindAsync(tenantId, operationCode, idempotencyKey, cancellationToken).ConfigureAwait(false);

            if (existing is null)
            {
                throw;
            }

            return new IdempotencyBeginResult(existing, false);
        }

        return new IdempotencyBeginResult(
            new IdempotencyRecord(
                id,
                tenantId,
                operationCode,
                idempotencyKey,
                requestFingerprint,
                IdempotencyOutcome.InProgress,
                null,
                null,
                now,
                null),
            true);
    }

    public async Task CompleteAsync(
        Guid recordId,
        int statusCode,
        string responseBody,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.IdempotencyRecord
            SET Outcome = @Outcome, StatusCode = @StatusCode, ResponseBody = @ResponseBody, CompletedAtUtc = @CompletedAtUtc
            WHERE Id = @Id;
            """;

        var connection = _context.Database.GetDbConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    Id = recordId,
                    Outcome = (int)IdempotencyOutcome.Completed,
                    StatusCode = statusCode,
                    ResponseBody = responseBody,
                    CompletedAtUtc = _timeProvider.GetUtcNow(),
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task AbandonAsync(Guid recordId, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM dbo.IdempotencyRecord WHERE Id = @Id;";

        var connection = _context.Database.GetDbConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = recordId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private sealed class IdempotencyRow
    {
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }

        public string OperationCode { get; set; } = string.Empty;

        public string IdempotencyKey { get; set; } = string.Empty;

        public string RequestFingerprint { get; set; } = string.Empty;

        public int Outcome { get; set; }

        public int? StatusCode { get; set; }

        public string? ResponseBody { get; set; }

        public DateTimeOffset CreatedAtUtc { get; set; }

        public DateTimeOffset? CompletedAtUtc { get; set; }

        public IdempotencyRecord ToRecord() => new(
            Id,
            TenantId,
            OperationCode,
            IdempotencyKey,
            RequestFingerprint,
            (IdempotencyOutcome)Outcome,
            StatusCode,
            ResponseBody,
            CreatedAtUtc,
            CompletedAtUtc);
    }
}
