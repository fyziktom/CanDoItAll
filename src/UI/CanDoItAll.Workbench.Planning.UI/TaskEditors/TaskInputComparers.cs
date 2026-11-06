using System.Globalization;

namespace CanDoItAll.Workbench.Planning.UI;

internal static class TaskInputComparers {
    public static IEqualityComparer<string> Date { get; } = By<string>(DateKey);
    public static IEqualityComparer<string> Number { get; } = By<string>(NumberKey);
    public static IEqualityComparer<TaskEffortInput> Effort { get; } = By<TaskEffortInput>(value =>
        (NumberKey(value.Amount), value.Unit.ToUpperInvariant()));
    public static IEqualityComparer<TaskMoneyInput> Money { get; } = By<TaskMoneyInput>(value =>
        (NumberKey(value.Amount), value.Currency.Trim().ToUpperInvariant()));
    public static IEqualityComparer<TaskDeliveryInput> Delivery { get; } = By<TaskDeliveryInput>(value =>
        (DateKey(value.Start), DateKey(value.End), NumberKey(value.Duration)));
    public static IEqualityComparer<TaskExecutionInputText> Execution { get; } = By<TaskExecutionInputText>(value =>
        (value.State, DateKey(value.Started), DateKey(value.Ended)));

    private static string NumberKey(string value) => decimal.TryParse(value, NumberStyles.Number,
        CultureInfo.InvariantCulture, out var parsed) ? parsed.ToString("G29", CultureInfo.InvariantCulture) : value;

    private static string DateKey(string value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
        ? parsed.ToString("O", CultureInfo.InvariantCulture) : value;

    private static IEqualityComparer<T> By<T>(Func<T, object> key) => EqualityComparer<T>.Create(
        (left, right) => left is not null && right is not null ? Equals(key(left), key(right)) : left is null && right is null,
        value => value is null ? 0 : key(value).GetHashCode());
}
