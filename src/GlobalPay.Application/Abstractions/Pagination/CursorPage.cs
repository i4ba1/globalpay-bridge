using System.Text;

namespace GlobalPay.Application.Abstractions.Pagination;

public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);

public static class CursorCodec
{
    private const char Separator = '\u001f';

    public static string Encode(params object?[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        var joined = string.Join(Separator, parts.Select(part => part?.ToString() ?? string.Empty));

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(joined));
    }

    public static bool TryDecode(string? cursor, out IReadOnlyList<string> parts)
    {
        parts = [];

        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            parts = decoded.Split(Separator);

            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
