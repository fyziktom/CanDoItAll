using CanDoItAll.SharedKernel;

namespace CanDoItAll.ScenarioSeeder;

internal static class GraphCatalog
{
    public static IReadOnlyList<AgentFrameworkIntegrationSimulationSeeder.GraphNodeSpec> BuildGraphSpecs()
    {
        return
        [
            new("phase-role", ProjectObjectType.Phase, string.Empty, "Phase 1 - Role-first process baseline", "Roles before people or agents", "Active", 120, 120, null, "Define reusable execution roles, lane policies, and meeting triggers before any implementation work proceeds."),
            new("phase-boundary", ProjectObjectType.Phase, string.Empty, "Phase 2 - Canonical model and boundary convergence", "One source of truth per concept", "Active", 640, 120, null, "Decide the canonical owner for provider profiles, participant identity, and role-definition semantics."),
            new("phase-execution", ProjectObjectType.Phase, string.Empty, "Phase 3 - Execution slices and provider integration", "Local and complex lanes split on purpose", "Active", 1160, 120, null, "Run bounded local slices while isolating the hard provider-governance work into a controlled complex lane."),
            new("phase-validation", ProjectObjectType.Phase, string.Empty, "Phase 4 - Validation and governed rollout", "Proof before closure", "Active", 1680, 120, null, "Collect service, UI, and graph proof before declaring the simulation ready for the next real bundle."),
            new("phase-learning", ProjectObjectType.Phase, string.Empty, "Phase 5 - Operational learning", "Turn friction into work", "Planned", 2200, 120, null, "Capture what made this simulation hard and convert it into concrete follow-up bundles and architecture repair."),
            new("role-catalog", ProjectObjectType.ProjectBlock, "feature", "Role catalogue and execution-lane policy", "Reusable operating model baseline", "Blocked", 120, 300, "phase-role", "Role-first operating model with explicit human-only powers, local-LLM-safe work, OpenAI escalation rules, and mandatory meetings.", "AgentFramework integration / role-first operating model baseline"),
            new("role-workshop", ProjectObjectType.Meeting, string.Empty, "Role charter convergence workshop", "Human meeting for blocked authority questions", "Planned", 360, 460, "phase-role", "Meeting exists because provider-profile ownership and authority overlap could not be settled by text alone.", "AgentFramework integration / role-first operating model baseline"),
            new("role-handbook-task", ProjectObjectType.WorkItem, "task", "Publish reusable role handbook", "Future bundle input", "Planned", 380, 300, "phase-role", "Task consumes the approved operating model and republishes it as a durable reference for later bundles.", "AgentFramework integration / role-first operating model baseline"),
            new("ownership-audit", ProjectObjectType.ProjectBlock, "research", "Cross-repo source-of-truth audit", "Inventory before merge decisions", "Completed", 640, 300, "phase-boundary", "Audit covers CanDoItAll, CRM/HR, and CanDoItAll.AgentFramework with explicit notes on duplicated provider-profile and participant identity semantics.", "AgentFramework integration / canonical model and boundary convergence"),
            new("provider-merge-decision", ProjectObjectType.Decision, string.Empty, "Single-source-of-truth decision record", "Provider-profile and agent identity ownership", "WaitingApproval", 920, 300, "phase-boundary", "Decision node represents the canonical owner selection that downstream slices depend on.", "AgentFramework integration / canonical model and boundary convergence"),
            new("architecture-meeting", ProjectObjectType.Meeting, string.Empty, "Architecture convergence review", "Human decision on disputed ownership", "Completed", 920, 460, "phase-boundary", "Meeting resolved that no duplicate provider-profile registry may remain writable after the integration work starts.", "AgentFramework integration / canonical model and boundary convergence"),
            new("migration-backlog", ProjectObjectType.WorkItem, "task", "Map provider-profile and role-definition migration path", "Backlog item for follow-up slices", "Active", 1180, 300, "phase-boundary", "Task tracks the concrete migration package that follows the ownership decision.", "AgentFramework integration / canonical model and boundary convergence"),
            new("local-slices", ProjectObjectType.ProjectBlock, "implementation", "Local-LLM-safe implementation slices", "Cheap, bounded, reviewable work", "Active", 1160, 300, "phase-execution", "Feature block holds bounded slices such as reusable form extraction, nearby validation hooks, and safe code cleanup.", "AgentFramework integration / local-LLM-safe execution slices"),
            new("form-extraction-task", ProjectObjectType.WorkItem, "task", "Extract process definition forms into reusable components", "Needed for floating windows and modal reuse", "Active", 1400, 300, "phase-execution", "Task focuses on reusable process forms so process canvas workflows do not duplicate editor markup.", "AgentFramework integration / local-LLM-safe execution slices"),
            new("openai-lane", ProjectObjectType.ProjectBlock, "research", "OpenAI-assisted complex integration lane", "Only for sanitized hard problems", "Blocked", 1160, 460, "phase-execution", "Feature block owns the hard cross-boundary questions that local slices must not absorb silently.", "AgentFramework integration / OpenAI-assisted complex integration lane"),
            new("provider-governance-meeting", ProjectObjectType.Meeting, string.Empty, "Provider-governance board", "Human review for security and budget posture", "Planned", 1400, 460, "phase-execution", "Meeting is required before external-model recommendations can become implementation work.", "AgentFramework integration / OpenAI-assisted complex integration lane"),
            new("provider-ownership-task", ProjectObjectType.WorkItem, "issue", "Resolve provider profile ownership and agent registration merge path", "Hard cross-boundary issue", "Blocked", 1640, 460, "phase-execution", "Issue remains blocked until security and cost review clear the external-model lane.", "AgentFramework integration / OpenAI-assisted complex integration lane"),
            new("validation-matrix", ProjectObjectType.TestPlan, string.Empty, "Validation matrix and proof package", "Service, UI, graph, and learning checks", "Completed", 1680, 300, "phase-validation", "Validation matrix names process routes, graph bindings, large-screen UX, and follow-up gap capture as first-class proof areas.", "AgentFramework integration / validation, rollout, and learning loop"),
            new("large-screen-proof", ProjectObjectType.ValidationRun, string.Empty, "Large-screen process/workbench parity proof", "Canvas, compactness, and route context", "Active", 1940, 300, "phase-validation", "Validation run checks that project structure and process workspace open the intended context and remain compact enough for large screens.", "AgentFramework integration / validation, rollout, and learning loop"),
            new("rollout-decision", ProjectObjectType.Decision, string.Empty, "Go / no-go for the next real bundle", "Closure depends on proof and follow-up gaps", "Planned", 2180, 300, "phase-validation", "Decision node stays closed until proof exists and follow-up gaps have owners.", "AgentFramework integration / validation, rollout, and learning loop"),
            new("learning-journal", ProjectObjectType.Note, string.Empty, "Simulation friction journal", "What was painful and why", "Draft", 2200, 300, "phase-learning", "Journal holds product gaps discovered while seeding and validating this scenario: binding APIs, lane-policy structure, reviewer granularity, and stale MCP routing.", "AgentFramework integration / validation, rollout, and learning loop", false),
            new("followup-backlog", ProjectObjectType.ProjectBlock, "task-flow", "Follow-up bundle generation", "Turn friction into repair work", "Draft", 2460, 300, "phase-learning", "Task-flow converts the learning journal into post-simulation repair bundles and execution backlog.", "AgentFramework integration / validation, rollout, and learning loop", false),
            new("execution-lanes", ProjectObjectType.ProjectBlock, "delivery", "Execution lanes and participants", "Human and AI lanes with explicit limits", "Active", 120, 720, null, "Root block summarizes who works in the simulation and under which lane rules."),
            new("human-governance-lane", ProjectObjectType.Participant, "team-section", "Human governance lane", "Sponsor, architect, steward, security, and cost review", "Active", 120, 900, "execution-lanes", "Primary humans: Elena Hart, Priya Nandakumar, Tomas Velek, Renata Ionescu, and Klara Novak. This lane owns approvals, ownership decisions, security review, and budget guardrails."),
            new("implementation-lane", ProjectObjectType.Participant, "team-section", "Human implementation lane", "Delivery, integration, UX, and QA execution", "Active", 420, 900, "execution-lanes", "Primary humans: Martin Kral, Miguel Ortega, Sara Kovacs, and Naomi Bell. This lane owns bounded implementation, review, proof, and meeting preparation."),
            new("local-ai-lane", ProjectObjectType.Participant, "ai-agent", "Local Slice Worker", "Bounded local AI execution only", "Active", 720, 900, "execution-lanes", "Allowed: small repository-local slices, code search, draft tests, component extraction within approved bounds. Forbidden: secrets, canonical ownership, provider governance, or release approval."),
            new("openai-ai-lane", ProjectObjectType.Participant, "ai-agent", "OpenAI Deep Analysis Agent", "Sanitized complex-lane analysis only", "Active", 1020, 900, "execution-lanes", "Allowed: sanitized option analysis for cross-boundary problems. Forbidden: secrets, direct execution, release approval, or canonical truth ownership."),
            new("repo-landscape", ProjectObjectType.ProjectBlock, "repos", "Repository and runtime landscape", "Where the simulation draws its evidence from", "Active", 1400, 720, null, "Root block anchors the repos, environments, and provider lanes used by the simulation."),
            new("candoitall-repo", ProjectObjectType.Repository, "folder", "CanDoItAll repository", @"C:\repositories\CanDoItAll", "Active", 1400, 900, "repo-landscape", "Primary application repo containing modules, process UI, workbench, and MCP servers."),
            new("agentframework-repo", ProjectObjectType.Repository, "folder", "CanDoItAll.AgentFramework repository", @"C:\repositories\CanDoItAll.AgentFramework", "Active", 1720, 900, "repo-landscape", "Separate repo whose registry, provider, and agent-creation capabilities are being integrated without duplicating ownership."),
            new("watch-runtime", ProjectObjectType.Environment, "dotnet-runtime", "Local watch runtime", "Managed app session against the active PostgreSQL profile", "Active", 2040, 900, "repo-landscape", "Runtime currently exposes the seeded project data but the project-structure MCP still points to a stale fixed port."),
            new("local-llm-env", ProjectObjectType.Environment, "dotnet-runtime", "Local LLM sandbox", "Cheap lane for bounded implementation slices", "Active", 2360, 900, "repo-landscape", "Environment exists for small, reversible, repository-local work only."),
            new("openai-guardrails", ProjectObjectType.Infrastructure, string.Empty, "OpenAI API and budget guardrails", "Security and spend review required", "Blocked", 2680, 900, "repo-landscape", "Infrastructure note captures that external-model use stays blocked until redaction and spend posture are approved.")
        ];
    }

