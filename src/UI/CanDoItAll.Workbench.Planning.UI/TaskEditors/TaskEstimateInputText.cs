using System.Globalization;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Workbench.Planning.UI;

public sealed record TaskEstimateInputText(string Effort, string Unit, string Cost, string Currency) {
    public ProjectTaskEstimate Parse(decimal hoursPerManDay = ProjectTaskEstimatePolicy.DefaultHoursPerManDay) {
        if (!Enum.TryParse<ProjectWorkItemEffortUnit>(Unit, true, out var unit) || !Enum.IsDefined(unit)) {
            throw new InvalidOperationException("Select a supported task effort unit.");
        }
        return ProjectTaskEstimatePolicy.Create(ParseNumber(Effort, "Expected task effort"), unit,
            ParseNumber(Cost, "Expected task cost"), Currency, hoursPerManDay);
    }

    private static decimal? ParseNumber(string text, string name) {
        if (string.IsNullOrWhiteSpace(text)) {
            return null;
        }
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value : throw new FormatException($"{name} must be a number.");
    }
}
