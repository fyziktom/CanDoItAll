using CanDoItAll.AgentFramework.Maf;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafContextManifestBuilderTests
{
    [Fact]
    public void Tool_schema_estimate_includes_serialized_parameter_schema()
    {
        const string toolName = "context_manifest_probe";
        const string description = "Measures the complete provider-visible tool contract.";
        var function = AIFunctionFactory.Create(
            (string path, int take) => $"{path}:{take}",
            toolName,
            description);

        var estimatedCharacters = MafContextManifestBuilder.EstimateToolSchemaChars(function);

        Assert.Equal(
            toolName.Length +
            description.Length +
            function.JsonSchema.GetRawText().Length +
            function.GetType().Name.Length +
            128,
            estimatedCharacters);
        Assert.Contains("path", function.JsonSchema.GetRawText(), StringComparison.Ordinal);
        Assert.Contains("take", function.JsonSchema.GetRawText(), StringComparison.Ordinal);
    }
}
