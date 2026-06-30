using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.ScenarioSeeder;

internal static class PartyCatalog
{
    public static IReadOnlyList<AgentFrameworkIntegrationSimulationSeeder.PartySpec> BuildPartySpecs()
    {
        return
        [
            new("AFINT-STEERING", PartyType.Organization, "CanDoItAll Product Steering Committee", "CanDoItAll Product Steering Committee", "Internal steering body for product direction, budget posture, and release risk appetite.", "Approves business direction and escalations, but does not own technical design detail.", "human-governance", "Raises a human decision board whenever architecture or provider governance changes user-facing autonomy.", "steering@candoitall.local", [PartyRoleKind.Stakeholder]),
            new("AFINT-DELIVERY-GUILD", PartyType.OrganizationUnit, "Platform Delivery Guild", "Platform Delivery Guild", "Primary delivery unit coordinating architecture, implementation, and validation work.", "Owns sequencing and staffing for the simulation delivery program.", "human-delivery", "Cannot overrule source-of-truth decisions without architecture review.", "delivery-guild@candoitall.local", [PartyRoleKind.DeliveryUnit]),
            new("AFINT-SPONSOR", PartyType.Person, "Elena Hart", null, "Program sponsor for the AgentFramework integration effort.", "Can approve scope, priority, and release risk acceptance. Cannot redefine canonical ownership or bypass security review.", "human-governance", "Must call a sponsor review if an unresolved issue changes budget, timeline, or customer-visible autonomy.", "elena.hart@candoitall.local", [PartyRoleKind.Stakeholder]),
            new("AFINT-DELIVERY-MGR", PartyType.Person, "Martin Kral", null, "Delivery manager coordinating phases, staffing, and decision cadences.", "Can sequence work and run meetings. Cannot approve irreversible technical changes alone.", "human-delivery", "Must raise a meeting when slices stop being independently verifiable.", "martin.kral@candoitall.local", [PartyRoleKind.Employee]),
            new("AFINT-ARCH", PartyType.Person, "Priya Nandakumar", null, "Solution architect for module boundaries, service ownership, and integration contracts.", "Can define boundaries and propose merges. Cannot self-approve release readiness or staffing.", "human-architecture", "Must raise an architecture convergence meeting when more than one repo claims the same canonical entity.", "priya.nandakumar@candoitall.local", [PartyRoleKind.Employee]),
            new("AFINT-CANONICAL", PartyType.Person, "Tomas Velek", null, "Canonical model steward for identity, provider profile, and process ownership decisions.", "Can approve ownership maps. Cannot waive migration evidence or invent fallback shadow models.", "human-architecture", "Must raise a cross-module review when a projection starts behaving like a source of truth.", "tomas.velek@candoitall.local", [PartyRoleKind.Employee]),
            new("AFINT-CRMHR", PartyType.Person, "Ivana Petrovic", null, "CRM/HR owner representing current participant, staffing, and supplier records.", "Can expose current model constraints. Cannot duplicate profiles or merge identity semantics unilaterally.", "human-domain-owner", "Must raise a reconciliation meeting if role-first staffing would lose current CRM/HR obligations.", "ivana.petrovic@candoitall.local", [PartyRoleKind.Employee]),
            new("AFINT-INTEGRATION", PartyType.Person, "Miguel Ortega", null, "Integration engineer implementing cross-module runtime slices and service registration changes.", "Can deliver bounded code slices. Cannot approve provider governance, budget changes, or canonical ownership decisions.", "human-implementation", "Must escalate when a slice touches more than one repository-owned source of truth.", "miguel.ortega@candoitall.local", [PartyRoleKind.Employee]),
            new("AFINT-WORKBENCH", PartyType.Person, "Sara Kovacs", null, "Workbench and UX engineer responsible for large-screen compactness and process-canvas parity.", "Can refactor UI composition and validation flows. Cannot silently introduce non-component markup patterns where shared components should be used.", "human-implementation", "Must raise a UI review when canvas workflows diverge from project structure patterns.", "sara.kovacs@candoitall.local", [PartyRoleKind.Employee]),
            new("AFINT-QA", PartyType.Person, "Naomi Bell", null, "QA and validation lead for integration tests, browser proof, and evidence quality.", "Can block release on missing proof. Cannot redefine architecture or waive evidence requirements.", "human-validation", "Must call a validation review if evidence is partial, stale, or unrepeatable.", "naomi.bell@candoitall.local", [PartyRoleKind.Employee]),
            new("AFINT-SECURITY", PartyType.Person, "Renata Ionescu", null, "Security and governance reviewer for provider usage, secret handling, and risky autonomy transitions.", "Can reject unsafe provider usage. Cannot change delivery scope or canonical ownership alone.", "human-governance", "Must escalate to a decision board if OpenAI use requires sensitive context or irreversible actions.", "renata.ionescu@candoitall.local", [PartyRoleKind.Employee]),
            new("AFINT-COST", PartyType.Person, "Klara Novak", null, "Cost and vendor steward for OpenAI consumption, contract posture, and budget guardrails.", "Can define spend ceilings and vendor constraints. Cannot approve architecture or release safety alone.", "human-governance", "Must raise a commercial review when proposed OpenAI usage changes expected recurring cost.", "klara.novak@candoitall.local", [PartyRoleKind.Employee]),
            new("AFINT-LOCAL-LLM", PartyType.AiAgent, "Local Slice Worker", null, "Local AI agent for small, bounded coding and analysis slices inside pre-approved execution lanes.", "Allowed: repository-local refactors, code search, draft tests, component extraction inside bounded scope. Cannot: receive secrets, change canonical ownership, approve release, or handle complex provider policy.", "ai-local", "Must request a human meeting when a slice crosses modules, credentials, or policy interpretation.", null, [PartyRoleKind.AiSteward]),
            new("AFINT-OPENAI", PartyType.AiAgent, "OpenAI Deep Analysis Agent", null, "External-model specialist for sanitized, high-complexity architecture analysis and option generation.", "Allowed: critique complex proposals from sanitized context, compare tradeoffs, generate governance-aware alternatives. Cannot: receive secrets, directly execute risky changes, approve rollout, or own canonical truth.", "ai-openai", "Must escalate to a human decision meeting when its recommendation affects autonomy, security, or spend policy.", null, [PartyRoleKind.AiSteward])
        ];
    }

