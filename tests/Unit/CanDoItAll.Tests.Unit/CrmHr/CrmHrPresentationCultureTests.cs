using System.Globalization;
using CanDoItAll.CrmHr.UI;
using CanDoItAll.CrmHr.UI.Activity;
using CanDoItAll.CrmHr.UI.Financials;

namespace CanDoItAll.Tests.Unit.CrmHr;

// Generated interface words stay English under non-English server cultures. The expectations are written out by
// hand; none of them is produced by the formatter under test.
public sealed class CrmHrPresentationCultureTests
{
    private static readonly string[] EnglishMonths =
    [
        "Jan 2025", "Feb 2025", "Mar 2025", "Apr 2025", "May 2025", "Jun 2025",
        "Jul 2025", "Aug 2025", "Sep 2025", "Oct 2025", "Nov 2025", "Dec 2025"
    ];

    [Theory]
    [InlineData("cs-CZ")]
    [InlineData("ja-JP")]
    [InlineData("ar-SA")]
    [InlineData("el-GR")]
    [InlineData("en-US")]
    public void Financial_month_and_year_labels_are_english_under_every_ambient_culture(string ambient)
    {
        using var scope = new AmbientCulture(ambient);

        var months = Enumerable.Range(1, 12)
            .Select(month => CrmHrFinancialsText.PeriodLabel(new DateOnly(2025, month, 1), CrmHrFinancialPeriod.Month))
            .ToArray();

        Assert.Equal(EnglishMonths, months);
        Assert.Equal("2025", CrmHrFinancialsText.PeriodLabel(new DateOnly(2025, 1, 1), CrmHrFinancialPeriod.Year));
        Assert.Equal("0987", CrmHrPresentationCulture.FormatYear(new DateOnly(987, 1, 1)));
    }

    [Fact]
    public void Chart_categories_use_the_english_labels_under_a_czech_server_culture()
    {
        using var scope = new AmbientCulture("cs-CZ");
        var snapshot = new CrmHrFinancialsSnapshot(
            Guid.NewGuid(),
            CrmHrFinancialAvailability.Available,
            [new CrmHrCurrencyTotal("EUR", 3m)],
            [
                new CrmHrFinancialPeriodAmount(new DateOnly(2025, 1, 1), "EUR", 1m),
                new CrmHrFinancialPeriodAmount(new DateOnly(2025, 3, 1), "EUR", 2m)
            ],
            [new CrmHrFinancialPeriodAmount(new DateOnly(2025, 1, 1), "EUR", 3m)],
            IncompleteWonCount: 0,
            CrmHrFinancialAvailability.Unavailable,
            CrmHrFinancialAvailability.Unavailable,
            CrmHrFinancialAvailability.Unavailable);

        var series = Assert.Single(CrmHrFinancialsChart.BuildSoldSeries(snapshot, CrmHrFinancialPeriod.Month));

        Assert.Equal(new[] { "Jan 2025", "Mar 2025" }, series.Points.Select(point => point.Category));
        // The amounts are the owner's decimals; the label policy never touches them.
        Assert.Equal(new[] { 1m, 2m }, series.Points.Select(point => point.Value));
    }

    [Fact]
    public void Timestamps_keep_the_ambient_numeric_pattern_and_only_force_english_day_period_designators()
    {
        var afternoon = new DateTime(2026, 3, 9, 14, 5, 0, DateTimeKind.Local);

        using (new AmbientCulture("cs-CZ"))
        {
            // Czech: day first, dots, 24-hour clock, no designator; the policy changes nothing here.
            Assert.Equal("09.03.2026 14:05", CrmHrPresentationCulture.FormatShortTimestamp(afternoon));
        }

        using (new AmbientCulture("en-US"))
        {
            // The separator before the designator belongs to the ambient pattern, which the policy keeps: a space in
            // the Windows (NLS) culture data and a narrow no-break space (U+202F) in the ICU data of Linux and macOS.
            Assert.Contains(
                CrmHrPresentationCulture.FormatShortTimestamp(afternoon),
                new[] { "3/9/2026 2:05 PM", "3/9/2026 2:05 PM" });
        }

        using (new AmbientCulture("el-GR"))
        {
            // Greek uses a 12-hour clock with Greek designators; the designator is English, the pattern stays Greek.
            var formatted = CrmHrPresentationCulture.FormatShortTimestamp(afternoon);
            Assert.EndsWith("PM", formatted, StringComparison.Ordinal);
            Assert.DoesNotContain("μ", formatted, StringComparison.Ordinal);
            Assert.StartsWith("9/3/2026", formatted, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Activity_timestamps_go_through_the_same_policy()
    {
        using var scope = new AmbientCulture("el-GR");
        var occurred = new DateTimeOffset(new DateTime(2026, 3, 9, 14, 5, 0, DateTimeKind.Local));

        Assert.Equal(
            CrmHrPresentationCulture.FormatShortTimestamp(occurred.LocalDateTime),
            CrmHrActivityText.FormatTimestamp(occurred));
        Assert.EndsWith("PM", CrmHrActivityText.FormatTimestamp(occurred), StringComparison.Ordinal);
    }

    [Fact]
    public void The_policy_never_changes_the_ambient_culture()
    {
        using var scope = new AmbientCulture("cs-CZ");

        _ = CrmHrFinancialsText.PeriodLabel(new DateOnly(2025, 1, 1), CrmHrFinancialPeriod.Month);
        _ = CrmHrPresentationCulture.FormatShortTimestamp(DateTime.Now);

        Assert.Equal("cs-CZ", CultureInfo.CurrentCulture.Name);
        Assert.Equal("cs-CZ", CultureInfo.CurrentUICulture.Name);
        // Amounts keep the ambient number format; only interface words are covered by the policy.
        Assert.Equal(1234.5m.ToString("N2", CultureInfo.GetCultureInfo("cs-CZ")) + " EUR", CrmHrFinancialsText.FormatAmount(new CrmHrCurrencyTotal("EUR", 1234.5m)));
    }

    // Sets the ambient culture for the current test flow only and restores the previous one on disposal.
    private sealed class AmbientCulture : IDisposable
    {
        private readonly CultureInfo previousCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;

        public AmbientCulture(string name)
        {
            var culture = CultureInfo.GetCultureInfo(name);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }
}
