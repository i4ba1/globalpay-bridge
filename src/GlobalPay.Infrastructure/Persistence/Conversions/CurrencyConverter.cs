using GlobalPay.Domain.Finance;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GlobalPay.Infrastructure.Persistence.Conversions;

internal sealed class CurrencyConverter : ValueConverter<Currency, string>
{
    public CurrencyConverter()
        : base(currency => currency.Code, code => Currency.FromCode(code))
    {
    }
}