    public static IReadOnlyList<AgentFrameworkIntegrationSimulationSeeder.GraphLinkSpec> BuildGraphLinks()
    {
        return
        [
            new("role-catalog", "role-workshop", ProjectObjectLinkKind.Blocks),
            new("role-catalog", "role-handbook-task", ProjectObjectLinkKind.DerivedFrom),
            new("ownership-audit", "provider-merge-decision", ProjectObjectLinkKind.DependsOn),
            new("provider-merge-decision", "migration-backlog", ProjectObjectLinkKind.Blocks),
            new("openai-lane", "provider-governance-meeting", ProjectObjectLinkKind.Blocks),
            new("validation-matrix", "large-screen-proof", ProjectObjectLinkKind.Validates),
            new("large-screen-proof", "rollout-decision", ProjectObjectLinkKind.DependsOn),
            new("learning-journal", "followup-backlog", ProjectObjectLinkKind.DerivedFrom),
            new("local-slices", "candoitall-repo", ProjectObjectLinkKind.Uses),
            new("ownership-audit", "agentframework-repo", ProjectObjectLinkKind.Uses),
            new("large-screen-proof", "watch-runtime", ProjectObjectLinkKind.Uses),
            new("local-slices", "local-llm-env", ProjectObjectLinkKind.Uses),
            new("openai-lane", "openai-guardrails", ProjectObjectLinkKind.Uses),
            new("provider-ownership-task", "provider-merge-decision", ProjectObjectLinkKind.DependsOn)
        ];
    }
}
