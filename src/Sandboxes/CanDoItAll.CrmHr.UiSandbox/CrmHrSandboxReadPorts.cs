using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.CrmHr.UiSandbox;

// One shared synthetic party record used to project into the five read-port result shapes below. Every name, email
// and title in this file is fictitious; nothing here reaches a real person, organization or agent context.
public sealed record CrmHrSandboxPartyRecord(
    Guid Id,
    string DisplayName,
    PartyType PartyType,
    PartyLifecycleStatus LifecycleStatus,
    string ExternalCode,
    string Summary,
    IReadOnlyList<string> Tags,
    bool IsSensitive,
    string PrimaryEmail,
    string PrimaryPhone,
    WorkforceRecordClassification? Classification,
    string JobTitle,
    string Discipline,
    string Seniority,
    string Location,
    string SkillSummary,
    WorkforceAvailabilityState? AvailabilityState,
    decimal AvailablePercent,
    DateOnly? ContractEndDate,
    DateOnly? NextAvailabilityOn,
    DateTimeOffset UpdatedAtUtc);

public sealed record CrmHrSandboxProjectRecord(
    Guid Id,
    Guid LifetimeId,
    string Name,
    ProjectStatus Status,
    string CurrentPhase,
    string Description,
    DateTimeOffset UpdatedAtUtc);

public sealed record CrmHrSandboxOpportunityRecord(
    Guid Id,
    Guid AccountPartyId,
    string Title,
    OpportunityStage Stage,
    OpportunitySource Source,
    Guid OwnerPartyId,
    Guid? DeliveryUnitPartyId,
    string CurrencyCode,
    decimal? Amount,
    int ProbabilityPercent,
    DateOnly? ExpectedCloseOn,
    Guid? LinkedProjectId,
    DateTimeOffset UpdatedAtUtc);

// The shared synthetic data set: about 30 parties of mixed types, 8 projects, opportunities for one account and
// organization affiliations for two people. Read-only for every workspace sandbox view in this project.
public static class CrmHrSandboxData
{
    public static Guid Id(string seed) => CrmHrWorkspaceSandboxIds.Id(seed);

    public static readonly Guid NorthwindLogistics = Id("northwind-logistics");
    public static readonly Guid BeaconHillAdvisory = Id("beacon-hill-advisory");
    public static readonly Guid CascadeRobotics = Id("cascade-robotics");
    public static readonly Guid SolaceFinancialGroup = Id("solace-financial-group");

    public static readonly Guid NorthwindFulfillmentUnit = Id("northwind-fulfillment-unit");
    public static readonly Guid NorthwindSupportUnit = Id("northwind-support-unit");
    public static readonly Guid CascadeRnDUnit = Id("cascade-rnd-unit");

    public static readonly Guid ElenaWard = Id("elena-ward");
    public static readonly Guid MarcusChen = Id("marcus-chen");
    public static readonly Guid PriyaNatarajan = Id("priya-natarajan");
    public static readonly Guid OliverBennett = Id("oliver-bennett");
    public static readonly Guid SofiaRamirez = Id("sofia-ramirez");
    public static readonly Guid DerekHolt = Id("derek-holt");
    public static readonly Guid GraceKim = Id("grace-kim");
    public static readonly Guid NoahFischer = Id("noah-fischer");
    public static readonly Guid IvyChapman = Id("ivy-chapman");
    public static readonly Guid LucasMeyer = Id("lucas-meyer");
    public static readonly Guid AvaThompson = Id("ava-thompson");
    public static readonly Guid JonasKeller = Id("jonas-keller");
    public static readonly Guid MiaTorres = Id("mia-torres");
    public static readonly Guid LiamOConnor = Id("liam-oconnor");
    public static readonly Guid NoraPatel = Id("nora-patel");
    public static readonly Guid EthanBrooks = Id("ethan-brooks");
    public static readonly Guid ChloeDubois = Id("chloe-dubois");
    public static readonly Guid VictorHughes = Id("victor-hughes");

    public static readonly Guid AtlasOpsAgent = Id("atlas-ops-agent");
    public static readonly Guid LedgerInsightsAgent = Id("ledger-insights-agent");
    public static readonly Guid SupportCopilotAgent = Id("support-copilot-agent");
    public static readonly Guid SchedulerAgent = Id("scheduler-agent");
    public static readonly Guid RecruitingScoutAgent = Id("recruiting-scout-agent");

    public static readonly Guid ProjectWarehouseAutomation = Id("project-warehouse-automation");
    public static readonly Guid ProjectFulfillmentRollout = Id("project-fulfillment-rollout");
    public static readonly Guid ProjectBeaconHillEngagement = Id("project-beacon-hill-engagement");
    public static readonly Guid ProjectCascadeRnDSprint = Id("project-cascade-rnd-sprint");
    public static readonly Guid ProjectPlatformModernization = Id("project-platform-modernization");
    public static readonly Guid ProjectSolaceMigration = Id("project-solace-migration");
    public static readonly Guid ProjectSupportDeskRevamp = Id("project-support-desk-revamp");
    public static readonly Guid ProjectArchivedPilot = Id("project-archived-pilot");

