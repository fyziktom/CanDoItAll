using System.Text.Json;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectTaskAssemblyCompatibilityTests {
    [Fact]
    public void Existing_assembly_qualified_contracts_resolve_and_keep_unknown_wire_values() {
        Type[] contracts = [
            typeof(ProjectWorkItemEffortUnit), typeof(ProjectTaskEstimate), typeof(ProjectTaskEstimateInputKeys),
            typeof(ProjectTaskEstimatePolicy), typeof(ProjectTaskExecutionSnapshot), typeof(ProjectTaskExecutionStatePolicy),
            typeof(ProjectTaskExecutionState), typeof(ProjectProgressPolicy), typeof(ProjectStructureTaskActionIds),
            typeof(ProjectStructureTaskResourceKind), typeof(ProjectStructureTaskResourceSelection), typeof(ProjectStructureTaskResourceOption),
            typeof(ProjectStructureTaskResourceCostQuoteStatus), typeof(ProjectStructureTaskResourceCostSource),
            typeof(ProjectStructureTaskResourceCostSourcePolicy), typeof(ProjectStructureTaskResourceCostRequest), typeof(ProjectStructureTaskResourceCostQuote),
            typeof(ProjectStructureTaskEstimateEditor), typeof(ProjectStructureTaskExecutionEditor), typeof(ProjectStructureTaskResourcePicker),
            typeof(ProjectStructureTaskResourceCostEstimator), typeof(ProjectTaskQuoteContext), typeof(ProjectStructureTaskEditDialogResult),
            typeof(ProjectStructureTaskDialogResult)
        ];
        foreach (var contract in contracts) {
            Assert.Same(contract, Type.GetType($"{contract.FullName}, CanDoItAll.Modules.Workbench", throwOnError: true));
        }
        const string json = "{\"ExpectedEffortHours\":1.123456789,\"ExpectedEffortUnit\":713,\"ExpectedCostAmount\":0,\"ExpectedCostCurrencyCode\":\"XYZ\"}";
        var estimate = JsonSerializer.Deserialize<ProjectTaskEstimate>(json);
        Assert.NotNull(estimate);
        Assert.Equal(json, JsonSerializer.Serialize(estimate));
        Assert.Equal((ProjectTaskExecutionState)713, JsonSerializer.Deserialize<ProjectTaskExecutionState>("713"));
    }
}
