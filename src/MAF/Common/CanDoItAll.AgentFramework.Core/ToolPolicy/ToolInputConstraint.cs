using System.Globalization;

namespace CanDoItAll.AgentFramework.Core;

public abstract record ToolInputConstraint(string Parameter) {
    public abstract string Description { get; }
    public abstract bool Accepts(IReadOnlyDictionary<string, string> arguments);
}

public sealed record ToolIntegerInputConstraint(string Parameter, int Maximum, bool Required,
    string? WhenTrue = null) : ToolInputConstraint(Parameter) {
    public override string Description => FormattableString.Invariant(
        $"{Parameter} must be an integer at most {Maximum}{(WhenTrue is null ? Required ? "; it is required" : string.Empty : $" when {WhenTrue}=true")}.");

    public override bool Accepts(IReadOnlyDictionary<string, string> arguments) {
        if (WhenTrue is not null && (!arguments.TryGetValue(WhenTrue, out var condition) ||
            !bool.TryParse(condition, out var enabled) || !enabled)) {
            return true;
        }
        return arguments.TryGetValue(Parameter, out var value)
            ? int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer) && integer <= Maximum
            : !Required;
    }
}

public sealed record ToolBooleanInputConstraint(string Parameter, bool AllowedValue) : ToolInputConstraint(Parameter) {
    public override string Description => $"{Parameter} must be {AllowedValue.ToString().ToLowerInvariant()} or omitted.";

    public override bool Accepts(IReadOnlyDictionary<string, string> arguments) =>
        !arguments.TryGetValue(Parameter, out var value) || bool.TryParse(value, out var boolean) && boolean == AllowedValue;
}