    public static readonly Guid OpportunityWarehouseRenewal = Id("opportunity-warehouse-renewal");
    public static readonly Guid OpportunityFulfillmentExpansion = Id("opportunity-fulfillment-expansion");
    public static readonly Guid OpportunitySupportUpsell = Id("opportunity-support-upsell");

    // A party id no read port ever returns: used by the workspace sandbox views to build "unavailable reference"
    // scenarios (a saved reference to a party that has since disappeared from every catalog).
    public static readonly Guid MissingParty = Id("missing-party-reference");
    public static readonly Guid MissingProject = Id("missing-project-reference");

    public static readonly DateTimeOffset BaseUpdatedAtUtc = new(2026, 3, 9, 14, 30, 0, TimeSpan.Zero);

    // A named-argument factory instead of long positional constructor calls: the 21-field record above is easy to
    // miscount positionally, so every optional field here defaults to the "not applicable" value for that field.
    private static CrmHrSandboxPartyRecord Party(
        Guid id, string displayName, PartyType partyType, PartyLifecycleStatus lifecycleStatus, string externalCode,
        string summary, IReadOnlyList<string> tags, bool isSensitive = false, string primaryEmail = "", string primaryPhone = "",
        WorkforceRecordClassification? classification = null, string jobTitle = "", string discipline = "", string seniority = "",
        string location = "", string skillSummary = "", WorkforceAvailabilityState? availabilityState = null,
        decimal availablePercent = 0m, DateOnly? contractEndDate = null, DateOnly? nextAvailabilityOn = null,
        DateTimeOffset? updatedAtUtc = null)
        => new(id, displayName, partyType, lifecycleStatus, externalCode, summary, tags, isSensitive, primaryEmail, primaryPhone,
            classification, jobTitle, discipline, seniority, location, skillSummary, availabilityState, availablePercent,
            contractEndDate, nextAvailabilityOn, updatedAtUtc ?? BaseUpdatedAtUtc);

