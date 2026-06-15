using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Builder;

public sealed record ProcessPlanCompileSource(
    string SourceSchemaVersion,
    string TargetSchemaVersion,
    ProcessDefinitionKernel Definition,
    string DefinitionContentHash,
    ProcessDriverCatalog DriverCatalog,
    ProcessCapabilityRequest CapabilityRequest,
    IReadOnlyList<ProcessTemplateComponentReference> RequiredTemplateComponents,
    IReadOnlyList<ProcessTemplateComponentReference> AvailableTemplateComponents,
    IReadOnlyList<ProcessTemplateLocalOverridePatch> LocalOverridePatches,
    IReadOnlySet<string> ChangedGlobalTemplatePointers,
    IReadOnlyList<ProcessArtifactReference> InitialArtifactReferences,
    ProcessManagerPlanRequest Manager,
    ProcessMonitoringPlanRequest Monitoring,
    ProcessSecurityPlanRequest Security,
    ProcessTemplateMigrationRegistry? MigrationRegistry = null);

public sealed record ProcessInstancePlanCompileRequest(
    ProcessPlanCompileSource Source,
    IReadOnlyList<SubprocessCompileRequest> Subprocesses,
    int MaximumSubprocessDepth = 8)
{
    public ProcessInstancePlanCompileRequest(
        string SourceSchemaVersion,
        string TargetSchemaVersion,
        ProcessDefinitionKernel Definition,
        string DefinitionContentHash,
        ProcessDriverCatalog DriverCatalog,
        ProcessCapabilityRequest CapabilityRequest,
        IReadOnlyList<ProcessTemplateComponentReference> RequiredTemplateComponents,
        IReadOnlyList<ProcessTemplateComponentReference> AvailableTemplateComponents,
        IReadOnlyList<ProcessTemplateLocalOverridePatch> LocalOverridePatches,
        IReadOnlySet<string> ChangedGlobalTemplatePointers,
        IReadOnlyList<ProcessArtifactReference> InitialArtifactReferences,
        ProcessManagerPlanRequest Manager,
        ProcessMonitoringPlanRequest Monitoring,
        ProcessSecurityPlanRequest Security,
        IReadOnlyList<SubprocessCompileRequest> Subprocesses,
        ProcessTemplateMigrationRegistry? MigrationRegistry = null,
        int MaximumSubprocessDepth = 8)
        : this(
            new ProcessPlanCompileSource(
                SourceSchemaVersion,
                TargetSchemaVersion,
                Definition,
                DefinitionContentHash,
                DriverCatalog,
                CapabilityRequest,
                RequiredTemplateComponents,
                AvailableTemplateComponents,
                LocalOverridePatches,
                ChangedGlobalTemplatePointers,
                InitialArtifactReferences,
                Manager,
                Monitoring,
                Security,
                MigrationRegistry),
            Subprocesses,
            MaximumSubprocessDepth)
    {
    }

    public string SourceSchemaVersion => Source.SourceSchemaVersion;

    public string TargetSchemaVersion => Source.TargetSchemaVersion;

    public ProcessDefinitionKernel Definition => Source.Definition;

    public string DefinitionContentHash => Source.DefinitionContentHash;

    public ProcessDriverCatalog DriverCatalog => Source.DriverCatalog;

    public ProcessCapabilityRequest CapabilityRequest => Source.CapabilityRequest;

    public IReadOnlyList<ProcessTemplateComponentReference> RequiredTemplateComponents => Source.RequiredTemplateComponents;

    public IReadOnlyList<ProcessTemplateComponentReference> AvailableTemplateComponents => Source.AvailableTemplateComponents;

    public IReadOnlyList<ProcessTemplateLocalOverridePatch> LocalOverridePatches => Source.LocalOverridePatches;

    public IReadOnlySet<string> ChangedGlobalTemplatePointers => Source.ChangedGlobalTemplatePointers;

    public IReadOnlyList<ProcessArtifactReference> InitialArtifactReferences => Source.InitialArtifactReferences;

    public ProcessManagerPlanRequest Manager => Source.Manager;

    public ProcessMonitoringPlanRequest Monitoring => Source.Monitoring;

    public ProcessSecurityPlanRequest Security => Source.Security;

    public ProcessTemplateMigrationRegistry? MigrationRegistry => Source.MigrationRegistry;
}

public sealed record ProcessManagerPlanRequest(
    StrategyId? ManagerStrategyId,
    IReadOnlyList<StrategyId> RecoveryStrategyIds,
    IReadOnlyList<StrategyId> ResupplyStrategyIds,
    string PolicyHash);

public sealed record ProcessMonitoringPlanRequest(
    bool Enabled,
    string ProjectionConfigHash);

public sealed record ProcessSecurityPlanRequest(
    string GovernancePolicyHash,
    IReadOnlyList<string> RequiredApprovalKeys);

public sealed record SubprocessCompileRequest(
    ProcessDefinitionId ParentDefinitionId,
    ProcessDefinitionVersionId ParentDefinitionVersionId,
    ProcessStepDefinitionId ParentStepDefinitionId,
    ProcessPlanCompileSource ChildSource,
    string ParentToChildArtifactProjectionHash,
    string ChildToParentArtifactProjectionHash,
    string CancellationPolicyHash,
    string EscalationPolicyHash);
