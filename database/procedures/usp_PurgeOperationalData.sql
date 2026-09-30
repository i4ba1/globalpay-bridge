-- GBP-DBD-001 §5/§8: remove eligible transient operational data.
-- Never removes financial audit evidence. Always run with @DryRun = 1 first.
CREATE OR ALTER PROCEDURE dbo.usp_PurgeOperationalData
    @OlderThanDays INT,
    @DryRun        BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Cutoff DATETIMEOFFSET(3) = DATEADD(DAY, -@OlderThanDays, SYSDATETIMEOFFSET());

    DECLARE @OutboxCandidates INT = (
        SELECT COUNT(*) FROM dbo.OutboxMessage
        WHERE ProcessedAtUtc IS NOT NULL AND ProcessedAtUtc < @Cutoff);

    DECLARE @InboxCandidates INT = (
        SELECT COUNT(*) FROM dbo.ProviderEventInbox
        WHERE ProcessedAtUtc IS NOT NULL AND ProcessedAtUtc < @Cutoff);

    DECLARE @IdempotencyCandidates INT = (
        SELECT COUNT(*) FROM dbo.IdempotencyRecord
        WHERE CompletedAtUtc IS NOT NULL AND CompletedAtUtc < @Cutoff);

    SELECT @OutboxCandidates AS OutboxMessages, @InboxCandidates AS ProviderEvents, @IdempotencyCandidates AS IdempotencyRecords;

    IF @DryRun = 1
    BEGIN
        RETURN;
    END

    DELETE FROM dbo.OutboxMessage
    WHERE ProcessedAtUtc IS NOT NULL AND ProcessedAtUtc < @Cutoff;

    DELETE FROM dbo.ProviderEventInbox
    WHERE ProcessedAtUtc IS NOT NULL AND ProcessedAtUtc < @Cutoff;

    DELETE FROM dbo.IdempotencyRecord
    WHERE CompletedAtUtc IS NOT NULL AND CompletedAtUtc < @Cutoff;
END;
