using CanDoItAll.CrmHr.UI.Accounts;
using CanDoItAll.CrmHr.UI.Activity;

namespace CanDoItAll.CrmHr.UiSandbox;

public enum CrmHrAccountActivitySandboxScenario
{
    Populated,
    NoAccount,
    ActiveCustomer,
    Loading,
    Empty,
    Overdue,
    LongText,
    NullContacts,
    ManyPages
}

// The three production compositions of the timeline: each host supplies its own wording and test identifier.
public enum CrmHrActivitySandboxHost
{
    Crm,
    Directory,
    Workforce
}

public sealed record CrmHrAccountActivitySandboxContext(
    CrmHrAccountActivitySandboxScenario Scenario = CrmHrAccountActivitySandboxScenario.Populated,
    CrmHrHomeSandboxLayout Layout = CrmHrHomeSandboxLayout.Matched)
{
    public static CrmHrAccountActivitySandboxContext Parse(string? scenario, string? layout)
        => new(ParseScenario(scenario), CrmHrHomeSandboxContext.ParseLayout(layout));

    public static CrmHrAccountActivitySandboxScenario ParseScenario(string? token)
        => Enum.GetValues<CrmHrAccountActivitySandboxScenario>()
            .FirstOrDefault(scenario => string.Equals(Token(scenario), token?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Token(CrmHrAccountActivitySandboxScenario scenario)
        => string.Concat(scenario.ToString().Select((character, index) =>
            char.IsUpper(character) && index > 0 ? $"-{char.ToLowerInvariant(character)}" : char.ToLowerInvariant(character).ToString()));

    public IReadOnlyDictionary<string, object?> ToQuery() => new Dictionary<string, object?>
    {
        ["scenario"] = Token(Scenario),
        ["layout"] = CrmHrHomeSandboxContext.Token(Layout)
    };
}

// Deterministic local state for the real account summary and the three timeline compositions; no query service,
// timer or persistence is involved. Every value is synthetic: no real person, organization, contact detail or
// confidential note appears here.
public sealed class CrmHrAccountActivitySandboxFixture
{
    public const int PageSize = CrmHrActivityPage.DefaultPageSize;

    private static readonly Guid AccountId = Guid.Parse("52000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset BaseUtc = new(2026, 3, 9, 14, 30, 0, TimeSpan.Zero);

    public static CrmHrActivityCopy DirectoryCopy { get; } = new(
        "Party history",
        "Recent interactions, changes, and follow-ups",
        "Directory edits, CRM interactions, recruiting state changes, and workforce or AI updates stay visible in one place for the selected party.",
        "No party activity",
        "No saved activity for this party",
        "Save the party and related CRM-HR workflows to start building a shared history.");

    public static CrmHrActivityCopy WorkforceCopy { get; } = new(
        "Workforce history",
        "Recent staffing, lifecycle, and audit activity",
        "Workforce profile saves, archive/reactivate events, recruiting transitions, and related CRM-HR changes stay visible here for the selected party.",
        "No workforce history",
        "No saved history for this record",
        "Save a workforce profile or related CRM-HR workflow to build the shared audit trail.");

    private readonly Dictionary<CrmHrActivitySandboxHost, int> pageIndexes = new();
    private CrmHrAccountActivitySandboxScenario scenario = CrmHrAccountActivitySandboxScenario.Populated;
    private IReadOnlyList<CrmHrActivityEntry> entries = PopulatedEntries();
    private int actionCount;
    private int overdueCount;

    public CrmHrAccountSummary? Account { get; private set; } = PopulatedAccount();

    public bool IsLoading { get; private set; }

    public string IntentLog { get; private set; } = "No intent yet.";

    public static string TestId(CrmHrActivitySandboxHost host)
        => host switch
        {
            CrmHrActivitySandboxHost.Directory => "crmhr-directory-activity",
            CrmHrActivitySandboxHost.Workforce => "crmhr-workforce-history",
            _ => "crmhr-account-activity"
        };

    public static CrmHrActivityCopy Copy(CrmHrActivitySandboxHost host)
        => host switch
        {
            CrmHrActivitySandboxHost.Directory => DirectoryCopy,
            CrmHrActivitySandboxHost.Workforce => WorkforceCopy,
            _ => CrmHrActivityCopy.Default
        };

    public void Apply(CrmHrAccountActivitySandboxScenario next)
    {
        scenario = next;
        pageIndexes.Clear();
        IntentLog = "No intent yet.";
        IsLoading = next == CrmHrAccountActivitySandboxScenario.Loading;
        Account = next switch
        {
            CrmHrAccountActivitySandboxScenario.NoAccount => null,
            CrmHrAccountActivitySandboxScenario.ActiveCustomer => PopulatedAccount() with
            {
                RelationshipStageLabel = "ActiveCustomer",
                RelationshipStageTone = CrmHrAccountTone.Success,
                CanConvertToActiveCustomer = false
            },
            CrmHrAccountActivitySandboxScenario.LongText => LongTextAccount(),
            CrmHrAccountActivitySandboxScenario.NullContacts => NullContactsAccount(),
            _ => PopulatedAccount()
        };
        entries = next switch
        {
            CrmHrAccountActivitySandboxScenario.Empty or CrmHrAccountActivitySandboxScenario.NoAccount or CrmHrAccountActivitySandboxScenario.Loading => [],
            CrmHrAccountActivitySandboxScenario.Overdue => OverdueEntries(),
            CrmHrAccountActivitySandboxScenario.LongText => LongTextEntries(),
            CrmHrAccountActivitySandboxScenario.ManyPages => ManyPagesEntries(),
            _ => PopulatedEntries()
        };
        actionCount = entries.Count(entry => entry.Meta.Contains("Next action", StringComparison.Ordinal));
        overdueCount = entries.Count(entry => entry.IsOverdue);
    }

    // The page a host shows: the whole-history counts plus the requested slice, exactly as the query owner pages it.
    public CrmHrActivityPage Page(CrmHrActivitySandboxHost host)
    {
        var pageIndex = pageIndexes.GetValueOrDefault(host);
        return new CrmHrActivityPage(
            entries.Skip(pageIndex * PageSize).Take(PageSize).ToArray(),
            pageIndex,
            PageSize,
            entries.Count,
            actionCount,
            overdueCount);
    }

    public void HandleAccount(CrmHrAccountSummaryIntent intent)
    {
        switch (intent)
        {
            case CrmHrAccountSummaryIntent.OpenDirectory open:
                IntentLog = open.AccountPartyId is { } id ? $"Open directory: account {id:D}" : "Open directory: no account";
                break;
            case CrmHrAccountSummaryIntent.ConvertToActiveCustomer convert when Account?.AccountPartyId == convert.AccountPartyId:
                // The production page saves and reloads; the sandbox shows the reloaded state locally.
                IntentLog = $"Convert to active customer: account {convert.AccountPartyId:D}";
                Account = Account with
                {
                    RelationshipStageLabel = "ActiveCustomer",
                    RelationshipStageTone = CrmHrAccountTone.Success,
                    CanConvertToActiveCustomer = false
                };
                break;
            case CrmHrAccountSummaryIntent.ConvertToActiveCustomer convert:
                IntentLog = $"Convert ignored: account {convert.AccountPartyId:D} is not shown";
                break;
        }
    }

    public void HandleActivity(CrmHrActivitySandboxHost host, CrmHrActivityIntent intent)
    {
        switch (intent)
        {
            case CrmHrActivityIntent.RequestPage request when request.PageIndex >= 0 && request.PageIndex < Page(host).TotalPages:
                pageIndexes[host] = request.PageIndex;
                IntentLog = $"Request page: {host} {request.PageIndex + 1}";
                break;
            case CrmHrActivityIntent.RequestPage request:
                IntentLog = $"Request page ignored: {host} {request.PageIndex + 1}";
                break;
        }
    }

    public static CrmHrAccountSummary PopulatedAccount()
        => new(
            AccountId,
            "Aurora Logistics",
            "Regional logistics account with two delivery units and a renewal in negotiation.",
            "Prospect",
            CrmHrAccountTone.Info,
            "Active",
            CrmHrAccountTone.Success,
            "accounts@aurora.example",
            "+1 555 0100",
            ["Customer", "Partner"],
            ConnectionCount: 3,
            OpportunityCount: 2,
            TagCount: 4,
            CanConvertToActiveCustomer: true);

    private static CrmHrAccountSummary LongTextAccount()
        => new(
            AccountId,
            "<script>alert('account')</script> " + new string('A', 120),
            string.Concat(Enumerable.Repeat("A long unbroken summary segment ", 14)) + "<b>markup stays text</b>",
            "DormantCustomer",
            CrmHrAccountTone.Warning,
            "Former",
            CrmHrAccountTone.Warning,
            new string('e', 60) + "@example.test",
            "+1 555 0100 ext. 123456789012345",
            ["Customer", "CustomerContact", "Partner", "Vendor", "DeliveryUnit", "AccountManager"],
            ConnectionCount: 1284,
            OpportunityCount: 311,
            TagCount: 97,
            CanConvertToActiveCustomer: true);

    private static CrmHrAccountSummary NullContactsAccount()
        => new(
            AccountId,
            "Harbor Freight Partners",
            null,
            "LostCustomer",
            CrmHrAccountTone.Danger,
            "Inactive",
            CrmHrAccountTone.Warning,
            null,
            null,
            [],
            ConnectionCount: 0,
            OpportunityCount: 0,
            TagCount: 0,
            CanConvertToActiveCustomer: true);

    public static IReadOnlyList<CrmHrActivityEntry> PopulatedEntries()
        =>
        [
            Interaction(0, "Quarterly review", "Confirmed the renewal scope and open risks.", "Meeting / Bram Vos, Dana Reyes / Next action due 2026-03-20", CrmHrActivityTone.Info),
            Interaction(1, "Pricing call", "Walked through the revised statement of work.", "Call / Bram Vos", CrmHrActivityTone.Success),
            Audit(2, "CRM account profile saved", "Updated", "crm-hr-ui"),
            Interaction(3, "Invoice reminder", "Sent the overdue invoice reminder.", "Email / Finance desk / Next action due 2026-02-01", CrmHrActivityTone.Neutral, isOverdue: true),
            Interaction(4, "Chat follow-up", null, "Message / Bram Vos", CrmHrActivityTone.Warning),
            Audit(5, "Company connections saved", "Updated", "crm-hr-ui"),
            Interaction(6, "Kickoff workshop", "Agreed the delivery units and the handoff path.", "Meeting / Bram Vos / Next action due 2026-03-28", CrmHrActivityTone.Info),
            Audit(7, "Sensitive record reviewed", "Reviewed", "compliance", isSensitive: true),
            Interaction(8, "Reference call", "Reference for the analytics onboarding.", "Call / Eastfield Advisory", CrmHrActivityTone.Success),
            Interaction(9, "Contract questions", "Clarified the data residency clause.", "Email / Legal desk", CrmHrActivityTone.Neutral),
            Audit(10, "CRM account created", "Created", "crm-hr-ui"),
            Interaction(11, "Discovery call", "First conversation about warehouse automation.", "Call / Bram Vos", CrmHrActivityTone.Success)
        ];

    private static IReadOnlyList<CrmHrActivityEntry> OverdueEntries()
        => Enumerable.Range(0, 7)
            .Select(index => Interaction(
                index,
                $"Follow-up {index + 1:00}",
                "The next action is past its due date.",
                $"Email / Owner {index + 1} / Next action due 2026-01-{index + 1:00}",
                CrmHrActivityTone.Neutral,
                isOverdue: index % 2 == 0))
            .ToArray();

    // Untrusted text: markup-looking titles and very long values must render as text and wrap inside the row.
    private static IReadOnlyList<CrmHrActivityEntry> LongTextEntries()
        =>
        [
            Interaction(0, "<script>alert('activity')</script> Untrusted title", "<b>Bold</b> description with markup that must stay text.", "Meeting / <i>Participant</i>", CrmHrActivityTone.Info),
            Interaction(1, new string('T', 160), string.Concat(Enumerable.Repeat("A long unbroken description segment ", 12)), new string('M', 90) + " / Next action due 2026-03-20", CrmHrActivityTone.Warning, isOverdue: true),
            Audit(2, "Ünïcode audit with emoji 🚀", "Reviewed", "ops-desk")
        ];

    private static IReadOnlyList<CrmHrActivityEntry> ManyPagesEntries()
        => Enumerable.Range(0, 47)
            .Select(index => index % 3 == 2
                ? Audit(index, $"Audit entry {index + 1:00}", "Updated", "crm-hr-ui")
                : Interaction(index, $"Interaction {index + 1:00}", $"Summary of interaction {index + 1:00}.", index % 5 == 0 ? "Call / Owner / Next action due 2026-04-01" : "Email / Owner", index % 2 == 0 ? CrmHrActivityTone.Success : CrmHrActivityTone.Neutral))
            .ToArray();

    private static CrmHrActivityEntry Interaction(int index, string title, string? description, string meta, CrmHrActivityTone tone, bool isOverdue = false)
        => new(DeterministicId($"interaction-{index}"), "Interaction", title, description, meta, BaseUtc.AddHours(-index * 7), tone, isOverdue);

    private static CrmHrActivityEntry Audit(int index, string title, string action, string actor, bool isSensitive = false)
        => new(DeterministicId($"audit-{index}"), "Audit", title, action, actor, BaseUtc.AddHours(-index * 7), isSensitive ? CrmHrActivityTone.Warning : CrmHrActivityTone.Neutral, IsOverdue: false);

    // A stable identity per seed (FNV-1a over the UTF-16 code units, folded into both Guid halves), so scenario rows
    // keep the same ids across renders and processes without any cryptographic dependency.
    private static Guid DeterministicId(string seed)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var high = offset;
        var low = offset ^ 0x3C3C3C3C3C3C3C3CUL;
        foreach (var character in seed)
        {
            high = (high ^ character) * prime;
            low = (low ^ (uint)(character * 31)) * prime;
        }

        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes[..8], high);
        BitConverter.TryWriteBytes(bytes[8..], low);
        return new Guid(bytes);
    }
}
