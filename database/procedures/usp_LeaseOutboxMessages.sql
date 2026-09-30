-- GBP-DBD-001 §5/§7: atomically lease pending outbox work for dispatch.
-- UPDLOCK/READPAST keeps concurrent dispatchers from leasing the same row.
CREATE OR ALTER PROCEDURE dbo.usp_LeaseOutboxMessages
    @BatchSize     INT,
    @LeaseSeconds  INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Now DATETIMEOFFSET(3) = SYSDATETIMEOFFSET();
    DECLARE @LeaseUntil DATETIMEOFFSET(3) = DATEADD(SECOND, @LeaseSeconds, @Now);

    ;WITH Pending AS (
        SELECT TOP (@BatchSize) *
        FROM dbo.OutboxMessage WITH (UPDLOCK, READPAST, ROWLOCK)
        WHERE ProcessedAtUtc IS NULL
          AND AvailableAtUtc <= @Now
          AND (LeaseUntilUtc IS NULL OR LeaseUntilUtc < @Now)
        ORDER BY AvailableAtUtc
    )
    UPDATE Pending
    SET LeaseUntilUtc = @LeaseUntil
    OUTPUT inserted.Id, inserted.Type, inserted.Payload, inserted.Attempts;
END;
