using GlobalPay.Domain.Common;

namespace GlobalPay.Application.Abstractions;

internal static class DomainExceptionMapping
{
    public static Error ToError(this DomainException exception, string fallbackCode)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var code = string.IsNullOrWhiteSpace(exception.Code) ? fallbackCode : exception.Code;

        return Error.Conflict(code, exception.Message);
    }
}
