using Plantech.Bim.PhaseVisualizer.Configuration;
using Plantech.Bim.PhaseVisualizer.Domain;
using System;
using System.Globalization;
using System.Linq;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal static class PhaseCellDisplayFormatter
{
    private const string UnixSecondsDateFormat = "unixSecondsDate";
    private const string DefaultDateFormat = "yyyy-MM-dd";

    public static PhaseCellValue Format(PhaseCellValue value, PhaseColumnConfig column)
    {
        if (column == null)
        {
            throw new ArgumentNullException(nameof(column));
        }

        if (!value.HasValue || string.IsNullOrWhiteSpace(column.DisplayFormat))
        {
            return value.ConvertTo(column.Type);
        }

        if (string.Equals(column.DisplayFormat, UnixSecondsDateFormat, StringComparison.OrdinalIgnoreCase))
        {
            var dateFormat = string.IsNullOrWhiteSpace(column.DateFormat)
                ? DefaultDateFormat
                : column.DateFormat;

            if (column.Aggregate == PhaseAggregateType.Distinct)
            {
                var joinedValues = value.AsComparableString();
                if (string.IsNullOrWhiteSpace(joinedValues))
                {
                    return PhaseCellValue.Empty;
                }

                var formattedParts = joinedValues
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => TryFormatUnixSecondsDate(part.Trim(), dateFormat))
                    .ToList();

                return PhaseCellValue.FromString(string.Join("; ", formattedParts))
                    .ConvertTo(column.Type);
            }

            return PhaseCellValue.FromString(
                    TryFormatUnixSecondsDate(value.AsComparableString(), dateFormat))
                .ConvertTo(column.Type);
        }

        return value.ConvertTo(column.Type);
    }

    private static string TryFormatUnixSecondsDate(string rawValue, string dateFormat)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        if (!long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
        {
            return rawValue;
        }

        try
        {
            return DateTimeOffset
                .FromUnixTimeSeconds(seconds)
                .UtcDateTime
                .ToString(dateFormat, CultureInfo.InvariantCulture);
        }
        catch
        {
            return rawValue;
        }
    }
}
