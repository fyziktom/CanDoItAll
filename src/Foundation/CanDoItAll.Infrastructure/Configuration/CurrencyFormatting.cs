using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Infrastructure.Configuration;

public sealed class CurrencyOptions
{
    public const string SectionName = "Currency";

    [RegularExpression("^[A-Za-z]{3}$")]
    public string CurrencyCode { get; set; } = "USD";

    public string CultureName { get; set; } = "en-US";

    [Range(0, 6)]
    public int DecimalDigits { get; set; } = 2;
}

public sealed record CurrencyDisplaySettings(
    string CurrencyCode,
    string CultureName,
    int DecimalDigits)
{
    public static CurrencyDisplaySettings Default { get; } = new("USD", "en-US", 2);

    public static CurrencyDisplaySettings Normalize(
        string? currencyCode,
        string? cultureName,
        int decimalDigits = 2)
    {
        var normalizedCode = string.IsNullOrWhiteSpace(currencyCode)
            ? Default.CurrencyCode
            : currencyCode.Trim().ToUpperInvariant();
        if (normalizedCode.Length != 3 || normalizedCode.Any(character => character is < 'A' or > 'Z'))
        {
            normalizedCode = Default.CurrencyCode;
        }

        var normalizedCultureName = string.IsNullOrWhiteSpace(cultureName)
            ? Default.CultureName
            : cultureName.Trim();
        if (!CanResolveCulture(normalizedCultureName))
        {
            normalizedCultureName = Default.CultureName;
        }

        return new CurrencyDisplaySettings(
            normalizedCode,
            normalizedCultureName,
            Math.Clamp(decimalDigits, 0, 6));
    }

    public static CurrencyDisplaySettings FromOptions(CurrencyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Normalize(options.CurrencyCode, options.CultureName, options.DecimalDigits);
    }

    private static bool CanResolveCulture(string cultureName)
    {
        try
        {
            CultureInfo.GetCultureInfo(cultureName);
            return true;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }
}

public interface ICurrencyFormatter
{
    string CurrencyCode { get; }

    string Format(decimal value);
}

public sealed class CurrencyDisplayState
{
    private readonly object gate = new();
    private CurrencyDisplaySettings current;

    public CurrencyDisplayState(IOptions<CurrencyOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        current = CurrencyDisplaySettings.FromOptions(options.Value);
    }

    public CurrencyDisplaySettings Current
    {
        get
        {
            lock (gate)
            {
                return current;
            }
        }
    }

    public void Update(CurrencyDisplaySettings settings)
    {
        lock (gate)
        {
            current = settings;
        }
    }

    public void Update(string? currencyCode, string? cultureName, int decimalDigits = 2)
    {
        Update(CurrencyDisplaySettings.Normalize(currencyCode, cultureName, decimalDigits));
    }
}

public sealed class CurrencyFormatter(CurrencyDisplayState state) : ICurrencyFormatter
{
    public string CurrencyCode => state.Current.CurrencyCode;

    public string Format(decimal value)
    {
        return CurrencyFormatting.Format(value, state.Current);
    }
}

public static class CurrencyFormatting
{
    public static string Format(decimal value)
    {
        return Format(value, CurrencyDisplaySettings.Default);
    }

    public static string Format(decimal value, CurrencyDisplaySettings settings)
    {
        var normalized = CurrencyDisplaySettings.Normalize(
            settings.CurrencyCode,
            settings.CultureName,
            settings.DecimalDigits);
        var culture = (CultureInfo)CultureInfo.GetCultureInfo(normalized.CultureName).Clone();
        culture.NumberFormat.CurrencySymbol = ResolveCurrencySymbol(culture, normalized.CurrencyCode);
        culture.NumberFormat.CurrencyDecimalDigits = normalized.DecimalDigits;

        if (value == 0m && normalized.CurrencyCode == "USD" && normalized.DecimalDigits == 2)
        {
            return "$0";
        }

        return value.ToString("C", culture);
    }

    private static string ResolveCurrencySymbol(CultureInfo culture, string currencyCode)
    {
        if (currencyCode == "USD")
        {
            return "$";
        }

        try
        {
            var region = new RegionInfo(culture.Name);
            if (string.Equals(region.ISOCurrencySymbol, currencyCode, StringComparison.OrdinalIgnoreCase))
            {
                return region.CurrencySymbol;
            }
        }
        catch (ArgumentException)
        {
        }

        return $"{currencyCode} ";
    }
}
