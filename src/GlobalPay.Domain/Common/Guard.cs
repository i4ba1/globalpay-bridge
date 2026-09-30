namespace GlobalPay.Domain.Common;

public static class Guard
{
    public static T NotNull<T>(T? value, string parameterName)
        where T : class
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        return value;
    }

    public static string NotNullOrWhiteSpace(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be null or whitespace.", parameterName);
        }

        return value;
    }

    public static Guid NotEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Value must not be an empty identifier.", parameterName);
        }

        return value;
    }

    public static decimal NotNegative(decimal value, string parameterName)
    {
        if (value < 0m)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must not be negative.");
        }

        return value;
    }

    public static TEnum DefinedEnum<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must be a defined enum member.");
        }

        return value;
    }
}