    internal static readonly IReadOnlyList<CrmHrSandboxPartyRecord> Parties =
    [
        Party(NorthwindLogistics, "Northwind Logistics", PartyType.Organization, PartyLifecycleStatus.Active,
            "ACC-NWL", "Regional logistics account with two delivery units and a renewal in negotiation.",
            ["Customer", "Logistics"], primaryEmail: "accounts@northwindlogistics.example.test", primaryPhone: "+1 555 0181"),
        Party(BeaconHillAdvisory, "Beacon Hill Advisory", PartyType.Organization, PartyLifecycleStatus.Prospect,
            "ACC-BHA", "Advisory prospect evaluating a discovery engagement.",
            ["Prospect", "Advisory"], primaryEmail: "hello@beaconhilladvisory.example.test", primaryPhone: "+1 555 0155"),
        Party(CascadeRobotics, "Cascade Robotics", PartyType.Organization, PartyLifecycleStatus.Active,
            "ACC-CAR", "Robotics manufacturer running an R&D sprint with our delivery team.",
            ["Customer", "Manufacturing"], primaryEmail: "partners@cascaderobotics.example.test", primaryPhone: "+1 555 0166"),
        Party(SolaceFinancialGroup, "Solace Financial Group", PartyType.Organization, PartyLifecycleStatus.Former,
            "ACC-SFG", "Former customer; the migration project closed last year.", ["FormerCustomer"]),

        Party(NorthwindFulfillmentUnit, "Northwind Fulfillment Unit", PartyType.OrganizationUnit, PartyLifecycleStatus.Active,
            "UNIT-NWF", "Delivery unit staffing the warehouse automation and fulfillment rollout projects.",
            ["DeliveryUnit"], classification: WorkforceRecordClassification.DeliveryUnit),
        Party(NorthwindSupportUnit, "Northwind Support Unit", PartyType.OrganizationUnit, PartyLifecycleStatus.Active,
            "UNIT-NWS", "Delivery unit staffing the support desk revamp project.",
            ["DeliveryUnit"], classification: WorkforceRecordClassification.DeliveryUnit),
        Party(CascadeRnDUnit, "Cascade R&D Unit", PartyType.OrganizationUnit, PartyLifecycleStatus.Active,
            "UNIT-CRD", "Delivery unit staffing the Cascade Robotics R&D sprint.",
            ["DeliveryUnit"], classification: WorkforceRecordClassification.DeliveryUnit),

        Party(ElenaWard, "Elena Ward", PartyType.Person, PartyLifecycleStatus.Active, "EMP-1001",
            "Delivery lead for the Northwind account.", ["DeliveryLead"],
            primaryEmail: "elena.ward@example.test", primaryPhone: "+1 555 0110", classification: WorkforceRecordClassification.Employee,
            jobTitle: "Delivery Lead", discipline: "Delivery", seniority: "Lead", location: "Remote - Denver",
            skillSummary: "Delivery management, stakeholder communication",
            availabilityState: WorkforceAvailabilityState.Allocated, availablePercent: 20m, nextAvailabilityOn: new DateOnly(2026, 4, 6)),
        Party(MarcusChen, "Marcus Chen", PartyType.Person, PartyLifecycleStatus.Active, "EMP-1002",
            "Account manager for Northwind Logistics and Beacon Hill Advisory.", ["AccountManager"],
            primaryEmail: "marcus.chen@example.test", primaryPhone: "+1 555 0111", classification: WorkforceRecordClassification.Employee,
            jobTitle: "Account Manager", discipline: "Sales", seniority: "Senior", location: "Chicago",
            skillSummary: "Account management, negotiation",
            availabilityState: WorkforceAvailabilityState.Allocated, availablePercent: 40m, nextAvailabilityOn: new DateOnly(2026, 3, 30)),
        Party(PriyaNatarajan, "Priya Natarajan", PartyType.Person, PartyLifecycleStatus.Active, "EMP-1003",
            "Recruiter running the current candidate pipelines.", ["Recruiter"],
            primaryEmail: "priya.natarajan@example.test", primaryPhone: "+1 555 0112", classification: WorkforceRecordClassification.Employee,
            jobTitle: "Recruiter", discipline: "People", seniority: "Mid", location: "Austin",
            skillSummary: "Sourcing, interview coordination",
            availabilityState: WorkforceAvailabilityState.Bench, availablePercent: 90m),
        Party(OliverBennett, "Oliver Bennett", PartyType.Person, PartyLifecycleStatus.Active, "EMP-1004",
            "Engineering manager for the Cascade R&D unit.", ["Manager"],
            primaryEmail: "oliver.bennett@example.test", primaryPhone: "+1 555 0113", classification: WorkforceRecordClassification.Employee,
            jobTitle: "Engineering Manager", discipline: "Engineering", seniority: "Principal", location: "Remote - Seattle",
            skillSummary: "Robotics platform architecture",
            availabilityState: WorkforceAvailabilityState.NearAvailable, availablePercent: 15m, nextAvailabilityOn: new DateOnly(2026, 3, 23)),
        Party(SofiaRamirez, "Sofia Ramirez", PartyType.Person, PartyLifecycleStatus.Active, "CON-2001",
            "Contract data engineer supporting the R&D sprint.", ["Contractor"],
            primaryEmail: "sofia.ramirez@example.test", primaryPhone: "+1 555 0114", classification: WorkforceRecordClassification.Contractor,
            jobTitle: "Data Engineer", discipline: "Engineering", seniority: "Senior", location: "Remote - Austin",
            skillSummary: "Data pipelines, Python",
            availabilityState: WorkforceAvailabilityState.Overallocated, availablePercent: 0m,
            contractEndDate: new DateOnly(2026, 6, 30), nextAvailabilityOn: new DateOnly(2026, 7, 1)),
        Party(DerekHolt, "Derek Holt", PartyType.Person, PartyLifecycleStatus.Active, "CON-2002",
            "Contract platform engineer on the fulfillment rollout.", ["Contractor"],
            primaryEmail: "derek.holt@example.test", primaryPhone: "+1 555 0115", classification: WorkforceRecordClassification.Contractor,
            jobTitle: "Platform Engineer", discipline: "Engineering", seniority: "Mid", location: "Remote - Denver",
            skillSummary: "Kubernetes, CI/CD",
            availabilityState: WorkforceAvailabilityState.Allocated, availablePercent: 25m,
            contractEndDate: new DateOnly(2026, 5, 15), nextAvailabilityOn: new DateOnly(2026, 5, 16)),
        Party(GraceKim, "Grace Kim", PartyType.Person, PartyLifecycleStatus.Active, "FRE-3001",
            "Freelance product designer available for short engagements.", ["Freelancer"],
            primaryEmail: "grace.kim@example.test", primaryPhone: "+1 555 0116", classification: WorkforceRecordClassification.Freelancer,
            jobTitle: "Product Designer", discipline: "Design", seniority: "Senior", location: "Remote - Portland",
            skillSummary: "UX research, prototyping",
            availabilityState: WorkforceAvailabilityState.Bench, availablePercent: 100m),
        Party(NoahFischer, "Noah Fischer", PartyType.Person, PartyLifecycleStatus.Active, "EMP-1005",
            "Support engineer on the Northwind support unit.", [],
            primaryEmail: "noah.fischer@example.test", primaryPhone: "+1 555 0117", classification: WorkforceRecordClassification.Employee,
            jobTitle: "Support Engineer", discipline: "Support", seniority: "Mid", location: "Austin",
            skillSummary: "Incident response, customer support",
            availabilityState: WorkforceAvailabilityState.NearAvailable, availablePercent: 30m, nextAvailabilityOn: new DateOnly(2026, 3, 25)),
        Party(IvyChapman, "Ivy Chapman", PartyType.Person, PartyLifecycleStatus.Active, "EMP-1006",
            "QA engineer supporting the Cascade R&D unit.", [],
            primaryEmail: "ivy.chapman@example.test", primaryPhone: "+1 555 0118", classification: WorkforceRecordClassification.Employee,
            jobTitle: "QA Engineer", discipline: "Engineering", seniority: "Mid", location: "Remote - Seattle",
            skillSummary: "Test automation, quality gates",
            availabilityState: WorkforceAvailabilityState.Bench, availablePercent: 80m),
        Party(LucasMeyer, "Lucas Meyer", PartyType.Person, PartyLifecycleStatus.Candidate, "CAND-4001",
            "Backend engineer candidate in the interviewing stage.", ["Candidate"],
            primaryEmail: "lucas.meyer@example.test", primaryPhone: "+1 555 0119"),
        Party(AvaThompson, "Ava Thompson", PartyType.Person, PartyLifecycleStatus.Candidate, "CAND-4002",
            "Delivery-lead candidate freshly applied.", ["Candidate"],
            primaryEmail: "ava.thompson@example.test", primaryPhone: "+1 555 0120"),
        Party(JonasKeller, "Jonas Keller", PartyType.Person, PartyLifecycleStatus.Active, "CON-5001",
            "Operations director at Northwind Logistics; primary commercial contact.", ["CustomerContact"],
            primaryEmail: "jonas.keller@northwindlogistics.example.test", primaryPhone: "+1 555 0181",
            classification: WorkforceRecordClassification.ExternalContact),
        Party(MiaTorres, "Mia Torres", PartyType.Person, PartyLifecycleStatus.Active, "CON-5002",
            "Finance manager at Northwind Logistics; billing contact.", ["CustomerContact"],
            primaryEmail: "mia.torres@northwindlogistics.example.test", primaryPhone: "+1 555 0182",
            classification: WorkforceRecordClassification.ExternalContact),
        Party(LiamOConnor, "Liam O'Connor", PartyType.Person, PartyLifecycleStatus.Active, "CON-5003",
            "Primary contact at Beacon Hill Advisory.", ["CustomerContact"],
            primaryEmail: "liam.oconnor@beaconhilladvisory.example.test", primaryPhone: "+1 555 0156",
            classification: WorkforceRecordClassification.ExternalContact),
        Party(NoraPatel, "Nora Patel", PartyType.Person, PartyLifecycleStatus.Archived, "EMP-1007",
            "Former delivery lead; archived after the Solace migration closed.", ["Alumni"],
            classification: WorkforceRecordClassification.Employee,
            jobTitle: "Delivery Lead", discipline: "Delivery", seniority: "Lead", location: "Chicago",
            skillSummary: "Delivery management", contractEndDate: new DateOnly(2025, 12, 31),
            updatedAtUtc: BaseUpdatedAtUtc.AddMonths(-3)),
        Party(EthanBrooks, "Ethan Brooks", PartyType.Person, PartyLifecycleStatus.Active, "EMP-1008",
            "Bench engineer available for staffing.", [],
            primaryEmail: "ethan.brooks@example.test", primaryPhone: "+1 555 0121", classification: WorkforceRecordClassification.Employee,
            jobTitle: "Software Engineer", discipline: "Engineering", seniority: "Mid", location: "Remote - Denver",
            skillSummary: "C#, Azure", availabilityState: WorkforceAvailabilityState.Bench, availablePercent: 100m),
        Party(ChloeDubois, "Chloe Dubois", PartyType.Person, PartyLifecycleStatus.Active, "EMP-1009",
            "Overallocated engineer across two concurrent projects.", [],
            primaryEmail: "chloe.dubois@example.test", primaryPhone: "+1 555 0122", classification: WorkforceRecordClassification.Employee,
            jobTitle: "Software Engineer", discipline: "Engineering", seniority: "Senior", location: "Austin",
            skillSummary: "TypeScript, React", availabilityState: WorkforceAvailabilityState.Overallocated,
            availablePercent: 0m, nextAvailabilityOn: new DateOnly(2026, 6, 1)),
        Party(VictorHughes, "Victor Hughes", PartyType.Person, PartyLifecycleStatus.Active, "EMP-1010",
            "VP of Operations; manager of record for several profiles.", ["Manager"], isSensitive: true,
            primaryEmail: "victor.hughes@example.test", primaryPhone: "+1 555 0123", classification: WorkforceRecordClassification.Employee,
            jobTitle: "VP Operations", discipline: "Operations", seniority: "Principal", location: "Chicago",
            skillSummary: "Org planning, staffing governance",
            availabilityState: WorkforceAvailabilityState.NearAvailable, availablePercent: 10m, nextAvailabilityOn: new DateOnly(2026, 4, 20)),

        Party(AtlasOpsAgent, "Atlas Ops Agent", PartyType.AiAgent, PartyLifecycleStatus.Active, "AGT-6001",
            "Operations copilot bound to a workspace agent.", ["Operations"]),
        Party(LedgerInsightsAgent, "Ledger Insights Agent", PartyType.AiAgent, PartyLifecycleStatus.Active, "AGT-6002",
            "Finance analytics agent bound to a workspace agent.", ["Finance"]),
        Party(SupportCopilotAgent, "Support Copilot Agent", PartyType.AiAgent, PartyLifecycleStatus.Active, "AGT-6003",
            "Support desk copilot awaiting governance review.", ["Support"]),
        Party(SchedulerAgent, "Scheduler Agent", PartyType.AiAgent, PartyLifecycleStatus.Draft, "AGT-6004",
            "Draft scheduling agent; not yet technically bound.", ["Draft"]),
        Party(RecruitingScoutAgent, "Recruiting Scout Agent", PartyType.AiAgent, PartyLifecycleStatus.Candidate, "AGT-6005",
            "AI candidate in the recruiting pipeline, pending assessment review.", ["Candidate"])
    ];

