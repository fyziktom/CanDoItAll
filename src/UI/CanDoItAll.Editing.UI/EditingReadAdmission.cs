namespace CanDoItAll.Editing.UI;

public sealed record EditingReadAdmission(Func<CancellationToken, ValueTask> AuthorizeAsync);