    public static IReadOnlyList<ProjectPartyAssignmentUpsertRequest> BuildProjectAssignmentSpecs(
        Guid projectId,
        IReadOnlyDictionary<string, SeededParty> parties)
    {
        return
        [
            Create(projectId, parties["AFINT-STEERING"], ProjectPartyAssignmentRole.Customer, true, 0m, "Simulation steering customer context."),
            Create(projectId, parties["AFINT-SPONSOR"], ProjectPartyAssignmentRole.Stakeholder, true, 10m, "Primary sponsor for escalation and release-risk decisions."),
            Create(projectId, parties["AFINT-SPONSOR"], ProjectPartyAssignmentRole.CustomerContact, true, 10m, "Internal product representative for the simulation."),
            Create(projectId, parties["AFINT-DELIVERY-GUILD"], ProjectPartyAssignmentRole.DeliveryUnit, true, 100m, "Primary delivery unit for the integration program."),
            Create(projectId, parties["AFINT-DELIVERY-MGR"], ProjectPartyAssignmentRole.Manager, true, 40m, "Delivery sequencing and meeting orchestration."),
            Create(projectId, parties["AFINT-ARCH"], ProjectPartyAssignmentRole.TechnicalContact, true, 35m, "Architecture boundary owner."),
            Create(projectId, parties["AFINT-CANONICAL"], ProjectPartyAssignmentRole.Reviewer, true, 30m, "Canonical model review owner."),
            Create(projectId, parties["AFINT-SECURITY"], ProjectPartyAssignmentRole.Reviewer, false, 20m, "Security and governance reviewer for provider usage."),
            Create(projectId, parties["AFINT-INTEGRATION"], ProjectPartyAssignmentRole.TeamMember, true, 70m, "Primary implementation engineer."),
            Create(projectId, parties["AFINT-WORKBENCH"], ProjectPartyAssignmentRole.TeamMember, false, 50m, "Workbench and UX implementation engineer."),
            Create(projectId, parties["AFINT-QA"], ProjectPartyAssignmentRole.TeamMember, false, 45m, "Validation and proof execution."),
            Create(projectId, parties["AFINT-CRMHR"], ProjectPartyAssignmentRole.TeamMember, false, 20m, "Current CRM/HR model owner."),
            Create(projectId, parties["AFINT-LOCAL-LLM"], ProjectPartyAssignmentRole.AiAgent, true, 60m, "Primary local AI slice worker."),
            Create(projectId, parties["AFINT-OPENAI"], ProjectPartyAssignmentRole.AiAgent, false, 15m, "Escalation lane for sanitized high-complexity analysis."),
            Create(projectId, parties["AFINT-COST"], ProjectPartyAssignmentRole.BillingContact, true, 10m, "Commercial cost and vendor guardrails.")
        ];
    }

    private static ProjectPartyAssignmentUpsertRequest Create(
        Guid projectId,
        SeededParty party,
        ProjectPartyAssignmentRole role,
        bool isPrimary,
        decimal allocationPercent,
        string notes)
    {
        return new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = party.PartyId,
            Role = role,
            NodeKey = string.Empty,
            IsPrimary = isPrimary,
            AllocationPercent = allocationPercent,
            Source = "scenario-seeder",
            Notes = notes
        };
    }
}
