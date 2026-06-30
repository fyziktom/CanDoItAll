using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Builder;

public sealed partial class ProcessInstancePlanCompiler
{
    private static void ValidateRequiredValues(
        ProcessInstancePlanCompileRequest request,
        ICollection<ProcessBuildDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(request.SourceSchemaVersion))
        {
            diagnostics.Add(Error(
                "Builder.SchemaSourceMissing",
                "Source schema version is required."));
        }

        if (string.IsNullOrWhiteSpace(request.TargetSchemaVersion))
        {
            diagnostics.Add(Error(
                "Builder.SchemaTargetMissing",
                "Target schema version is required."));
        }

        if (string.IsNullOrWhiteSpace(request.DefinitionContentHash))
        {
            diagnostics.Add(Error(
                "Builder.DefinitionHashMissing",
                "Definition content hash is required."));
        }

        if (request.MaximumSubprocessDepth < 0)
        {
            diagnostics.Add(Error(
                "Builder.InvalidSubprocessDepth",
                "Maximum subprocess depth cannot be negative."));
        }
    }

    private static void ValidateSubprocessDepth(
        int hierarchyDepth,
        int maximumSubprocessDepth,
        ICollection<ProcessBuildDiagnostic> diagnostics)
    {
        if (hierarchyDepth > maximumSubprocessDepth)
        {
            diagnostics.Add(Error(
                "Builder.SubprocessDepthExceeded",
                $"Subprocess depth '{hierarchyDepth}' exceeds maximum '{maximumSubprocessDepth}'."));
        }
    }

    private static void ValidateDefinitionCycle(
        ProcessInstancePlanCompileRequest request,
        IReadOnlyList<DefinitionIdentity> ancestorDefinitions,
        ICollection<ProcessBuildDiagnostic> diagnostics)
    {
        var current = DefinitionIdentity.From(request.Definition);
        if (ancestorDefinitions.Contains(current))
        {
            diagnostics.Add(Error(
                "Builder.SubprocessCycle",
                $"Subprocess definition '{request.Definition.DefinitionId}' version '{request.Definition.VersionId}' already appears in the plan path."));
        }
    }

    private static IReadOnlyList<string> ValidateMigrations(
        ProcessInstancePlanCompileRequest request,
        ICollection<ProcessBuildDiagnostic> diagnostics)
    {
        if (string.Equals(request.SourceSchemaVersion, request.TargetSchemaVersion, StringComparison.Ordinal))
        {
            return [];
        }

        if (request.MigrationRegistry is null)
        {
            diagnostics.Add(Error(
                "Builder.TemplateMigrationRegistryMissing",
                "Template migration registry is required when source and target schema versions differ."));
            return [];
        }

        var migrationPlan = request.MigrationRegistry.CreatePlan(
            request.SourceSchemaVersion,
            request.TargetSchemaVersion);
        if (!migrationPlan.Succeeded)
        {
            diagnostics.Add(Error(
                migrationPlan.ErrorCode ?? "Builder.TemplateMigrationFailed",
                migrationPlan.ErrorMessage ?? "Template migration planning failed."));
            return [];
        }

        return migrationPlan.Migrations
            .Select(migration => migration.MigrationId)
            .ToArray();
    }

    private static IReadOnlyList<ResolvedTemplateComponentSnapshot> ResolveTemplateComponents(
        ProcessInstancePlanCompileRequest request,
        ICollection<ProcessBuildDiagnostic> diagnostics)
    {
        var available = request.AvailableTemplateComponents.ToDictionary(
            component => component.ComponentId);
        var resolved = new List<ResolvedTemplateComponentSnapshot>();
        foreach (var required in request.RequiredTemplateComponents)
        {
            if (!available.TryGetValue(required.ComponentId, out var component))
            {
                diagnostics.Add(Error(
                    "Builder.TemplateComponentMissing",
                    $"Required template component '{required.Key}' is not available."));
                continue;
            }

            if (!string.Equals(required.ResolvedContentHash, component.ResolvedContentHash, StringComparison.Ordinal))
            {
                diagnostics.Add(Error(
                    "Builder.TemplateComponentHashMismatch",
                    $"Required template component '{required.Key}' hash does not match the available component."));
                continue;
            }

            resolved.Add(new ResolvedTemplateComponentSnapshot(
                component.ComponentId,
                component.Key,
                component.ContentVersion,
                component.ResolvedContentHash));
        }

        return resolved
            .OrderBy(component => component.Key, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> ValidateLocalOverrides(
        ProcessInstancePlanCompileRequest request,
        ICollection<ProcessBuildDiagnostic> diagnostics)
    {
        var operations = request.LocalOverridePatches
            .SelectMany(patch => patch.Operations)
            .ToArray();
        var mergeResult = ProcessTemplateThreeWayMerge.DetectConflicts(
            operations,
            request.ChangedGlobalTemplatePointers);

        foreach (var conflict in mergeResult.Conflicts)
        {
            diagnostics.Add(Error(
                "Builder.TemplateOverrideConflict",
                $"Local template override conflicts at '{conflict.JsonPointer}'."));
        }

        return mergeResult.AutoAppliedOperations
            .Select(operation => operation.JsonPointer)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidateDefinition(
        ProcessInstancePlanCompileRequest request,
        ICollection<ProcessBuildDiagnostic> diagnostics)
    {
        foreach (var failure in ProcessGraphKernel.Validate(request.Definition).Failures)
        {
            diagnostics.Add(Error(failure.Code, failure.Message));
        }

        var stepIds = request.Definition.Steps
            .Select(step => step.Id)
            .ToHashSet();
        foreach (var branch in request.Definition.Branches)
        {
            foreach (var outcome in branch.Outcomes)
            {
                if (outcome.RouteTarget.StepId is { } stepId && !stepIds.Contains(stepId))
                {
                    diagnostics.Add(Error(
                        "Builder.BranchRouteUnknownStep",
                        $"Branch outcome '{outcome.Id}' references unknown step '{stepId}'."));
                }
            }
        }
    }

    private static void ValidateInitialArtifacts(
        ProcessInstancePlanCompileRequest request,
        ICollection<ProcessBuildDiagnostic> diagnostics)
    {
        var slotIds = request.Definition.ArtifactSlots
            .Select(slot => slot.Id)
            .ToHashSet();
        foreach (var failure in ProcessArtifactRules.ValidateReferences(request.InitialArtifactReferences, slotIds).Failures)
        {
            diagnostics.Add(Error(failure.Code, failure.Message));
        }
    }

    private static ProcessCapabilityMatchResult SelectDriverStack(
        ProcessInstancePlanCompileRequest request,
        ICollection<ProcessBuildDiagnostic> diagnostics)
    {
        var match = request.DriverCatalog.Match(request.CapabilityRequest);
        foreach (var missingCapability in match.MissingCapabilityTags)
        {
            diagnostics.Add(Error(
                "Builder.DriverCapabilityMissing",
                $"Required capability '{missingCapability}' is not provided by the selected driver stack."));
        }

        foreach (var conflict in match.Conflicts)
        {
            diagnostics.Add(Error(
                "Builder.DriverConflict",
                conflict.Reason));
        }

        return match;
    }
}