    internal static readonly IReadOnlyList<CrmHrSandboxProjectRecord> Projects =
    [
        new(ProjectWarehouseAutomation, Id("lifetime-warehouse-automation"), "Northwind Warehouse Automation",
            ProjectStatus.Active, "Execution", "Automating pallet routing across two Northwind warehouses.", BaseUpdatedAtUtc),
        new(ProjectFulfillmentRollout, Id("lifetime-fulfillment-rollout"), "Northwind Fulfillment Rollout",
            ProjectStatus.Active, "Planning", "Rolling out the new fulfillment process to three regions.", BaseUpdatedAtUtc),
        new(ProjectBeaconHillEngagement, Id("lifetime-beacon-hill-engagement"), "Beacon Hill Advisory Engagement",
            ProjectStatus.Draft, "Discovery", "Discovery engagement pending a signed statement of work.", BaseUpdatedAtUtc),
        new(ProjectCascadeRnDSprint, Id("lifetime-cascade-rnd-sprint"), "Cascade Robotics R&D Sprint",
            ProjectStatus.Active, "Build", "Six-week sprint building the next robotics navigation module.", BaseUpdatedAtUtc),
        new(ProjectPlatformModernization, Id("lifetime-platform-modernization"), "Internal Platform Modernization",
            ProjectStatus.OnHold, "Paused", "Internal modernization paused pending budget confirmation.", BaseUpdatedAtUtc),
        new(ProjectSolaceMigration, Id("lifetime-solace-migration"), "Solace Financial Migration",
            ProjectStatus.Completed, "Closed", "Completed migration for the former Solace Financial account.", BaseUpdatedAtUtc.AddMonths(-4)),
        new(ProjectSupportDeskRevamp, Id("lifetime-support-desk-revamp"), "Support Desk Revamp",
            ProjectStatus.Active, "Execution", "Revamping the shared support desk tooling and staffing model.", BaseUpdatedAtUtc),
        new(ProjectArchivedPilot, Id("lifetime-archived-pilot"), "Archived Pilot Program",
            ProjectStatus.Archived, "Archived", "An early pilot program kept for historical reference only.", BaseUpdatedAtUtc.AddYears(-1))
    ];

