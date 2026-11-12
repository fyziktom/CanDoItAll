using System.Globalization;
using CanDoItAll.AppComponents.Navigation;

namespace CanDoItAll.Modules.Workbench.Pages;

public enum ProjectCalendarView {
    Day,
    Week,
    Month,
    List,
    Year
}

// An absent view or date falls back to the persisted calendar preference, then to the product default.
public sealed record ProjectCalendarUrlState(ProjectCalendarView? View = null, DateOnly? Date = null, Guid? EventId = null);

public sealed class ProjectCalendarUrlCodec : IUrlStateCodec<ProjectCalendarUrlState> {
    public const string ViewKey = "view";
    public const string DateKey = "date";
    public const string EventKey = "eventId";

    public static ProjectCalendarUrlCodec Instance { get; } = new();

    public static UrlTokenMap<ProjectCalendarView> Views { get; } = new(
        (ProjectCalendarView.Day, "day"),
        (ProjectCalendarView.Week, "week"),
        (ProjectCalendarView.Month, "month"),
        (ProjectCalendarView.List, "list"),
        (ProjectCalendarView.Year, "year"));

    public IReadOnlyList<string> Keys { get; } = [ViewKey, DateKey, EventKey];

    public ProjectCalendarUrlState Read(UrlQuery query)
        => new(query.GetToken(ViewKey, Views), query.GetDate(DateKey), query.GetId(EventKey));

    public void Write(ProjectCalendarUrlState state, UrlQueryWriter query) {
        query.Token(ViewKey, state.View, Views);
        query.Date(DateKey, state.Date);
        query.Id(EventKey, state.EventId);
    }

    public static ProjectCalendarView? ParseView(string? view) => Views.Parse(view);

    public static DateOnly? ParseDate(string? date)
        => DateOnly.TryParseExact(date?.Trim(), UrlFormats.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;

    public static string FormatDate(DateOnly date) => date.ToString(UrlFormats.Date, CultureInfo.InvariantCulture);
}
