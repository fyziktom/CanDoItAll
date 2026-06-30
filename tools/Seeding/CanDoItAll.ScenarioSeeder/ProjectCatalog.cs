using CanDoItAll.Modules.Projects;

namespace CanDoItAll.ScenarioSeeder;

internal static class ProjectCatalog
{
    public static IReadOnlyList<ProjectPhaseEditorModel> BuildProjectPhases()
    {
        return
        [
            new ProjectPhaseEditorModel
            {
                Name = "Role-first process baseline",
                Goal = "Define reusable execution roles, approval rights, AI lanes, and escalation triggers before implementation begins.",
                Status = ProjectPhaseStatus.Active,
                StartDateUtc = new DateTime(2026, 4, 13),
                EndDateUtc = new DateTime(2026, 4, 18)
            },
            new ProjectPhaseEditorModel
            {
                Name = "Canonical model and boundary convergence",
                Goal = "Decide the single sources of truth between CanDoItAll, CRM/HR, and AgentFramework and publish the migration path.",
                Status = ProjectPhaseStatus.Active,
                StartDateUtc = new DateTime(2026, 4, 18),
                EndDateUtc = new DateTime(2026, 4, 25)
            },
            new ProjectPhaseEditorModel
            {
                Name = "Execution slices and provider integration",
                Goal = "Deliver small, locally solvable slices and isolate the larger provider-orchestration problems into controlled expert lanes.",
                Status = ProjectPhaseStatus.Planned,
                StartDateUtc = new DateTime(2026, 4, 25),
                EndDateUtc = new DateTime(2026, 5, 15)
            },
            new ProjectPhaseEditorModel
            {
                Name = "Validation and governed rollout",
                Goal = "Prove process UX, canvas parity, data integrity, and release readiness before rollout.",
                Status = ProjectPhaseStatus.Planned,
                StartDateUtc = new DateTime(2026, 5, 15),
                EndDateUtc = new DateTime(2026, 5, 29)
            },
            new ProjectPhaseEditorModel
            {
                Name = "Operational learning and follow-up bundles",
                Goal = "Capture friction, missing capabilities, and new bundle work so future implementation does not build on hidden weaknesses.",
                Status = ProjectPhaseStatus.Planned,
                StartDateUtc = new DateTime(2026, 5, 29),
                EndDateUtc = new DateTime(2026, 6, 5)
            }
        ];
    }

    public static IReadOnlyList<ProjectOptionEditorModel> BuildProjectOptions()
    {
        return
        [
            new ProjectOptionEditorModel
            {
                Category = ProjectOptionCategory.Language,
                OptionName = "C# / .NET 10",
                Notes = "Shared runtime and modules stay strongly typed."
            },
            new ProjectOptionEditorModel
            {
                Category = ProjectOptionCategory.Database,
                OptionName = "PostgreSQL runtime profile",
                Notes = "Simulation is seeded into the active PostgreSQL profile for realistic evaluation."
            },
            new ProjectOptionEditorModel
            {
                Category = ProjectOptionCategory.Ui,
                OptionName = "Blazor workbench with BaseLib/CanvasLib",
                Notes = "Process canvas and project structure must stay aligned on large-screen UX."
            },
            new ProjectOptionEditorModel
            {
                Category = ProjectOptionCategory.ExternalApi,
                OptionName = "OpenAI API for high-complexity analysis only",
                Notes = "Local slices stay on local LLM lanes; complex cross-boundary work escalates to sanitized OpenAI contexts."
            },
            new ProjectOptionEditorModel
            {
                Category = ProjectOptionCategory.Storage,
                OptionName = "Workspace files plus IPFS-ready artifact strategy",
                Notes = "Evidence retention, replay, and managed artifacts remain explicit."
            },
            new ProjectOptionEditorModel
            {
                Category = ProjectOptionCategory.Deployment,
                OptionName = "Local watch runtime before governed rollout",
                Notes = "Simulation focuses on design and validation, not production release."
            },
            new ProjectOptionEditorModel
            {
                Category = ProjectOptionCategory.Testing,
                OptionName = "Integration tests, Playwright, process-run audit trail",
                Notes = "Each execution slice must end with proof and follow-up learning."
            }
        ];
    }
}
