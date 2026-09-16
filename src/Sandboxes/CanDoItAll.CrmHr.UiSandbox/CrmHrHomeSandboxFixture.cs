using CanDoItAll.CrmHr.UI.Home;

namespace CanDoItAll.CrmHr.UiSandbox;

public enum CrmHrHomeSandboxScenario
{
    Populated,
    Loading,
    Empty,
    Failed,
    Sensitive,
    LongText,
    NullOptionals,
    LargeTotals
}

public enum CrmHrHomeSandboxLayout
{
    Matched,
    Narrow,
    Flexible
}

public sealed record CrmHrHomeSandboxContext(
    CrmHrHomeSandboxScenario Scenario = CrmHrHomeSandboxScenario.Populated,
    CrmHrHomeSandboxLayout Layout = CrmHrHomeSandboxLayout.Matched)
{
    public static CrmHrHomeSandboxContext Parse(string? scenario, string? layout)
        => new(ParseScenario(scenario), ParseLayout(layout));

    public static CrmHrHomeSandboxScenario ParseScenario(string? token)
        => Enum.GetValues<CrmHrHomeSandboxScenario>()
            .FirstOrDefault(scenario => string.Equals(Token(scenario), token?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static CrmHrHomeSandboxLayout ParseLayout(string? token)
        => token?.Trim().ToLowerInvariant() switch
        {
            "narrow" => CrmHrHomeSandboxLayout.Narrow,
            "flexible" => CrmHrHomeSandboxLayout.Flexible,
            _ => CrmHrHomeSandboxLayout.Matched
        };

    public static string Token(CrmHrHomeSandboxScenario scenario)
        => string.Concat(scenario.ToString().Select((character, index) =>
            char.IsUpper(character) && index > 0 ? $"-{char.ToLowerInvariant(character)}" : char.ToLowerInvariant(character).ToString()));

    public static string Token(CrmHrHomeSandboxLayout layout) => layout.ToString().ToLowerInvariant();

    public IReadOnlyDictionary<string, object?> ToQuery() => new Dictionary<string, object?>
    {
        ["scenario"] = Token(Scenario),
        ["layout"] = Token(Layout)
    };
}

// Deterministic local state for the real Home surface; no query service, timer or persistence is involved. Every
// value is synthetic: no real person, organization or confidential note appears here.
public sealed class CrmHrHomeSandboxFixture
{
    private static readonly Guid AccountId = Guid.Parse("51000000-0000-0000-0000-000000000001");
    private static readonly Guid OwnerId = Guid.Parse("51000000-0000-0000-0000-000000000002");
    private static readonly Guid SecondAccountId = Guid.Parse("51000000-0000-0000-0000-000000000003");

    private CrmHrHomeSandboxScenario scenario = CrmHrHomeSandboxScenario.Populated;
    private long generation;
    private bool retried;

    public CrmHrHomePresentation Presentation { get; private set; } = CrmHrHomePresentation.CreateReady(0, Populated());

    public string IntentLog { get; private set; } = "No intent yet.";

    public void Apply(CrmHrHomeSandboxScenario next)
    {
        scenario = next;
        retried = false;
        IntentLog = "No intent yet.";
        Presentation = Build(++generation);
    }

    // The surface's intents only update the intent line; a retry of the failed scenario resolves to the populated
    // overview so the retry path can be observed without any backend.
    public void Handle(CrmHrHomeIntent intent)
    {
        switch (intent)
        {
            case CrmHrHomeIntent.Navigate navigate:
                IntentLog = $"Navigate: {navigate.Destination}";
                break;
            case CrmHrHomeIntent.OpenOpportunity open:
                IntentLog = $"Open opportunity: account {open.AccountPartyId:D}, opportunity {open.OpportunityId:D}";
                break;
            case CrmHrHomeIntent.Retry retry when retry.Generation == Presentation.Generation && scenario == CrmHrHomeSandboxScenario.Failed:
                IntentLog = $"Retry: generation {retry.Generation}";
                retried = true;
                Presentation = CrmHrHomePresentation.CreateReady(++generation, Populated());
                break;
            case CrmHrHomeIntent.Retry retry:
                IntentLog = $"Retry ignored: generation {retry.Generation}";
                break;
        }
    }

    private CrmHrHomePresentation Build(long current)
        => scenario switch
        {
            CrmHrHomeSandboxScenario.Loading => CrmHrHomePresentation.CreateLoading(current),
            CrmHrHomeSandboxScenario.Empty => CrmHrHomePresentation.CreateReady(current, CrmHrHomeOverview.Empty),
            CrmHrHomeSandboxScenario.Failed when !retried => CrmHrHomePresentation.CreateFailed(
                current,
                "The CRM / HR overview could not be loaded. Retry, or open the directory directly."),
            CrmHrHomeSandboxScenario.Sensitive => CrmHrHomePresentation.CreateReady(current, SensitiveHeavy()),
            CrmHrHomeSandboxScenario.LongText => CrmHrHomePresentation.CreateReady(current, LongText()),
            CrmHrHomeSandboxScenario.NullOptionals => CrmHrHomePresentation.CreateReady(current, NullOptionals()),
            CrmHrHomeSandboxScenario.LargeTotals => CrmHrHomePresentation.CreateReady(current, LargeTotals()),
            _ => CrmHrHomePresentation.CreateReady(current, Populated())
        };

    public static CrmHrHomeOverview Populated()
        => new(
            new CrmHrHomeTotals(Parties: 12, Organizations: 4, Opportunities: 5, WorkforceProfiles: 6, AgentProjections: 2, SensitiveRecords: 1),
            [
                Directory("Aurora Logistics", "Organization", "Active", "Regional logistics account with two delivery units."),
                Directory("Bram Vos", "Person", "Active", "Account manager for the northern region."),
                Directory("Cobalt Analytics Unit", "OrganizationUnit", "Active", null),
                Directory("Dana Reyes", "Person", "Candidate", "Senior data engineer in the recruiting pipeline.", isSensitive: true),
                Directory("Eastfield Advisory", "Organization", "Prospect", "Advisory prospect from the partner referral.")
            ],
            [Sensitive("Dana Reyes", "Person", "Candidate")],
            [
                Opportunity("Warehouse automation renewal", "Aurora Logistics", "Bram Vos", "Proposal", "Renewal", 48000m, 65),
                Opportunity("Analytics onboarding", "Eastfield Advisory", "Bram Vos", "Qualified", "Partner", null, 30),
                Opportunity("Support extension", "Aurora Logistics", "Bram Vos", "Negotiation", "Upsell", 12500.5m, 80)
            ]);

    private static CrmHrHomeOverview SensitiveHeavy()
        => new(
            new CrmHrHomeTotals(Parties: 9, Organizations: 2, Opportunities: 1, WorkforceProfiles: 4, AgentProjections: 0, SensitiveRecords: 5),
            [
                Directory("Aurora Logistics", "Organization", "Active", "Regional logistics account."),
                Directory("Dana Reyes", "Person", "Candidate", "Senior data engineer in the recruiting pipeline.", isSensitive: true),
                Directory("Felix Ahmed", "Person", "Active", "People operations partner.", isSensitive: true),
                Directory("Grace Lindqvist", "Person", "Former", "Former contractor.", isSensitive: true)
            ],
            [
                Sensitive("Dana Reyes", "Person", "Candidate"),
                Sensitive("Felix Ahmed", "Person", "Active"),
                Sensitive("Grace Lindqvist", "Person", "Former")
            ],
            [Opportunity("Compliance review", "Aurora Logistics", "Bram Vos", "Identified", "Direct", 9000m, 20)]);

    // Untrusted text: markup-looking names and summaries must render as text, and very long values must wrap.
    private static CrmHrHomeOverview LongText()
        => new(
            new CrmHrHomeTotals(Parties: 3, Organizations: 1, Opportunities: 2, WorkforceProfiles: 1, AgentProjections: 1, SensitiveRecords: 1),
            [
                Directory("<script>alert('directory')</script> Untrusted & Co", "Organization", "Active", "<b>Bold</b> summary with markup that must stay text."),
                Directory(new string('N', 140), "Person", "Active", string.Concat(Enumerable.Repeat("A long unbroken summary segment ", 12))),
                Directory("Ünïcode Näme with emoji 🚀", "OrganizationUnit", "Draft", "Diacritics and symbols are ordinary text.", isSensitive: true)
            ],
            [Sensitive("<img src=x onerror=alert(1)> Sensitive", "Person", "Active")],
            [
                Opportunity(new string('T', 120), "<script>alert('account')</script>", "<i>Owner</i>", "Proposal", "Direct", 123456789.99m, 100),
                Opportunity("Tiny deal", string.Concat(Enumerable.Repeat("VeryLongAccountName", 6)), "Owner", "Identified", "Partner", 0.5m, 0)
            ]);

    private static CrmHrHomeOverview NullOptionals()
        => new(
            new CrmHrHomeTotals(Parties: 2, Organizations: 1, Opportunities: 1, WorkforceProfiles: 0, AgentProjections: 0, SensitiveRecords: 0),
            [
                Directory("Harbor Freight Partners", "Organization", "Active", null),
                Directory("Ines Kowalski", "Person", "Inactive", "   ")
            ],
            [],
            [Opportunity("Discovery call", "Harbor Freight Partners", "Unknown owner", "Identified", "Direct", null, 0)]);

    // Totals far above the preview caps: the header and the sensitive badge must show totals, not preview lengths.
    private static CrmHrHomeOverview LargeTotals()
        => new(
            new CrmHrHomeTotals(Parties: 1284, Organizations: 311, Opportunities: 97, WorkforceProfiles: 542, AgentProjections: 18, SensitiveRecords: 41),
            Enumerable.Range(1, 5).Select(index => Directory($"Party {index:00}", index % 2 == 0 ? "Organization" : "Person", "Active", $"Summary {index}")).ToArray(),
            Enumerable.Range(1, 3).Select(index => Sensitive($"Sensitive {index:00}", "Person", "Active")).ToArray(),
            Enumerable.Range(1, 6).Select(index => Opportunity($"Opportunity {index:00}", "Aurora Logistics", "Bram Vos", "Qualified", "Direct", index * 1000m, index * 10)).ToArray());

    private static CrmHrHomeDirectoryEntry Directory(string name, string type, string lifecycle, string? summary, bool isSensitive = false)
        => new(DeterministicId(name), name, type, lifecycle, summary, isSensitive);

    private static CrmHrHomeSensitiveEntry Sensitive(string name, string type, string lifecycle)
        => new(DeterministicId(name), name, type, lifecycle);

    private static CrmHrHomeOpportunityEntry Opportunity(string title, string account, string owner, string stage, string source, decimal? amount, int probability)
        => new(DeterministicId(title), account.Contains("Aurora", StringComparison.Ordinal) ? AccountId : SecondAccountId, title, account, owner, stage, source, amount, probability);

    // A stable identity per name (FNV-1a over the UTF-16 code units, folded into both Guid halves), so scenario rows
    // keep the same ids across renders and processes without any cryptographic dependency.
    private static Guid DeterministicId(string seed)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var high = offset;
        var low = offset ^ 0x5A5A5A5A5A5A5A5AUL;
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