    internal static readonly IReadOnlyList<CrmHrSandboxOpportunityRecord> Opportunities =
    [
        new(OpportunityWarehouseRenewal, NorthwindLogistics, "Warehouse automation renewal", OpportunityStage.Proposal,
            OpportunitySource.Renewal, MarcusChen, NorthwindFulfillmentUnit, "USD", 68000m, 65,
            new DateOnly(2026, 4, 30), ProjectWarehouseAutomation, BaseUpdatedAtUtc),
        new(OpportunityFulfillmentExpansion, NorthwindLogistics, "Fulfillment expansion", OpportunityStage.Negotiation,
            OpportunitySource.Upsell, MarcusChen, NorthwindFulfillmentUnit, "USD", 142000m, 80,
            new DateOnly(2026, 5, 15), null, BaseUpdatedAtUtc),
        new(OpportunitySupportUpsell, NorthwindLogistics, "Support contract upsell", OpportunityStage.Qualified,
            OpportunitySource.Direct, ElenaWard, NorthwindSupportUnit, "USD", null, 35,
            null, null, BaseUpdatedAtUtc)
    ];

    internal static readonly IReadOnlyList<PartyOrganizationAffiliationListItemModel> Affiliations =
    [
        new(Id("affiliation-jonas-keller"), JonasKeller, "Jonas Keller", NorthwindLogistics, "Northwind Logistics",
            PartyOrganizationAffiliationKind.ExternalContact, true, "Operations Director", "",
            null, "", null, "", new DateOnly(2024, 1, 15), null, "Primary commercial contact.",
            "crm-hr-ui", BaseUpdatedAtUtc.AddYears(-2), BaseUpdatedAtUtc, true),
        new(Id("affiliation-mia-torres"), MiaTorres, "Mia Torres", NorthwindLogistics, "Northwind Logistics",
            PartyOrganizationAffiliationKind.ExternalContact, false, "Finance Manager", "",
            null, "", null, "", new DateOnly(2024, 6, 1), null, "Billing and invoicing contact.",
            "crm-hr-ui", BaseUpdatedAtUtc.AddYears(-1), BaseUpdatedAtUtc, true)
    ];

