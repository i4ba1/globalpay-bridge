using GlobalPay.Domain.Common;
using GlobalPay.Domain.Finance;
using GlobalPay.Domain.Modules.Payments;

namespace GlobalPay.UnitTests;

public sealed class PaymentBatchTests
{
    private static readonly Currency Usd = Currency.FromCode("USD");
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithoutInstructions_IsRejected()
    {
        var exception = Assert.Throws<DomainException>(() => NewBatch([]));

        Assert.Equal("payments.instructions_required", exception.Code);
    }

    [Fact]
    public void ApplyProviderEvent_BeforeSubmit_IsIgnored()
    {
        var batch = NewBatch([new PaymentInstructionSpec(Guid.NewGuid(), Guid.NewGuid(), 100m)]);
        var instruction = batch.Instructions.First();

        var applied = batch.ApplyProviderEvent(
            instruction.Id,
            ProviderEventType.Accepted,
            "prov-1",
            null,
            Now);

        Assert.False(applied);
        Assert.Equal(PaymentBatchStatus.Created, batch.Status);
    }

    [Fact]
    public void FullSettlement_MovesBatchToSettled()
    {
        var batch = NewBatch([new PaymentInstructionSpec(Guid.NewGuid(), Guid.NewGuid(), 100m)]);
        var instruction = batch.Instructions.First();

        batch.MarkSubmitted("prov-1", Now);
        Assert.True(batch.ApplyProviderEvent(instruction.Id, ProviderEventType.Accepted, "prov-1", null, Now));
        Assert.True(batch.ApplyProviderEvent(instruction.Id, ProviderEventType.Settled, "prov-1", null, Now));

        Assert.Equal(PaymentBatchStatus.Settled, batch.Status);
        Assert.NotNull(batch.SettledAtUtc);
    }

    [Fact]
    public void OutOfOrderEvent_IsIgnored_AndRetained()
    {
        var batch = NewBatch([new PaymentInstructionSpec(Guid.NewGuid(), Guid.NewGuid(), 100m)]);
        var instruction = batch.Instructions.First();

        batch.MarkSubmitted("prov-1", Now);
        batch.ApplyProviderEvent(instruction.Id, ProviderEventType.Settled, "prov-1", null, Now);

        var regression = batch.ApplyProviderEvent(instruction.Id, ProviderEventType.Accepted, "prov-1", null, Now.AddMinutes(1));

        Assert.False(regression);
        Assert.Equal(PaymentInstructionStatus.Settled, instruction.Status);
        Assert.Equal(PaymentBatchStatus.Settled, batch.Status);
    }

    [Fact]
    public void PartialSettlement_IsReported()
    {
        var batch = NewBatch(
        [
            new PaymentInstructionSpec(Guid.NewGuid(), Guid.NewGuid(), 100m),
            new PaymentInstructionSpec(Guid.NewGuid(), Guid.NewGuid(), 200m),
        ]);

        batch.MarkSubmitted("prov-1", Now);

        var first = batch.Instructions.First();
        batch.ApplyProviderEvent(first.Id, ProviderEventType.Settled, "prov-1", null, Now);

        Assert.Equal(PaymentBatchStatus.PartiallySettled, batch.Status);
        Assert.Equal(300m, batch.TotalAmount);
    }

    [Fact]
    public void FailedInstruction_IsTerminal()
    {
        var batch = NewBatch([new PaymentInstructionSpec(Guid.NewGuid(), Guid.NewGuid(), 100m)]);
        var instruction = batch.Instructions.First();

        batch.MarkSubmitted("prov-1", Now);
        Assert.True(batch.ApplyProviderEvent(instruction.Id, ProviderEventType.Failed, "prov-1", "insufficient_funds", Now));

        var retry = batch.ApplyProviderEvent(instruction.Id, ProviderEventType.Accepted, "prov-1", null, Now.AddMinutes(1));

        Assert.False(retry);
        Assert.Equal(PaymentInstructionStatus.Failed, instruction.Status);
        Assert.Equal("insufficient_funds", instruction.FailureCode);
        Assert.Equal(PaymentBatchStatus.Failed, batch.Status);
    }

    private static PaymentBatch NewBatch(IReadOnlyList<PaymentInstructionSpec> instructions) =>
        PaymentBatch.Create(Guid.NewGuid(), Guid.NewGuid(), Usd, "key-1", "finance.user", Now, instructions);
}
