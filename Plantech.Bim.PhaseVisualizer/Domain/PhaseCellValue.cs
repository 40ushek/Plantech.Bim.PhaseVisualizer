using System;
using System.Collections.Generic;
using System.Globalization;

namespace Plantech.Bim.PhaseVisualizer.Domain;

internal sealed class PhaseCellValue
{
    private static readonly HashSet<string> TrueTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "true",
        "1",
        "yes",
        "y",
        "on",
    };

    private static readonly HashSet<string> FalseTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "false",
        "0",
        "no",
        "n",
        "off",
    };

    public static PhaseCellValue Empty { get; } = new(PhaseValueType.String, null, null, null);

    public PhaseCellValue(PhaseValueType type, string? text, double? number, bool? boolean)
    {
        Type = type;
        Text = text;
        Number = number;
        Bool = boolean;
    }

    public PhaseValueType Type { get; }
    public string? Text { get; }
    public double? Number { get; }
    public bool? Bool { get; }

    public bool HasValue =>
        !string.IsNullOrWhiteSpace(Text) || Number.HasValue || Bool.HasValue;

    public static PhaseCellValue FromString(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return new PhaseCellValue(PhaseValueType.String, null, null, null);
        }

        return new PhaseCellValue(PhaseValueType.String, trimmed, null, null);
    }

    public static PhaseCellValue FromNumber(double? value)
    {
        if (!value.HasValue)
        {
            return new PhaseCellValue(PhaseValueType.Number, null, null, null);
        }

        return new PhaseCellValue(PhaseValueType.Number, null, value, null);
    }

    public static PhaseCellValue FromInteger(int? value)
    {
        if (!value.HasValue)
        {
            return new PhaseCellValue(PhaseValueType.Integer, null, null, null);
        }

        return new PhaseCellValue(PhaseValueType.Integer, null, value.Value, null);
    }

    public static PhaseCellValue FromBool(bool? value)
    {
        if (!value.HasValue)
        {
            return new PhaseCellValue(PhaseValueType.Boolean, null, null, null);
        }

        return new PhaseCellValue(PhaseValueType.Boolean, null, null, value);
    }

    public static PhaseCellValue FromObject(object? value)
    {
        return value switch
        {
            null => Empty,
            string s => FromString(s),
            bool b => FromBool(b),
            byte n => FromInteger(n),
            sbyte n => FromInteger(n),
            short n => FromInteger(n),
            ushort n => FromInteger(n),
            int n => FromInteger(n),
            uint n when n <= int.MaxValue => FromInteger((int)n),
            long n when n >= int.MinValue && n <= int.MaxValue => FromInteger((int)n),
            float n => FromNumber(n),
            double n => FromNumber(n),
            decimal n => FromNumber((double)n),
            _ => FromString(value?.ToString())
        };
    }

    public string AsComparableString()
    {
        return Type switch
        {
            PhaseValueType.Integer when Number.HasValue => ((int)Number.Value).ToString(CultureInfo.InvariantCulture),
            PhaseValueType.Number when Number.HasValue => Number.Value.ToString(CultureInfo.InvariantCulture),
            PhaseValueType.Boolean when Bool.HasValue => Bool.Value ? "true" : "false",
            _ => Text ?? string.Empty,
        };
    }

    public PhaseCellValue ConvertTo(PhaseValueType targetType)
    {
        if (targetType == Type)
        {
            return this;
        }

        return targetType switch
        {
            PhaseValueType.String => FromString(AsComparableString()),
            PhaseValueType.Number => ConvertToNumber(),
            PhaseValueType.Integer => ConvertToInteger(),
            PhaseValueType.Boolean => ConvertToBool(),
            _ => Empty,
        };
    }

    private PhaseCellValue ConvertToNumber()
    {
        if (Number.HasValue)
        {
            return FromNumber(Number.Value);
        }

        if (Bool.HasValue)
        {
            return FromNumber(Bool.Value ? 1d : 0d);
        }

        if (double.TryParse(Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return FromNumber(parsed);
        }

        return Empty;
    }

    private PhaseCellValue ConvertToInteger()
    {
        if (Number.HasValue && IsWholeNumber(Number.Value))
        {
            return FromInteger((int)Number.Value);
        }

        if (Bool.HasValue)
        {
            return FromInteger(Bool.Value ? 1 : 0);
        }

        if (int.TryParse(Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt))
        {
            return FromInteger(parsedInt);
        }

        if (double.TryParse(Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedDouble)
            && IsWholeNumber(parsedDouble))
        {
            return FromInteger((int)parsedDouble);
        }

        return Empty;
    }

    private PhaseCellValue ConvertToBool()
    {
        if (Bool.HasValue)
        {
            return FromBool(Bool.Value);
        }

        if (Number.HasValue)
        {
            return FromBool(Math.Abs(Number.Value) > double.Epsilon);
        }

        if (TryParseLooseBool(Text, out var parsed))
        {
            return FromBool(parsed);
        }

        return Empty;
    }

    private static bool IsWholeNumber(double value)
    {
        return Math.Abs(value % 1d) < double.Epsilon
            && value >= int.MinValue
            && value <= int.MaxValue;
    }

    private static bool TryParseLooseBool(string? text, out bool value)
    {
        value = false;
        var normalized = text?.Trim();
        if (normalized == null || normalized.Length == 0)
        {
            return false;
        }
        if (TrueTokens.Contains(normalized))
        {
            value = true;
            return true;
        }

        if (FalseTokens.Contains(normalized))
        {
            value = false;
            return true;
        }

        return false;
    }
}