    internal static CrmHrSandboxPartyRecord RequirePartyRecord(Guid partyId)
        => Parties.First(party => party.Id == partyId);

    internal static string DisplayNameOf(Guid partyId)
        => Parties.FirstOrDefault(party => party.Id == partyId)?.DisplayName ?? "Unknown party";
}

// The mutable backing store behind the party and workforce read ports below. Save, merge and delivery-unit-creation
// mutations go through here so the shared catalogs and pickers reflect them on the next read, exactly as production
// persistence would; nothing here is a database, a file or a network call.
public sealed class CrmHrSandboxPartyStore
{
    private readonly List<CrmHrSandboxPartyRecord> parties = CrmHrSandboxData.Parties.ToList();

    public IReadOnlyList<CrmHrSandboxPartyRecord> Snapshot() => parties.ToList();

    public CrmHrSandboxPartyRecord? Find(Guid partyId) => parties.FirstOrDefault(party => party.Id == partyId);

    public void Upsert(CrmHrSandboxPartyRecord record)
    {
        var index = parties.FindIndex(party => party.Id == record.Id);
        if (index >= 0)
        {
            parties[index] = record;
        }
        else
        {
            parties.Add(record);
        }
    }

    public void Remove(Guid partyId) => parties.RemoveAll(party => party.Id == partyId);

    public string DisplayNameOf(Guid partyId) => Find(partyId)?.DisplayName ?? "Unknown party";
}

// The mutable backing store behind the opportunity pipeline read port; a saved or newly created opportunity is
// visible to the pipeline on its next read.
public sealed class CrmHrSandboxOpportunityStore
{
    private readonly List<CrmHrSandboxOpportunityRecord> opportunities = CrmHrSandboxData.Opportunities.ToList();

    public IReadOnlyList<CrmHrSandboxOpportunityRecord> Snapshot() => opportunities.ToList();

    public CrmHrSandboxOpportunityRecord? Find(Guid opportunityId) => opportunities.FirstOrDefault(item => item.Id == opportunityId);

    public void Upsert(CrmHrSandboxOpportunityRecord record)
    {
        var index = opportunities.FindIndex(item => item.Id == record.Id);
        if (index >= 0)
        {
            opportunities[index] = record;
        }
        else
        {
            opportunities.Add(record);
        }
    }
}

// Deterministic in-memory implementation of the shared party directory read port: honors search text, tags, scope,
// the excluded party id, archived inclusion and the workforce population filter.
public sealed class CrmHrSandboxPartyRecordQueryService(CrmHrSandboxPartyStore store) : IPartyRecordQueryService
{
    public Task<PartyRecordQueryItem?> GetAsync(Guid partyId, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var party = store.Find(partyId);
        if (party is null || (!includeArchived && party.LifecycleStatus == PartyLifecycleStatus.Archived))
        {
            return Task.FromResult<PartyRecordQueryItem?>(null);
        }

        return Task.FromResult<PartyRecordQueryItem?>(ToQueryItem(party));
    }

