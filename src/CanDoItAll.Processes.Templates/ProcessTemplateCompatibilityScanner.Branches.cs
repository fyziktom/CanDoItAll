using System.Text.Json;

namespace CanDoItAll.Processes.Templates;

public static partial class ProcessTemplateCompatibilityScanner
{
    private static ProcessBranchMigrationDiagnosticReport AnalyzeBranchOutcomes(string processKey, JsonElement definition)
    {
        if (!TryGetProperty(definition, "steps", out var steps) || steps.ValueKind != JsonValueKind.Array)
        {
            return new ProcessBranchMigrationDiagnosticReport(0, []);
        }

        var outcomeCount = 0;
        var diagnostics = new List<ProcessBranchMigrationDiagnostic>();
        foreach (var step in steps.EnumerateArray())
        {
            var stepKey = TryGetString(step, "key", out var key)
                ? key
                : "(missing-step-key)";
            if (!TryGetProperty(step, "branchOutcomes", out var outcomes) || outcomes.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var outcome in outcomes.EnumerateArray())
            {
                outcomeCount++;
                var hasKey = TryGetString(outcome, "key", out var outcomeKey);
                if (!hasKey)
                {
                    diagnostics.Add(new ProcessBranchMigrationDiagnostic(
                        processKey,
                        stepKey,
                        "(missing-outcome-key)",
                        ProcessBranchMigrationDiagnosticKind.MissingStableOutcomeKey,
                        "Branch outcome has no stable key and cannot be migrated automatically."));
                    continue;
                }

                if (!HasTypedRoute(outcome))
                {
                    diagnostics.Add(new ProcessBranchMigrationDiagnostic(
                        processKey,
                        stepKey,
                        outcomeKey,
                        ProcessBranchMigrationDiagnosticKind.AmbiguousRouteTarget,
                        "Branch outcome has display text but no typed route target; manual resolution is required."));
                }
            }
        }

        return new ProcessBranchMigrationDiagnosticReport(outcomeCount, diagnostics);
    }

    private static bool HasTypedRoute(JsonElement outcome)
    {
        return TryGetProperty(outcome, "routeTargetKind", out _) ||
               TryGetProperty(outcome, "routeTarget", out _) ||
               TryGetProperty(outcome, "targetStepKey", out _) ||
               TryGetProperty(outcome, "targetStepId", out _) ||
               TryGetProperty(outcome, "routeTargetId", out _);
    }
}
