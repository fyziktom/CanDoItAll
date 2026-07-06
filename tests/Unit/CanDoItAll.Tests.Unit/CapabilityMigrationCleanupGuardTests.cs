using System.Text.RegularExpressions;

namespace CanDoItAll.Tests.Unit;

public sealed class CapabilityMigrationCleanupGuardTests
{
    [Fact]
    public void SB12_INV_CLEANUP_001_seed_builder_uses_template_materializer_only_for_default_capabilities()
    {
        var source = ReadRepositoryFile("src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs");

        Assert.Contains("new CapabilityTemplatePackLoader", source, StringComparison.Ordinal);
        Assert.Contains("CapabilityTemplateSeedMaterializer.MaterializeDefaultCapabilities", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateFileSkillCapability", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateInlineSkillCapability", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateToolCapability", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateAiContextCapability", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InlineSkillResourceSeed", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SB12_INV_CLEANUP_002_maf_runtime_does_not_define_private_capability_descriptor_dtos()
    {
        var source = ReadRepositoryFiles("src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities");

        Assert.Contains("CapabilityExposureDescriptor", source, StringComparison.Ordinal);
        Assert.DoesNotMatch(
            new Regex(@"private\s+sealed\s+(record|class)\s+\w*Capability\w*Descriptor", RegexOptions.Multiline),
            source);
    }

    [Fact]
    public void SB12_INV_CLEANUP_003_runtime_suppression_uses_shared_evaluator()
    {
        var accessSource = ReadRepositoryFiles("src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities");
        var runtimeProviderComposerSource = ReadRepositoryFile("src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeToolProviderComposer.cs");
        var policySource = ReadRepositoryFile("src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Access.Policies.cs");

        Assert.Contains("ICapabilityAccessPolicyEvaluator", accessSource, StringComparison.Ordinal);
        Assert.Contains("RuntimeToolProviderAccessFilter", runtimeProviderComposerSource, StringComparison.Ordinal);
        Assert.Contains("request.AccessPlan.Evaluator.Evaluate", runtimeProviderComposerSource, StringComparison.Ordinal);
        Assert.Contains("result.ToEffectiveSet()", accessSource, StringComparison.Ordinal);
        Assert.Contains("CapabilitySelector.ByTag(CapabilityTag.Create(\"configured\"))", policySource, StringComparison.Ordinal);
        Assert.DoesNotContain("EvaluateRuntimeToolAccess", accessSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AppendRuntimeToolAccessResult", accessSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ShouldExcludeSkillsForProcessStep", accessSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveProcessScopedWorkspaceToolAccess", accessSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FilterCapabilitiesForProcess", accessSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SB12_INV_CLEANUP_004_runtime_access_logic_does_not_compare_raw_selector_values()
    {
        var mafSource = ReadRepositoryFiles("src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities");
        var evaluatorSource = ReadRepositoryFile("src/MAF/Capabilities/CanDoItAll.AgentFramework.Capabilities.Access/CapabilityAccessPolicyEvaluator.cs");

        Assert.Contains("CapabilitySelector.By", mafSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CapabilitySelectorKind", mafSource, StringComparison.Ordinal);
        Assert.DoesNotContain("selector.Value", mafSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("selector.RuntimeToolName == candidate.RuntimeToolName", evaluatorSource, StringComparison.Ordinal);
        Assert.Contains("selector.McpServerKey == candidate.McpServerKey", evaluatorSource, StringComparison.Ordinal);
        Assert.Contains("selector.McpToolName == candidate.McpToolName", evaluatorSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SB12_INV_CLEANUP_005_external_tool_and_mcp_setup_failures_remain_structured()
    {
        var source = string.Join(
            Environment.NewLine,
            ReadRepositoryFile("src/MAF/Tools/CanDoItAll.AgentFramework.Tools/External/ExternalProcessToolInvoker.cs"),
            ReadRepositoryFile("src/MAF/Tools/CanDoItAll.AgentFramework.Tools/External/ExternalHttpToolInvoker.cs"),
            ReadRepositoryFile("src/MAF/Tools/CanDoItAll.AgentFramework.Tools/External/ToolDiagnostics.cs"),
            ReadRepositoryFile("src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp/Runtime/McpSetupTestService.cs"),
            ReadRepositoryFile("src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp/Diagnostics/McpDiagnostics.cs"));

        Assert.Contains("CapabilityDiagnosticCategory.ProcessStart", source, StringComparison.Ordinal);
        Assert.Contains("CapabilityDiagnosticCategory.JsonParse", source, StringComparison.Ordinal);
        Assert.Contains("CapabilityDiagnosticCategory.McpListTools", source, StringComparison.Ordinal);
        Assert.Contains("repairHint", source, StringComparison.Ordinal);
        Assert.Contains("correlationId", source, StringComparison.Ordinal);
        Assert.Contains("MaskedDetail", ReadRepositoryFile("src/MAF/Capabilities/CanDoItAll.AgentFramework.Capabilities.Abstractions/CapabilityModels.cs"), StringComparison.Ordinal);
        Assert.DoesNotContain("Error on MCP start", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MCP unavailable", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Tool setup failed", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Generic setup error", source, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadRepositoryFiles(string relativeDirectory)
    {
        var directory = Path.Combine(FindRepositoryRoot(), relativeDirectory);
        return string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(File.ReadAllText));
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        return File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