    public Task<PartyRecordPage> SearchAsync(PartyRecordQuery query, CancellationToken cancellationToken = default)
    {
        var matches = store.Snapshot().Where(party => MatchesScope(party.PartyType, query.Scope));

        if (!query.IncludeArchived)
        {
            matches = matches.Where(party => party.LifecycleStatus != PartyLifecycleStatus.Archived);
        }

        if (query.ExcludedPartyId is { } excludedId)
        {
            matches = matches.Where(party => party.Id != excludedId);
        }

        if (query.Population == PartyRecordPopulation.Workforce)
        {
            matches = matches.Where(party => party.Classification.HasValue);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var searchText = query.SearchText.Trim();
            matches = matches.Where(party =>
                party.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                party.ExternalCode.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                party.Summary.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        if (query.Tags is { Count: > 0 } tags)
        {
            matches = matches.Where(party => tags.All(tag => party.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
        }

        var ordered = matches.OrderBy(party => party.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        var page = ordered.Skip(query.PageIndex * query.PageSize).Take(query.PageSize).Select(ToQueryItem).ToList();
        return Task.FromResult(new PartyRecordPage(page, query.PageIndex, query.PageSize, ordered.Count));
    }

    private static bool MatchesScope(PartyType partyType, PartyRecordScope scope)
        => partyType switch
        {
            PartyType.Person => scope.HasFlag(PartyRecordScope.People),
            PartyType.Organization => scope.HasFlag(PartyRecordScope.Organizations),
            PartyType.OrganizationUnit => scope.HasFlag(PartyRecordScope.OrganizationUnits),
            PartyType.AiAgent => scope.HasFlag(PartyRecordScope.AiAgents),
            _ => false
        };

    private static PartyRecordQueryItem ToQueryItem(CrmHrSandboxPartyRecord party)
        => new(party.Id, party.DisplayName, party.PartyType, party.LifecycleStatus, party.ExternalCode,
            party.Summary, party.Tags, party.IsSensitive);
}

// Deterministic in-memory implementation of the workforce read port: only parties with a resolved workforce
// classification are staffable, matching the production classification policy.
public sealed class CrmHrSandboxWorkforceRecordQueryService(CrmHrSandboxPartyStore store) : IWorkforceRecordQueryService
{
    public Task<WorkforceRecordPage> SearchAsync(WorkforceRecordQuery query, CancellationToken cancellationToken = default)
    {
        var matches = store.Snapshot().Where(party => party.Classification.HasValue);

        if (!query.IncludeArchived)
        {
            matches = matches.Where(party => party.LifecycleStatus != PartyLifecycleStatus.Archived);
        }

        if (query.Classification.HasValue)
        {
            matches = matches.Where(party => party.Classification == query.Classification);
        }

        if (query.LifecycleStatus.HasValue)
        {
            matches = matches.Where(party => party.LifecycleStatus == query.LifecycleStatus);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var searchText = query.SearchText.Trim();
            matches = matches.Where(party =>
                party.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                party.Summary.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                party.JobTitle.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = matches.OrderBy(party => party.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        var page = ordered.Skip(query.PageIndex * query.PageSize).Take(query.PageSize).Select(ToQueryItem).ToList();
        return Task.FromResult(new WorkforceRecordPage(page, query.PageIndex, query.PageSize, ordered.Count));
    }

    private static WorkforceRecordQueryItem ToQueryItem(CrmHrSandboxPartyRecord party)
    {
        var affiliation = CrmHrSandboxData.Affiliations.FirstOrDefault(item => item.PersonPartyId == party.Id && item.IsCurrent);
        var summary = affiliation is null
            ? []
            : new List<WorkforceRecordAffiliationSummaryModel>
            {
                new(affiliation.Id, affiliation.AffiliationKind, affiliation.OrganizationPartyId,
                    affiliation.OrganizationDisplayName, affiliation.JobTitle, affiliation.IsPrimary,
                    affiliation.ValidFrom, affiliation.ValidTo,
                    $"{affiliation.OrganizationDisplayName} / {affiliation.JobTitle}")
            };
        var primary = summary.FirstOrDefault(item => item.IsPrimary) ?? summary.FirstOrDefault();

        return new WorkforceRecordQueryItem(
            party.Id, party.DisplayName, party.PartyType, party.LifecycleStatus, party.IsSensitive, party.Summary,
            party.Classification!.Value, party.Classification != WorkforceRecordClassification.ExternalContact,
            party.UpdatedAtUtc, primary, primary?.DisplayText ?? "", summary);
    }
}

// Deterministic in-memory implementation of the opportunity pipeline read port: opportunities only exist for
// Northwind Logistics in this data set, so every other account returns an accepted empty page.
public sealed class CrmHrSandboxOpportunityPipelineQueryService(CrmHrSandboxOpportunityStore opportunityStore, CrmHrSandboxPartyStore partyStore)
    : IOpportunityPipelineQueryService
{
    public Task<OpportunityPipelinePage> SearchAsync(OpportunityPipelineQuery query, CancellationToken cancellationToken = default)
    {
        var matches = opportunityStore.Snapshot().Where(opportunity => opportunity.AccountPartyId == query.AccountPartyId);

        if (query.Stage.HasValue)
        {
            matches = matches.Where(opportunity => opportunity.Stage == query.Stage);
        }

        if (query.OwnerPartyId.HasValue)
        {
            matches = matches.Where(opportunity => opportunity.OwnerPartyId == query.OwnerPartyId);
        }

        if (query.DeliveryUnitPartyId.HasValue)
        {
            matches = matches.Where(opportunity => opportunity.DeliveryUnitPartyId == query.DeliveryUnitPartyId);
        }

        if (query.Source.HasValue)
        {
            matches = matches.Where(opportunity => opportunity.Source == query.Source);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var searchText = query.SearchText.Trim();
            matches = matches.Where(opportunity => opportunity.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = matches.OrderByDescending(opportunity => opportunity.UpdatedAtUtc).ToList();
        var page = ordered.Skip(query.PageIndex * query.PageSize).Take(query.PageSize).Select(ToItem).ToList();
        return Task.FromResult(new OpportunityPipelinePage(page, query.PageIndex, query.PageSize, ordered.Count));
    }

    private OpportunityPipelineItem ToItem(CrmHrSandboxOpportunityRecord opportunity)
        => new(opportunity.Id, opportunity.Title, opportunity.Stage, opportunity.Source, opportunity.AccountPartyId,
            partyStore.DisplayNameOf(opportunity.AccountPartyId), opportunity.OwnerPartyId,
            partyStore.DisplayNameOf(opportunity.OwnerPartyId), opportunity.DeliveryUnitPartyId,
            opportunity.DeliveryUnitPartyId.HasValue ? partyStore.DisplayNameOf(opportunity.DeliveryUnitPartyId.Value) : "",
            opportunity.CurrencyCode, opportunity.Amount, opportunity.ProbabilityPercent, opportunity.ExpectedCloseOn,
            opportunity.UpdatedAtUtc);
}

// Deterministic in-memory implementation of the organization affiliation read port: only two people carry an
// affiliation record in this data set, matching the manual-test note.
public sealed class CrmHrSandboxPartyOrganizationAffiliationReader : IPartyOrganizationAffiliationReader
{
    public Task<IReadOnlyList<PartyOrganizationAffiliationListItemModel>> ListAsync(Guid personPartyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PartyOrganizationAffiliationListItemModel>>(
            CrmHrSandboxData.Affiliations.Where(item => item.PersonPartyId == personPartyId).ToList());
}

// Deterministic in-memory implementation of the project catalog read port used by the connected-record, assignment
// and staffing pickers.
public sealed class CrmHrSandboxProjectRecordQueryService : IProjectRecordQueryService
{
    public Task<ProjectRecordQueryItem?> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Task.FromResult(CrmHrSandboxData.Projects.Where(project => project.Id == projectId).Select(ToItem).FirstOrDefault());

    public Task<IReadOnlyList<ProjectRecordQueryItem>> GetManyAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ProjectRecordQueryItem>>(
            CrmHrSandboxData.Projects.Where(project => projectIds.Contains(project.Id)).Select(ToItem).ToList());

    public Task<ProjectRecordPage> SearchAsync(ProjectRecordQuery query, CancellationToken cancellationToken = default)
    {
        var matches = CrmHrSandboxData.Projects.Where(project => MatchesScope(project.Status, query.Scope));

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var searchText = query.SearchText.Trim();
            matches = matches.Where(project => project.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = matches.OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var page = ordered.Skip(query.PageIndex * query.PageSize).Take(query.PageSize).Select(ToItem).ToList();
        return Task.FromResult(new ProjectRecordPage(page, query.PageIndex, query.PageSize, ordered.Count));
    }

    private static bool MatchesScope(ProjectStatus status, ProjectRecordScope scope)
        => scope switch
        {
            ProjectRecordScope.Open => status is ProjectStatus.Draft or ProjectStatus.Active or ProjectStatus.OnHold,
            ProjectRecordScope.Active => status == ProjectStatus.Active,
            ProjectRecordScope.Completed => status == ProjectStatus.Completed,
            ProjectRecordScope.Archived => status == ProjectStatus.Archived,
            _ => true
        };

    private static ProjectRecordQueryItem ToItem(CrmHrSandboxProjectRecord project)
        => new(project.Id, project.Name, project.Status, project.CurrentPhase, project.Description, project.UpdatedAtUtc)
        {
            LifetimeId = project.LifetimeId
        };
}

public static class CrmHrSandboxReadPortsServiceCollectionExtensions
{
    // Registers deterministic in-memory read ports over the shared synthetic data set; the sandbox has no database,
    // HTTP client or timer behind any of them.
    public static IServiceCollection AddCrmHrSandboxReadPorts(this IServiceCollection services)
    {
        services.AddSingleton<CrmHrSandboxPartyStore>();
        services.AddSingleton<CrmHrSandboxOpportunityStore>();
        services.AddSingleton<IPartyRecordQueryService, CrmHrSandboxPartyRecordQueryService>();
        services.AddSingleton<IWorkforceRecordQueryService, CrmHrSandboxWorkforceRecordQueryService>();
        services.AddSingleton<IOpportunityPipelineQueryService, CrmHrSandboxOpportunityPipelineQueryService>();
        services.AddSingleton<IPartyOrganizationAffiliationReader, CrmHrSandboxPartyOrganizationAffiliationReader>();
        services.AddSingleton<IProjectRecordQueryService, CrmHrSandboxProjectRecordQueryService>();
        return services;
    }
}
