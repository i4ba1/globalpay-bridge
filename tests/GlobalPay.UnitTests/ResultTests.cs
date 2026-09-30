using GlobalPay.Domain.Common;

namespace GlobalPay.UnitTests;

public sealed class ResultTests
{
    [Fact]
    public void Success_HasNoError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_CarriesError()
    {
        var error = Error.Validation("worker.invalid", "Worker is invalid.");

        var result = Result.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void GenericSuccess_ExposesValue()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void GenericFailure_AccessingValue_Throws()
    {
        var result = Result.Failure<int>(Error.NotFound("worker.missing", "Not found."));

        Assert.Throws<DomainException>(() => result.Value);
    }

    [Fact]
    public void ImplicitConversion_WrapsValueAsSuccess()
    {
        Result<string> result = "value";

        Assert.True(result.IsSuccess);
        Assert.Equal("value", result.Value);
    }

    [Fact]
    public void SuccessWithError_IsRejected()
    {
        Assert.Throws<DomainException>(() => Result.Failure(Error.None));
    }
}
