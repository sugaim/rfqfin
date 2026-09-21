using System.Globalization;
using System.Text.RegularExpressions;
using Rfq.Domain;

namespace Rfq.Application;

public static partial class SecuritySearchNormalizer
{
    public static string? NormalizeInternalCode(string query)
    {
        var components = query.Trim().Split('-', StringSplitOptions.TrimEntries);
        int[] widths;
        if (components.Length == 2)
        {
            components = ["0", "02", components[0], components[1]];
            widths = [1, 2, 4, 5];
        }
        else if (components.Length == 4)
        {
            widths = [1, 2, 4, 5];
        }
        else
        {
            return null;
        }

        var normalized = new string[components.Length];
        for (var index = 0; index < components.Length; index++)
        {
            if (!components[index].All(char.IsDigit)
                || components[index].Length == 0
                || components[index].Length > widths[index])
            {
                return null;
            }

            normalized[index] = components[index].PadLeft(widths[index], '0');
        }

        return string.Join('-', normalized);
    }

    public static string NormalizeBbgText(string query)
    {
        var tokens = WhitespacePattern()
            .Split(query.Trim().ToUpperInvariant())
            .Where(token => token.Length > 0)
            .ToArray();

        if (tokens.Length >= 2
            && decimal.TryParse(
                tokens[1],
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var coupon))
        {
            tokens[1] = coupon.ToString("0.################", CultureInfo.InvariantCulture);
        }

        if (tokens.Length >= 3 && TryNormalizeMaturity(tokens[2], out var maturity))
        {
            tokens[2] = maturity;
        }

        return string.Join(' ', tokens);
    }

    public static string? NormalizeIsinPrefix(string query)
    {
        var normalized = query.Trim().ToUpperInvariant();
        return IsinPattern().IsMatch(normalized) ? normalized : null;
    }

    private static bool TryNormalizeMaturity(string value, out string normalized)
    {
        normalized = string.Empty;
        var components = value.Split('/');
        if (components.Length != 3
            || !int.TryParse(components[0], out var month)
            || !int.TryParse(components[1], out var day)
            || !int.TryParse(components[2], out var year))
        {
            return false;
        }

        if (year is >= 0 and <= 99)
        {
            year += 2000;
        }

        if (!DateOnly.TryParseExact(
                $"{year:D4}-{month:D2}-{day:D2}",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return false;
        }

        normalized = date.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
        return true;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex("^[A-Z]{2}[A-Z0-9]{5,10}$")]
    private static partial Regex IsinPattern();
}
