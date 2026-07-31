using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;

namespace CanDoItAll.Processes.Templates;

public sealed class ProcessTemplatePackLoader
{
    private const string ManifestFileName = "manifest.json";
    private const string DefinitionFileName = "definition.json";
    private const string SharedDirectoryName = "shared";
    private const string RolesDirectoryName = "roles";
    private static readonly string RoleTemplatesRelativePath = Path.Combine("toolbox", "role-templates.json");

    private readonly string? configuredPackRoot;
    private readonly Lazy<ProcessTemplatePack> pack;

    public ProcessTemplatePackLoader(string? packRoot = null)
    {
        configuredPackRoot = packRoot;
        pack = new Lazy<ProcessTemplatePack>(LoadCore, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public ProcessTemplatePack Load() => pack.Value;

    public ProcessTemplateDefinitionDocument LoadDefinition(string processKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processKey);

        var loadedPack = Load();
        var entry = loadedPack.Manifest.Processes.FirstOrDefault(item =>
            string.Equals(item.Key, processKey.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Process template '{processKey}' is not available in the template pack.");
        var definitionPath = Path.GetFullPath(Path.Combine(
            loadedPack.RootPath,
            Require(entry.RelativePath, "process relative path", loadedPack.RootPath),
            DefinitionFileName));

        var definition = ReadJson(definitionPath, ProcessTemplateJsonContext.Default.ProcessTemplateDefinitionDocument);
        ValidateDefinition(definition, definitionPath);
        ResolveExecutionGuidance(
            definition,
            loadedPack.RootPath,
            Require(entry.RelativePath, "process relative path", loadedPack.RootPath),
            definitionPath);
        return definition;
    }

    public IReadOnlyList<ProcessTemplateLiveRunProfileDocument> LoadLiveRunProfiles()
    {
        var loadedPack = Load();
        var relativePath = loadedPack.Manifest.SeedCatalog.LiveRunProfilesPath;
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return [];
        }

        var path = Path.GetFullPath(Path.Combine(loadedPack.RootPath, relativePath));
        if (!File.Exists(path))
        {
            return [];
        }

        return ReadJson(path, ProcessTemplateJsonContext.Default.ProcessTemplateLiveRunProfileDocumentArray);
    }

    public static string FindPackRoot(string? packRoot = null) => ResolvePackRoot(packRoot);

    private ProcessTemplatePack LoadCore()
    {
        var root = ResolvePackRoot(configuredPackRoot);
        var manifestPath = Path.Combine(root, ManifestFileName);
        var manifest = ReadJson(manifestPath, ProcessTemplateJsonContext.Default.ProcessTemplatePackManifest);
        var definitions = new List<ProcessTemplateDefinitionSummary>(manifest.Processes.Count);
        var loadedDefinitions = new List<(ProcessTemplateDefinitionDocument Definition, string DefinitionPath)>();
        var roleTemplateActions = LoadRoleTemplateActions(root);

        foreach (var entry in manifest.Processes.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = Require(entry.RelativePath, "process relative path", manifestPath);
            var definitionPath = Path.GetFullPath(Path.Combine(root, relativePath, DefinitionFileName));
            var definition = ReadJson(definitionPath, ProcessTemplateJsonContext.Default.ProcessTemplateDefinitionDocument);
            ValidateDefinition(definition, definitionPath);
            ValidateExecutionGuidanceReferences(definition, root, relativePath, definitionPath);
            var key = Require(definition.Key, "definition key", definitionPath);
            if (!string.Equals(key, Require(entry.Key, "process key", manifestPath), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Process template '{definitionPath}' key '{key}' does not match manifest key '{entry.Key}'.");
            }

            loadedDefinitions.Add((definition, definitionPath));

            definitions.Add(new ProcessTemplateDefinitionSummary(
                key,
                relativePath,
                Require(definition.DisplayName, "definition display name", definitionPath),
                Require(definition.Summary, "definition summary", definitionPath),
                NormalizeOptional(definition.Criticality, "Unspecified"),
                NormalizeOptional(definition.OperatingMode, "Unspecified"),
                NormalizeOptional(definition.AutonomyLevel, "Unspecified"),
                File.GetLastWriteTimeUtc(definitionPath),
                new ProcessTemplateDefinitionAuthoringDefaults(
                    NormalizeOptional(definition.ValueStatement, string.Empty),
                    NormalizeOptional(definition.CustomerName, string.Empty),
                    NormalizeOptional(definition.OwnerName, string.Empty),
                    NormalizeOptional(definition.InterfaceContractSummary, string.Empty),
                    NormalizeOptional(definition.ManagerOverrideSummary, string.Empty),
                    NormalizeOptional(definition.GovernanceNotes, string.Empty),
                    NormalizeOptional(definition.ChangeSummary, string.Empty),
                    NormalizeOptional(definition.GovernancePolicySummary, string.Empty),
                    NormalizeOptional(definition.ConstitutionRuleSummary, string.Empty),
                    NormalizeOptional(definition.OperatingModeSummary, string.Empty),
                    NormalizeOptional(definition.SimulationReadinessSummary, string.Empty),
                    definition.Steps.Count,
                    definition.RoleUsages.Count(role => role.IsRequired),
                    definition.Steps.Sum(step => step.ArtifactExpectations.Count(artifact => artifact.IsRequired))),
                BuildRoleAuthoringDefaults(root, relativePath, definition, roleTemplateActions),
                ProcessTemplateStepSummaryBuilder.Build(definition),
                ProcessTemplateCanvasSummaryBuilder.Build(root, definition),
                ProcessTemplateLibrarySummaryBuilder.Build(relativePath, definition)));
        }

        ValidateForwardedChildContextArtifacts(loadedDefinitions);

        return new ProcessTemplatePack(root, manifest, definitions);
    }

    private static IReadOnlyList<ProcessTemplateRoleTemplateActionSummary> LoadRoleTemplateActions(string root)
    {
        var path = Path.Combine(root, RoleTemplatesRelativePath);
        if (!File.Exists(path))
        {
            return [];
        }

        var documents = ReadJson(path, ProcessTemplateJsonContext.Default.ProcessTemplateRoleTemplateActionDocumentArray);
        return documents
            .Select(document => new ProcessTemplateRoleTemplateActionSummary(
                Require(document.ActionId, "role template action id", path),
                Require(document.Label, "role template label", path),
                NormalizeOptional(document.Summary, string.Empty),
                NormalizeOptional(document.TemplateRoleKey, string.Empty),
                NormalizeOptional(document.KeyPrefix, "role"),
                NormalizeOptional(document.DisplayNameTemplate, "Role {ordinal}"),
                NormalizeOptional(document.PreferredExecutorKind, "person"),
                document.DefaultAllocationPercent))
            .ToArray();
    }

    private static ProcessTemplateDefinitionRoleAuthoringDefaults BuildRoleAuthoringDefaults(
        string root,
        string definitionRelativePath,
        ProcessTemplateDefinitionDocument definition,
        IReadOnlyList<ProcessTemplateRoleTemplateActionSummary> roleTemplateActions)
    {
        var roles = definition.RoleUsages
            .Select((role, index) => CreateRoleSummary(root, definitionRelativePath, role, index))
            .ToArray();
        var roleNames = roles.ToDictionary(role => role.Key, role => role.DisplayName, StringComparer.OrdinalIgnoreCase);
        var stepRoleBindings = definition.Steps
            .SelectMany(step => step.RoleAssignments.Select(assignment => CreateStepRoleBinding(step, assignment, roleNames)))
            .ToArray();

        return new ProcessTemplateDefinitionRoleAuthoringDefaults(
            roles,
            roleTemplateActions,
            stepRoleBindings);
    }

    private static void ValidateDefinition(
        ProcessTemplateDefinitionDocument definition,
        string definitionPath)
    {
        ValidateLaunchDriverActivations(definition, definitionPath);

        foreach (var step in definition.Steps)
        {
            ValidateExecutorPreferredSpecializationTags(step, definitionPath, definition.Key);
            ProcessTemplateStepCompletionPolicyValidator.Validate(
                step,
                $"{definitionPath}:{definition.Key}.{step.Key}");
            if (!string.Equals(step.StepKind, "Subprocess", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(step.SubprocessProcessKey))
            {
                ValidateStepExecutionContract(definition, step, definitionPath);
                continue;
            }

            ValidateStepExecutionContract(definition, step, definitionPath);
            ValidateSubprocessContract(definition, step, definitionPath);
        }
    }

    private static void ValidateExecutionGuidanceReferences(
        ProcessTemplateDefinitionDocument definition,
        string packRoot,
        string definitionRelativePath,
        string definitionPath)
    {
        foreach (var step in definition.Steps)
        {
            var references = step.ExecutionGuidanceRefs
                .Select(reference => Require(reference, "execution guidance reference", definitionPath))
                .ToArray();
            var duplicate = references
                .GroupBy(reference => reference, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate is not null)
            {
                throw new InvalidOperationException(
                    $"Process template step '{step.Key}' declares execution guidance reference '{duplicate.Key}' more than once in '{definitionPath}'.");
            }

            foreach (var reference in references)
            {
                var path = ResolveExecutionGuidancePath(packRoot, reference, definitionPath);
                if (!File.Exists(path))
                {
                    throw new InvalidOperationException(
                        $"Process template step '{step.Key}' references missing execution guidance '{reference}' in '{definitionPath}'.");
                }

                if (string.IsNullOrWhiteSpace(File.ReadAllText(path)))
                {
                    throw new InvalidOperationException(
                        $"Process template step '{step.Key}' references empty execution guidance '{reference}' in '{definitionPath}'.");
                }
            }
        }
    }

    private static void ResolveExecutionGuidance(
        ProcessTemplateDefinitionDocument definition,
        string packRoot,
        string definitionRelativePath,
        string definitionPath)
    {
        ValidateExecutionGuidanceReferences(definition, packRoot, definitionRelativePath, definitionPath);
        foreach (var step in definition.Steps)
        {
            step.ResolvedExecutionGuidance = step.ExecutionGuidanceRefs
                .Select(reference =>
                {
                    var path = ResolveExecutionGuidancePath(packRoot, reference, definitionPath);
                    var content = File.ReadAllText(path).Trim();
                    var contentHash = "sha256:" + Convert.ToHexString(
                        SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
                    return new ProcessTemplateExecutionGuidanceDocument(
                        reference.Replace('\\', '/'),
                        content,
                        contentHash);
                })
                .ToArray();
        }
    }

    private static string ResolveExecutionGuidancePath(
        string packRoot,
        string reference,
        string definitionPath)
    {
        if (Path.IsPathRooted(reference))
        {
            throw new InvalidOperationException(
                $"Execution guidance reference '{reference}' in '{definitionPath}' must be pack-relative.");
        }

        var normalizedPackRoot = Path.GetFullPath(packRoot);
        var path = Path.GetFullPath(Path.Combine(
            normalizedPackRoot,
            reference.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = normalizedPackRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedPackRoot
            : normalizedPackRoot + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Execution guidance reference '{reference}' in '{definitionPath}' must remain inside the process template pack.");
        }

        if (!string.Equals(Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Execution guidance reference '{reference}' in '{definitionPath}' must reference a Markdown file.");
        }

        return path;
    }

    private static void ValidateExecutorPreferredSpecializationTags(
        ProcessTemplateDefinitionStepDocument step,
        string definitionPath,
        string definitionKey)
    {
        if (step.ExecutorPreferredSpecializationTags.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                $"Process template step '{definitionPath}:{definitionKey}.{step.Key}' has an empty executor specialization tag.");
        }

        var duplicateTag = step.ExecutorPreferredSpecializationTags
            .GroupBy(tag => tag.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateTag is not null)
        {
            throw new InvalidOperationException(
                $"Process template step '{definitionPath}:{definitionKey}.{step.Key}' declares executor specialization tag '{duplicateTag.Key}' more than once.");
        }
    }

    private static void ValidateLaunchDriverActivations(
        ProcessTemplateDefinitionDocument definition,
        string definitionPath)
    {
        var driverKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var activation in definition.LaunchDriverActivations)
        {
            var driverKey = Require(activation.DriverKey, "launch driver activation key", definitionPath);
            if (!driverKeys.Add(driverKey))
            {
                throw new InvalidOperationException(
                    $"Process template '{definitionPath}:{definition.Key}' declares launch driver '{driverKey}' more than once.");
            }

            foreach (var (settingKey, settingValue) in activation.Settings)
            {
                if (string.IsNullOrWhiteSpace(settingKey) || string.IsNullOrWhiteSpace(settingValue))
                {
                    throw new InvalidOperationException(
                        $"Process template '{definitionPath}:{definition.Key}' launch driver '{driverKey}' has an empty setting key or value.");
                }
            }

            var bindingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var binding in activation.InputArtifactBindings)
            {
                var bindingKey = Require(binding.BindingKey, "launch driver artifact binding key", definitionPath);
                Require(binding.SourceStepKey, "launch driver artifact binding source step key", definitionPath);
                Require(binding.ArtifactExpectationKey, "launch driver artifact binding artifact expectation key", definitionPath);
                Require(binding.PayloadSchema, "launch driver artifact binding payload schema", definitionPath);
                if (!bindingKeys.Add(bindingKey))
                {
                    throw new InvalidOperationException(
                        $"Process template '{definitionPath}:{definition.Key}' launch driver '{driverKey}' declares artifact binding '{bindingKey}' more than once.");
                }
            }
        }
    }

    private static void ValidateStepExecutionContract(
        ProcessTemplateDefinitionDocument definition,
        ProcessTemplateDefinitionStepDocument step,
        string definitionPath)
    {
        var stepPath = $"{definitionPath}:{definition.Key}.{step.Key}";
        var executionClass = ResolveExecutionClass(step);
        if (string.IsNullOrWhiteSpace(executionClass))
        {
            return;
        }

        if (!ProcessTemplateStepExecutionClasses.IsKnown(executionClass))
        {
            throw new InvalidOperationException(
                $"Process template step '{stepPath}' has unknown executionClass '{executionClass}'.");
        }

        if (ProcessTemplateStepExecutionClasses.RequiresDeterministicToolPlan(executionClass) &&
            step.ExecutionContract?.DeterministicToolPlan is null)
        {
            throw new InvalidOperationException(
                $"Process template step '{stepPath}' executionClass '{executionClass}' requires ExecutionContract.DeterministicToolPlan.");
        }

        if (ProcessTemplateStepExecutionClasses.IsRuntimeOwnedToolPlan(executionClass) &&
            string.IsNullOrWhiteSpace(step.ExecutionContract?.RuntimeOwnedExecutorKey))
        {
            throw new InvalidOperationException(
                $"Process template step '{stepPath}' executionClass '{executionClass}' requires ExecutionContract.RuntimeOwnedExecutorKey.");
        }

        if (ProcessTemplateStepExecutionClasses.IsRuntimeOwnedToolPlan(executionClass) &&
            step.ExecutionContract?.DeterministicToolPlan is { } runtimeOwnedPlan &&
            (string.IsNullOrWhiteSpace(runtimeOwnedPlan.PlanKey) ||
             string.IsNullOrWhiteSpace(runtimeOwnedPlan.PlanKind) ||
             string.IsNullOrWhiteSpace(runtimeOwnedPlan.ExecutionPlanLaunchVariable)))
        {
            throw new InvalidOperationException(
                $"Process template step '{stepPath}' runtime-owned deterministic tool plan requires PlanKey, PlanKind, and ExecutionPlanLaunchVariable.");
        }

        if (step.ExecutionContract?.DeterministicToolPlan is { } deterministicToolPlan)
        {
            ValidateDeterministicToolPlan(deterministicToolPlan, stepPath);
        }
    }

    private static void ValidateDeterministicToolPlan(
        ProcessTemplateDeterministicToolPlanDocument plan,
        string stepPath)
    {
        if (plan.Operations.Count == 0)
        {
            throw new InvalidOperationException(
                $"Process template step '{stepPath}' deterministic tool plan must declare at least one operation.");
        }

        if (plan.RequiresReadbackChecks && plan.ReadbackChecks.Count == 0)
        {
            throw new InvalidOperationException(
                $"Process template step '{stepPath}' deterministic tool plan requires at least one readback check.");
        }

        var operationKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var operation in plan.Operations)
        {
            var operationKey = Require(
                operation.Key,
                "deterministic tool plan operation key",
                stepPath);
            Require(
                operation.ToolName,
                $"deterministic tool plan operation '{operationKey}' tool name",
                stepPath);
            Require(
                operation.RequiredReceiptKey,
                $"deterministic tool plan operation '{operationKey}' required receipt key",
                stepPath);
            if (!operationKeys.Add(operationKey))
            {
                throw new InvalidOperationException(
                    $"Process template step '{stepPath}' deterministic tool plan declares operation '{operationKey}' more than once.");
            }

            if (!ProcessToolOperationExecutionPolicyKeys.TryResolveIdempotency(
                    operation.IdempotencyPolicyKey,
                    out _))
            {
                throw new InvalidOperationException(
                    $"Process template step '{stepPath}' deterministic tool plan operation '{operationKey}' has unknown idempotency policy '{operation.IdempotencyPolicyKey}'.");
            }

            if (!ProcessToolOperationExecutionPolicyKeys.TryResolveFailureReconciliation(
                    operation.FailureReconciliationPolicyKey,
                    out var failureReconciliation))
            {
                throw new InvalidOperationException(
                    $"Process template step '{stepPath}' deterministic tool plan operation '{operationKey}' has unknown failure reconciliation policy '{operation.FailureReconciliationPolicyKey}'.");
            }

            if (failureReconciliation ==
                    ProcessToolOperationFailureReconciliationPolicy.AuthoritativeReadbackConvergence &&
                (!plan.RequiresReadbackChecks || plan.ReadbackChecks.Count == 0))
            {
                throw new InvalidOperationException(
                    $"Process template step '{stepPath}' deterministic tool plan operation '{operationKey}' requires authoritative readback checks for failure reconciliation.");
            }
        }
    }

    private static string ResolveExecutionClass(ProcessTemplateDefinitionStepDocument step)
        => !string.IsNullOrWhiteSpace(step.ExecutionClass)
            ? step.ExecutionClass.Trim()
            : step.ExecutionContract?.ExecutionClass.Trim() ?? string.Empty;

    private static void ValidateSubprocessContract(
        ProcessTemplateDefinitionDocument definition,
        ProcessTemplateDefinitionStepDocument step,
        string definitionPath)
    {
        var stepPath = $"{definitionPath}:{definition.Key}.{step.Key}";
        var contract = step.SubprocessContract
            ?? throw new InvalidOperationException($"Process template subprocess step '{stepPath}' must define SubprocessContract.");
        var childDefinitionKey = Require(contract.DefinitionKey, "subprocess contract definition key", stepPath);
        var stepChildDefinitionKey = Require(step.SubprocessProcessKey, "subprocess step child process key", stepPath);
        if (!string.Equals(childDefinitionKey, stepChildDefinitionKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Process template subprocess step '{stepPath}' contract DefinitionKey '{childDefinitionKey}' does not match SubprocessProcessKey '{stepChildDefinitionKey}'.");
        }

        if (contract.LaunchMode != ProcessSubprocessLaunchMode.RuntimeOwned)
        {
            throw new InvalidOperationException($"Process template subprocess step '{stepPath}' must use RuntimeOwned subprocess launch mode.");
        }

        if (contract.MaterializationMode != ProcessSubprocessMaterializationMode.RuntimeSynthesizedParentHandoff)
        {
            throw new InvalidOperationException($"Process template subprocess step '{stepPath}' must use RuntimeSynthesizedParentHandoff materialization mode.");
        }

        var parentExpectationKey = Require(
            contract.ParentProducedArtifactExpectationKey,
            "subprocess parent produced artifact expectation key",
            stepPath);
        if (step.ArtifactExpectations.All(artifact =>
                !string.Equals(artifact.Key, parentExpectationKey, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Process template subprocess step '{stepPath}' contract parent expectation '{parentExpectationKey}' does not match any artifact expectation on the step.");
        }

        if (contract.AcceptedChildOutputs.Count == 0 && contract.AlreadySatisfiedOutput is null)
        {
            throw new InvalidOperationException(
                $"Process template subprocess step '{stepPath}' must define at least one accepted child output or an AlreadySatisfiedOutput.");
        }

        ValidateChildOutputs(contract.AcceptedChildOutputs, step, stepPath, "accepted child output");
        ValidateChildOutputs(contract.NoGoChildOutputs, step, stepPath, "no-go child output");
        ValidateChildOutputDiscriminators(contract, stepPath);
        if (contract.AlreadySatisfiedOutput is not null)
        {
            ValidateChildOutput(contract.AlreadySatisfiedOutput, step, stepPath, "already-satisfied output");
        }

        if (step.AllowsManualSkip && contract.AlreadySatisfiedOutput is null)
        {
            throw new InvalidOperationException(
                $"Process template subprocess step '{stepPath}' allows manual skip but does not define a typed AlreadySatisfiedOutput.");
        }

        ValidateForwardedChildContextArtifactShape(contract, stepPath);
    }

    private static void ValidateForwardedChildContextArtifactShape(
        ProcessSubprocessContract contract,
        string stepPath)
    {
        var bindingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var forwardedArtifact in contract.ForwardedChildContextArtifacts)
        {
            var bindingKey = Require(
                forwardedArtifact.BindingKey,
                "forwarded child context artifact binding key",
                stepPath);
            Require(
                forwardedArtifact.SourceStepKey,
                "forwarded child context artifact source step key",
                stepPath);
            Require(
                forwardedArtifact.ArtifactExpectationKey,
                "forwarded child context artifact expectation key",
                stepPath);
            Require(
                forwardedArtifact.PayloadSchema,
                "forwarded child context artifact payload schema",
                stepPath);
            if (!bindingKeys.Add(bindingKey))
            {
                throw new InvalidOperationException(
                    $"Process template subprocess step '{stepPath}' declares forwarded child context binding '{bindingKey}' more than once.");
            }
        }
    }

    private static void ValidateForwardedChildContextArtifacts(
        IReadOnlyList<(ProcessTemplateDefinitionDocument Definition, string DefinitionPath)> definitions)
    {
        var definitionsByKey = definitions.ToDictionary(
            item => item.Definition.Key,
            StringComparer.OrdinalIgnoreCase);
        foreach (var (parentDefinition, parentDefinitionPath) in definitions)
        {
            foreach (var parentStep in parentDefinition.Steps.Where(step => step.SubprocessContract is not null))
            {
                var contract = parentStep.SubprocessContract!;
                if (contract.ForwardedChildContextArtifacts.Count == 0)
                {
                    continue;
                }

                var parentStepPath = $"{parentDefinitionPath}:{parentDefinition.Key}.{parentStep.Key}";
                var childDefinitionKey = Require(
                    contract.DefinitionKey,
                    "subprocess contract definition key",
                    parentStepPath);
                if (!definitionsByKey.TryGetValue(childDefinitionKey, out var childDefinition))
                {
                    throw new InvalidOperationException(
                        $"Process template subprocess step '{parentStepPath}' declares forwarded child context, but child definition '{childDefinitionKey}' is not present in the template pack.");
                }

                var outputStepKeys = contract.AcceptedChildOutputs
                    .Concat(contract.NoGoChildOutputs)
                    .Append(contract.AlreadySatisfiedOutput)
                    .Where(output => output is not null && !string.IsNullOrWhiteSpace(output.StepKey))
                    .Select(output => output!.StepKey.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                foreach (var forwardedArtifact in contract.ForwardedChildContextArtifacts)
                {
                    var sourceStepKey = forwardedArtifact.SourceStepKey.Trim();
                    var expectationKey = forwardedArtifact.ArtifactExpectationKey.Trim();
                    var payloadSchema = forwardedArtifact.PayloadSchema.Trim();
                    var producerStep = childDefinition.Definition.Steps.FirstOrDefault(step =>
                        string.Equals(step.Key, sourceStepKey, StringComparison.OrdinalIgnoreCase));
                    var expectation = producerStep?.ArtifactExpectations.FirstOrDefault(candidate =>
                        string.Equals(candidate.Key, expectationKey, StringComparison.OrdinalIgnoreCase));
                    if (producerStep is null || expectation is null)
                    {
                        throw new InvalidOperationException(
                            $"Process template subprocess step '{parentStepPath}' forwarded child context binding '{forwardedArtifact.BindingKey}' does not resolve child artifact '{sourceStepKey}/{expectationKey}' in '{childDefinitionKey}'.");
                    }

                    if (!string.Equals(expectation.PayloadSchema?.Trim(), payloadSchema, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Process template subprocess step '{parentStepPath}' forwarded child context binding '{forwardedArtifact.BindingKey}' schema '{payloadSchema}' does not match child artifact '{sourceStepKey}/{expectationKey}' schema '{expectation.PayloadSchema}'.");
                    }

                    foreach (var outputStepKey in outputStepKeys)
                    {
                        var outputStep = childDefinition.Definition.Steps.FirstOrDefault(step =>
                            string.Equals(step.Key, outputStepKey, StringComparison.OrdinalIgnoreCase));
                        if (outputStep is null || !outputStep.ArtifactInputs.Any(input =>
                                string.Equals(input.SourceStepKey, sourceStepKey, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(input.ArtifactExpectationKey, expectationKey, StringComparison.OrdinalIgnoreCase)))
                        {
                            throw new InvalidOperationException(
                                $"Process template subprocess step '{parentStepPath}' forwarded child context binding '{forwardedArtifact.BindingKey}' is not a typed input of child output step '{outputStepKey}' in '{childDefinitionKey}'.");
                        }
                    }
                }
            }
        }
    }

    private static void ValidateChildOutputs(
        IReadOnlyList<ProcessSubprocessChildOutputContract> outputs,
        ProcessTemplateDefinitionStepDocument parentStep,
        string stepPath,
        string outputKind)
    {
        foreach (var output in outputs)
        {
            ValidateChildOutput(output, parentStep, stepPath, outputKind);
        }
    }

    private static void ValidateChildOutput(
        ProcessSubprocessChildOutputContract output,
        ProcessTemplateDefinitionStepDocument parentStep,
        string stepPath,
        string outputKind)
    {
        Require(output.StepKey, $"{outputKind} step key", stepPath);
        Require(
            output.ArtifactExpectationKey,
            $"{outputKind} artifact expectation key",
            stepPath);

        if (!string.IsNullOrWhiteSpace(output.ParentBranchOutcomeKey) &&
            parentStep.BranchOutcomes.All(parentOutcome =>
                !string.Equals(
                    parentOutcome.Key,
                    output.ParentBranchOutcomeKey,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Process template subprocess step '{stepPath}' {outputKind} routes to unknown parent branch '{output.ParentBranchOutcomeKey}'.");
        }
    }

    internal static void ValidateChildOutputDiscriminators(
        ProcessSubprocessContract contract,
        string stepPath)
    {
        var outputs = contract.AcceptedChildOutputs
            .Select(output => (Kind: "accepted", Output: output))
            .Concat(contract.NoGoChildOutputs.Select(output => (Kind: "no-go", Output: output)))
            .ToArray();
        var groups = outputs.GroupBy(
            item => (
                StepKey: item.Output.StepKey.Trim(),
                ArtifactExpectationKey: item.Output.ArtifactExpectationKey.Trim()),
            ChildOutputDiscriminatorComparer.Instance);
        foreach (var group in groups.Where(group => group.Count() > 1))
        {
            var branchKeys = group
                .Select(item => item.Output.BranchOutcomeKey?.Trim() ?? string.Empty)
                .ToArray();
            if (branchKeys.Any(string.IsNullOrWhiteSpace) ||
                branchKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != branchKeys.Length)
            {
                throw new InvalidOperationException(
                    $"Process template subprocess step '{stepPath}' declares overlapping accepted/no-go child outputs for '{group.Key.StepKey}/{group.Key.ArtifactExpectationKey}'. Repeated child output mappings must each define a distinct BranchOutcomeKey.");
            }
        }
    }

    private sealed class ChildOutputDiscriminatorComparer :
        IEqualityComparer<(string StepKey, string ArtifactExpectationKey)>
    {
        internal static ChildOutputDiscriminatorComparer Instance { get; } = new();

        public bool Equals(
            (string StepKey, string ArtifactExpectationKey) left,
            (string StepKey, string ArtifactExpectationKey) right)
            => string.Equals(left.StepKey, right.StepKey, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   left.ArtifactExpectationKey,
                   right.ArtifactExpectationKey,
                   StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string StepKey, string ArtifactExpectationKey) value)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.StepKey),
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.ArtifactExpectationKey));
    }

    private static ProcessTemplateDefinitionRoleSummary CreateRoleSummary(
        string root,
        string definitionRelativePath,
        ProcessTemplateDefinitionRoleUsageDocument usage,
        int index)
    {
        var usageKey = NormalizeOptional(usage.Key, string.Empty);
        var resourceKey = NormalizeOptional(usage.RoleResourceKey, usageKey);
        var resource = string.IsNullOrWhiteSpace(resourceKey)
            ? null
            : TryLoadRoleResource(root, definitionRelativePath, resourceKey);
        var key = NormalizeOptional(usageKey, NormalizeOptional(resource?.Key, $"role-{index + 1}"));
        var displayName = NormalizeOptional(usage.DisplayName, NormalizeOptional(resource?.DisplayName, $"Role {index + 1}"));
        var summary = NormalizeOptional(usage.Notes, NormalizeOptional(resource?.Summary, string.Empty));
        var roleTemplateSourceKey = NormalizeOptional(
            usage.RoleTemplateSourceKey,
            NormalizeOptional(resource?.RoleTemplateSourceKey, string.IsNullOrWhiteSpace(resourceKey) ? string.Empty : $"process-role-template/{resourceKey}"));

        return new ProcessTemplateDefinitionRoleSummary(
            key,
            resourceKey,
            displayName,
            summary,
            NormalizeOptional(usage.Purpose, NormalizeOptional(resource?.Purpose, string.Empty)),
            NormalizeOptional(usage.StaffingIntent, NormalizeOptional(resource?.StaffingIntent, string.Empty)),
            NormalizeOptional(usage.PreferredExecutorKind, NormalizeOptional(resource?.PreferredExecutorKind, "person")),
            NormalizeOptional(usage.PreferredProjectAssignmentRole, NormalizeOptional(resource?.PreferredProjectAssignmentRole, string.Empty)),
            usage.IsRequired,
            usage.AllowsFallback,
            usage.RequiresExplicitApproval,
            usage.DefaultAllocationPercent,
            roleTemplateSourceKey,
            NormalizeOptional(usage.RoleTemplateSnapshotName, NormalizeOptional(resource?.RoleTemplateSnapshotName, string.Empty)),
            NormalizeOptional(usage.SnapshotSummary, NormalizeOptional(resource?.SnapshotSummary, summary)),
            string.IsNullOrWhiteSpace(roleTemplateSourceKey)
                ? "Local role without template source."
                : $"Resolved from {roleTemplateSourceKey}.",
            usage.CanvasX,
            usage.CanvasY)
        {
            WorkflowBinding = usage.WorkflowBinding
        };
    }

    private static ProcessTemplateRoleResourceDocument? TryLoadRoleResource(
        string root,
        string definitionRelativePath,
        string roleResourceKey)
    {
        var localPath = Path.Combine(root, definitionRelativePath, RolesDirectoryName, $"{roleResourceKey}.json");
        if (File.Exists(localPath))
        {
            return ReadJson(localPath, ProcessTemplateJsonContext.Default.ProcessTemplateRoleResourceDocument);
        }

        var sharedPath = Path.Combine(root, SharedDirectoryName, RolesDirectoryName, $"{roleResourceKey}.json");
        return File.Exists(sharedPath)
            ? ReadJson(sharedPath, ProcessTemplateJsonContext.Default.ProcessTemplateRoleResourceDocument)
            : null;
    }

    private static ProcessTemplateDefinitionStepRoleBindingSummary CreateStepRoleBinding(
        ProcessTemplateDefinitionStepDocument step,
        ProcessTemplateDefinitionStepRoleAssignmentDocument assignment,
        IReadOnlyDictionary<string, string> roleNames)
    {
        var roleKey = NormalizeOptional(assignment.RoleKey, string.Empty);
        roleNames.TryGetValue(roleKey, out var roleDisplayName);
        return new ProcessTemplateDefinitionStepRoleBindingSummary(
            NormalizeOptional(step.Key, "step"),
            NormalizeOptional(step.Title, NormalizeOptional(step.Key, "Step")),
            roleKey,
            NormalizeOptional(roleDisplayName, roleKey),
            NormalizeOptional(assignment.ResponsibilityKind, "Responsible"),
            assignment.IsRequired,
            assignment.FallbackOrder,
            NormalizeOptional(assignment.RebindPolicySummary, string.Empty));
    }

    private static T ReadJson<T>(
        string path,
        JsonTypeInfo<T> jsonTypeInfo)
        where T : class
    {
        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize(stream, jsonTypeInfo)
                   ?? throw new InvalidOperationException($"Process template JSON file '{path}' was empty.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidOperationException(
                $"Process template JSON file '{path}' could not be loaded: {exception.Message}",
                exception);
        }
    }

    private static string ResolvePackRoot(string? explicitRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            var normalizedExplicitRoot = Path.GetFullPath(explicitRoot);
            if (File.Exists(Path.Combine(normalizedExplicitRoot, ManifestFileName)))
            {
                return normalizedExplicitRoot;
            }

            if (File.Exists(normalizedExplicitRoot) &&
                string.Equals(Path.GetFileName(normalizedExplicitRoot), ManifestFileName, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetDirectoryName(normalizedExplicitRoot)!;
            }
        }

        var relativeManifestPath = Path.Combine(
            ProcessTemplatePackOptions.TemplatesRootDirectoryName,
            ProcessTemplatePackOptions.ProcessesDirectoryName,
            ManifestFileName);
        var discoveredRoot = FindContainingDirectory(
            relativeManifestPath,
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
        if (!string.IsNullOrWhiteSpace(discoveredRoot))
        {
            return discoveredRoot;
        }

        throw new InvalidOperationException(
            $"Unable to locate {ProcessTemplatePackOptions.DefaultRelativePackRoot}/{ManifestFileName} from the current execution root. " +
            "Configure a process template pack root when the template pack lives outside the repository default layout.");
    }

    private static string? FindContainingDirectory(string relativeFilePath, params string?[] startPaths)
    {
        foreach (var startPath in startPaths
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Select(path => Path.GetFullPath(path!))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var current = new DirectoryInfo(startPath);
            while (current is not null)
            {
                var candidate = Path.Combine(current.FullName, relativeFilePath);
                if (File.Exists(candidate))
                {
                    return Path.GetDirectoryName(candidate);
                }

                current = current.Parent;
            }
        }

        return null;
    }

    private static string Require(string? value, string description, string context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Process template {description} is missing in '{context}'.");
        }

        return value.Trim();
    }

    private static string NormalizeOptional(string? value, string defaultValue)
        => string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();

}

public static class ProcessTemplatePackOptions
{
    public const string TemplatesRootDirectoryName = "Templates";
    public const string ProcessesDirectoryName = "Processes";
    public static readonly string DefaultRelativePackRoot = Path.Combine(TemplatesRootDirectoryName, ProcessesDirectoryName);
}

public sealed record ProcessTemplatePack(
    string RootPath,
    ProcessTemplatePackManifest Manifest,
    IReadOnlyList<ProcessTemplateDefinitionSummary> Definitions);

public sealed record ProcessTemplateDefinitionSummary(
    string Key,
    string RelativePath,
    string DisplayName,
    string Summary,
    string Criticality,
    string OperatingMode,
    string AutonomyLevel,
    DateTimeOffset UpdatedAtUtc,
    ProcessTemplateDefinitionAuthoringDefaults AuthoringDefaults,
    ProcessTemplateDefinitionRoleAuthoringDefaults RoleAuthoringDefaults,
    ProcessTemplateDefinitionStepAuthoringDefaults StepAuthoringDefaults,
    ProcessTemplateDefinitionCanvasAuthoringDefaults CanvasAuthoringDefaults,
    ProcessTemplateDefinitionLibrarySummary LibrarySummary);

public sealed record ProcessTemplateDefinitionAuthoringDefaults(
    string ValueStatement,
    string CustomerName,
    string OwnerName,
    string InterfaceContractSummary,
    string ManagerOverrideSummary,
    string GovernanceNotes,
    string ChangeSummary,
    string GovernancePolicySummary,
    string ConstitutionRuleSummary,
    string OperatingModeSummary,
    string SimulationReadinessSummary,
    int StepCount,
    int RequiredRoleCount,
    int RequiredArtifactExpectationCount);

public sealed record ProcessTemplateDefinitionRoleAuthoringDefaults(
    IReadOnlyList<ProcessTemplateDefinitionRoleSummary> Roles,
    IReadOnlyList<ProcessTemplateRoleTemplateActionSummary> TemplateActions,
    IReadOnlyList<ProcessTemplateDefinitionStepRoleBindingSummary> StepRoleBindings);

public sealed record ProcessTemplateDefinitionRoleSummary(
    string Key,
    string RoleResourceKey,
    string DisplayName,
    string Summary,
    string Purpose,
    string StaffingIntent,
    string PreferredExecutorKind,
    string PreferredProjectAssignmentRole,
    bool IsRequired,
    bool AllowsFallback,
    bool RequiresExplicitApproval,
    int DefaultAllocationPercent,
    string RoleTemplateSourceKey,
    string RoleTemplateSnapshotName,
    string SnapshotSummary,
    string OverrideSummary,
    double CanvasX,
    double CanvasY)
{
    public ProcessWorkflowExecutorBinding? WorkflowBinding { get; init; }
}

public sealed record ProcessTemplateRoleTemplateActionSummary(
    string ActionId,
    string Label,
    string Summary,
    string TemplateRoleKey,
    string KeyPrefix,
    string DisplayNameTemplate,
    string PreferredExecutorKind,
    int DefaultAllocationPercent);

public sealed record ProcessTemplateDefinitionStepRoleBindingSummary(
    string StepKey,
    string StepTitle,
    string RoleKey,
    string RoleDisplayName,
    string ResponsibilityKind,
    bool IsRequired,
    int FallbackOrder,
    string RebindPolicySummary);

public sealed record ProcessTemplateDefinitionStepAuthoringDefaults(
    IReadOnlyList<ProcessTemplateDefinitionStepAuthoringSummary> Steps);

public sealed record ProcessTemplateDefinitionStepAuthoringSummary(
    int Order,
    string Key,
    string Title,
    string Subtitle,
    string Notes,
    string StepKind,
    int TargetLeadHours,
    bool AllowsManualSkip,
    bool AllowsSafeRefusal,
    bool RequiresApproval,
    bool RequiresDecisionRecord,
    string DecisionRoleKey,
    string InputContractSummary,
    string OutputContractSummary,
    string EvidenceContractSummary,
    string DecisionRightsSummary,
    string ExceptionPolicySummary,
    IReadOnlyList<string> AllowedOperations,
    string OperationTargetScope,
    ProcessCapabilityScope CapabilityScope,
    string SubprocessProcessKey,
    string SubprocessDefinitionSnapshotName,
    ProcessSubprocessContract? SubprocessContract,
    IReadOnlyList<ProcessTemplateDefinitionStepBranchOutcomeSummary> BranchOutcomes,
    IReadOnlyList<ProcessTemplateDefinitionStepRoleBindingSummary> RoleBindings,
    IReadOnlyList<ProcessTemplateDefinitionStepArtifactExpectationSummary> ArtifactExpectations);

public sealed record ProcessTemplateDefinitionStepBranchOutcomeSummary(
    string Key,
    string Title,
    string Description,
    string RouteTargetKind,
    string RouteTargetStepKey,
    string RouteTargetArtifactExpectationKey,
    bool IsBackwardRoute,
    int LoopBudgetMaximumRepeats,
    string LoopFingerprintPolicyKey,
    string LoopEscalationTargetKind);

public sealed record ProcessTemplateDefinitionStepArtifactExpectationSummary(
    string Key,
    string TemplateKey,
    string Title,
    string ArtifactKind,
    bool IsRequired,
    string TrustRequirement,
    string SensitivityLevel,
    int RetentionDays,
    string WorkflowOutputId,
    string WorkflowOutputName,
    string WorkflowOutputKind,
    Guid? SubprocessChildArtifactExpectationId,
    string SubprocessChildStepKey,
    string SubprocessChildArtifactTitle,
    string AllowedFutureUsageSummary,
    string ValidationRequirementSummary);

public sealed class ProcessTemplatePackManifest
{
    public string PackKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public DateTimeOffset GeneratedAtUtc { get; set; }

    public ProcessTemplateSeedCatalogDocument SeedCatalog { get; set; } = new();

    public List<ProcessTemplateManifestProcessEntry> Processes { get; set; } = [];
}

public sealed class ProcessTemplateSeedCatalogDocument
{
    public string BaselineScenariosPath { get; set; } = string.Empty;

    public string LiveRunProfilesPath { get; set; } = string.Empty;
}

public sealed class ProcessTemplateManifestProcessEntry
{
    public string Key { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;
}

public sealed class ProcessTemplateLiveRunProfileDocument
{
    public string Key { get; set; } = string.Empty;

    public string ProcessTemplateKey { get; set; } = string.Empty;

    public string RunNameTemplate { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string OperatingMode { get; set; } = string.Empty;

    public string TriggerReasonTemplate { get; set; } = string.Empty;

    public List<ProcessTemplateLiveRunAssignmentDocument> Assignments { get; set; } = [];

    public List<ProcessTemplateLiveRunAcceptanceCriterionDocument> AcceptanceCriteria { get; set; } = [];

    public List<string> RequiredProofKinds { get; set; } = [];
}

public sealed class ProcessTemplateLiveRunAssignmentDocument
{
    public string StepKey { get; set; } = string.Empty;

    public string RoleKey { get; set; } = string.Empty;

    public string DisplayNameTemplate { get; set; } = string.Empty;

    public string ExecutorKind { get; set; } = string.Empty;

    public string BindingReason { get; set; } = string.Empty;

    public ProcessWorkflowExecutorBinding? WorkflowBinding { get; set; }
}

public sealed class ProcessTemplateLiveRunAcceptanceCriterionDocument
{
    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

public sealed class ProcessTemplateDefinitionDocument
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Criticality { get; set; } = string.Empty;

    public string OperatingMode { get; set; } = string.Empty;

    public string AutonomyLevel { get; set; } = string.Empty;

    public string ValueStatement { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string OwnerName { get; set; } = string.Empty;

    public string InterfaceContractSummary { get; set; } = string.Empty;

    public string ManagerOverrideSummary { get; set; } = string.Empty;

    public string GovernanceNotes { get; set; } = string.Empty;

    public string ChangeSummary { get; set; } = string.Empty;

    public string GovernancePolicySummary { get; set; } = string.Empty;

    public string ConstitutionRuleSummary { get; set; } = string.Empty;

    public string OperatingModeSummary { get; set; } = string.Empty;

    public string SimulationReadinessSummary { get; set; } = string.Empty;

    public List<ProcessTemplateDriverActivationDocument> LaunchDriverActivations { get; set; } = [];

    public List<ProcessTemplateDefinitionRoleUsageDocument> RoleUsages { get; set; } = [];

    public List<ProcessTemplateDefinitionStepDocument> Steps { get; set; } = [];
}

public sealed class ProcessTemplateDefinitionRoleUsageDocument
{
    public string Key { get; set; } = string.Empty;

    public string RoleResourceKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public string StaffingIntent { get; set; } = string.Empty;

    public string PreferredExecutorKind { get; set; } = string.Empty;

    public ProcessWorkflowExecutorBinding? WorkflowBinding { get; set; }

    public string PreferredProjectAssignmentRole { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public bool AllowsFallback { get; set; }

    public bool RequiresExplicitApproval { get; set; }

    public int DefaultAllocationPercent { get; set; }

    public string RoleTemplateSourceKey { get; set; } = string.Empty;

    public string RoleTemplateSnapshotName { get; set; } = string.Empty;

    public string SnapshotSummary { get; set; } = string.Empty;

    public double CanvasX { get; set; }

    public double CanvasY { get; set; }

    public string Notes { get; set; } = string.Empty;
}

public sealed class ProcessTemplateDefinitionStepDocument
{
    public int Order { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string StepKind { get; set; } = string.Empty;

    public int TargetLeadHours { get; set; }

    public bool AllowsManualSkip { get; set; }

    public bool AllowsSafeRefusal { get; set; }

    public bool RequiresApproval { get; set; }

    public bool RequiresDecisionRecord { get; set; }

    public bool AllowsCompletedOutcomeWithOpenIssues { get; set; }

    public string InputContractSummary { get; set; } = string.Empty;

    public string OutputContractSummary { get; set; } = string.Empty;

    public string EvidenceContractSummary { get; set; } = string.Empty;

    public string DecisionRightsSummary { get; set; } = string.Empty;

    public string ExceptionPolicySummary { get; set; } = string.Empty;

    public List<string> ExecutionGuidanceRefs { get; set; } = [];

    [JsonIgnore]
    public IReadOnlyList<ProcessTemplateExecutionGuidanceDocument> ResolvedExecutionGuidance { get; set; } = [];

    public List<string> AllowedOperations { get; set; } = [];

    public string OperationTargetScope { get; set; } = string.Empty;

    public ProcessCapabilityScope CapabilityScope { get; set; } = ProcessCapabilityScope.Empty;

    public string DependsOnStepKey { get; set; } = string.Empty;

    public string DependsOnBranchOutcomeKey { get; set; } = string.Empty;

    public string DecisionRoleKey { get; set; } = string.Empty;

    public List<string> ExecutorPreferredSpecializationTags { get; set; } = [];

    public string SubprocessProcessKey { get; set; } = string.Empty;

    public string SubprocessDefinitionSnapshotName { get; set; } = string.Empty;

    public ProcessSubprocessContract? SubprocessContract { get; set; }

    public string ExecutionClass { get; set; } = string.Empty;

    public ProcessTemplateStepExecutionContractDocument? ExecutionContract { get; set; }

    public ProcessTemplateStepCompletionPolicyDocument? CompletionPolicy { get; set; }

    public double CanvasX { get; set; }

    public double CanvasY { get; set; }

    public double BranchCanvasX { get; set; }

    public double BranchCanvasY { get; set; }

    public List<ProcessTemplateDefinitionStepDependencyDocument> Dependencies { get; set; } = [];

    public List<ProcessTemplateDefinitionStepRoleAssignmentDocument> RoleAssignments { get; set; } = [];

    public List<ProcessTemplateDefinitionArtifactExpectationDocument> ArtifactExpectations { get; set; } = [];

    public List<ProcessTemplateDefinitionArtifactInputDocument> ArtifactInputs { get; set; } = [];

    public List<ProcessTemplateDefinitionStepBranchOutcomeDocument> BranchOutcomes { get; set; } = [];
}

public sealed record ProcessTemplateExecutionGuidanceDocument(
    string Reference,
    string Content,
    string ContentHash);

public sealed class ProcessTemplateDefinitionArtifactInputDocument
{
    public string SourceStepKey { get; set; } = string.Empty;

    public string ArtifactExpectationKey { get; set; } = string.Empty;
}

public sealed class ProcessTemplateDefinitionStepDependencyDocument
{
    public string DependsOnStepKey { get; set; } = string.Empty;

    public string DependsOnBranchOutcomeKey { get; set; } = string.Empty;
}

public sealed class ProcessTemplateDefinitionStepBranchOutcomeDocument
{
    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool AllowsCompletedOutcomeWithOpenIssues { get; set; }

    public string RouteTargetKind { get; set; } = string.Empty;

    public string RouteTargetStepKey { get; set; } = string.Empty;

    public string RouteTargetArtifactExpectationKey { get; set; } = string.Empty;

    public bool IsBackwardRoute { get; set; }

    public int LoopBudgetMaximumRepeats { get; set; }

    public string LoopFingerprintPolicyKey { get; set; } = string.Empty;

    public string LoopEscalationTargetKind { get; set; } = string.Empty;
}

public sealed class ProcessTemplateDefinitionStepRoleAssignmentDocument
{
    public string RoleKey { get; set; } = string.Empty;

    public string ResponsibilityKind { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public int FallbackOrder { get; set; }

    public string RebindPolicySummary { get; set; } = string.Empty;
}

public sealed class ProcessTemplateDefinitionArtifactExpectationDocument
{
    public string Key { get; set; } = string.Empty;

    public string TemplateKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string ArtifactKind { get; set; } = string.Empty;

    public string PayloadSchema { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public string TrustRequirement { get; set; } = string.Empty;

    public string SensitivityLevel { get; set; } = string.Empty;

    public int RetentionDays { get; set; }

    public string WorkflowOutputId { get; set; } = string.Empty;

    public string WorkflowOutputName { get; set; } = string.Empty;

    public string WorkflowOutputKind { get; set; } = string.Empty;

    public Guid? SubprocessChildArtifactExpectationId { get; set; }

    public string SubprocessChildStepKey { get; set; } = string.Empty;

    public string SubprocessChildArtifactTitle { get; set; } = string.Empty;

    public string AllowedFutureUsageSummary { get; set; } = string.Empty;

    public string ValidationRequirementSummary { get; set; } = string.Empty;
}

public sealed class ProcessTemplateRoleResourceDocument
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public string StaffingIntent { get; set; } = string.Empty;

    public string PreferredExecutorKind { get; set; } = string.Empty;

    public string PreferredProjectAssignmentRole { get; set; } = string.Empty;

    public string RoleTemplateSourceKey { get; set; } = string.Empty;

    public string RoleTemplateSnapshotName { get; set; } = string.Empty;

    public string SnapshotSummary { get; set; } = string.Empty;
}

public sealed class ProcessTemplateRoleTemplateActionDocument
{
    public string ActionId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string TemplateRoleKey { get; set; } = string.Empty;

    public string KeyPrefix { get; set; } = string.Empty;

    public string DisplayNameTemplate { get; set; } = string.Empty;

    public string PreferredExecutorKind { get; set; } = string.Empty;

    public int DefaultAllocationPercent { get; set; }
}

public sealed class ProcessTemplateStepTemplateActionDocument
{
    public string ActionId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public ProcessTemplateDefinitionStepDocument Template { get; set; } = new();
}
