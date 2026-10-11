using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

internal static class ProcessDefinitionCompilation {
    internal static ProcessInstancePlanCompileRequest CreateRequest(
        ProcessTemplateKernelBuildResult kernelBuild,
        ProcessTemplateDefinitionDocument definition,
        string contentVersion,
        ProcessLaunchDriverCatalog driverCatalog) {
        var templateComponent = new ProcessTemplateComponentReference(
            new TemplateComponentId(ProcessTemplateKernelBuilder.CreateDefinitionId(definition.Key).Value),
            definition.Key,
            contentVersion,
            kernelBuild.DefinitionContentHash);

        return new ProcessInstancePlanCompileRequest(
            new ProcessPlanCompileSource(
                SourceSchemaVersion: "runtime/1.0",
                TargetSchemaVersion: "runtime/1.0",
                kernelBuild.Definition,
                kernelBuild.DefinitionContentHash,
                driverCatalog.DriverCatalog,
                new ProcessCapabilityRequest(
                    driverCatalog.RequiredCapabilityTags,
                    driverCatalog.RequiredCapabilityTags,
                    new HashSet<CapabilityTag>())
                {
                    HostCapabilities = driverCatalog.HostCapabilities
                },
                [templateComponent],
                [templateComponent],
                [],
                new HashSet<string>(StringComparer.Ordinal),
                [],
                new ProcessManagerPlanRequest(
                    ManagerStrategyId: null,
                    RecoveryStrategyIds: [],
                    ResupplyStrategyIds: [],
                    PolicyHash: ProcessAuthoringCodec.Hash(definition.GovernancePolicySummary)),
                new ProcessMonitoringPlanRequest(
                    Enabled: true,
                    ProjectionConfigHash: ProcessAuthoringCodec.Hash("runtime-projections:v1")),
                new ProcessSecurityPlanRequest(
                    GovernancePolicyHash: ProcessAuthoringCodec.Hash(definition.GovernancePolicySummary),
                    RequiredApprovalKeys: [])),
            Subprocesses: []);
    }

}
