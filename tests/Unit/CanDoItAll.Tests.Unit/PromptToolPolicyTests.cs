using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.AgentFramework;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class PromptToolPolicyTests {
    [Theory]
    [InlineData(PromptGalleryToolPolicy.PromptGallerySearch, false, false)]
    [InlineData(PromptGalleryToolPolicy.PromptGalleryItemGet, false, false)]
    [InlineData(PromptGalleryToolPolicy.PromptGalleryCatalogSearch, false, true)]
    [InlineData(PromptGalleryToolPolicy.PromptGalleryItemEditorGet, false, false)]
    [InlineData(PromptGalleryToolPolicy.PromptGalleryDraftCreate, true, true)]
    [InlineData(PromptGalleryToolPolicy.PromptGalleryDraftUpdate, true, true)]
    [InlineData(PromptGalleryToolPolicy.PromptGalleryVersionCreate, true, true)]
    public void Owner_metadata_preserves_existing_contract_and_session_fingerprint(string name, bool mutation, bool sensitive) {
        var policies = new AgentToolPolicyCatalog(PromptGalleryToolPolicy.Capabilities);
        Assert.True(policies.TryResolve(name, out var policy));
        Assert.False(ToolCapabilityRegistry.TryResolve(name, out _));
        Assert.Equal(ToolInvocationClassification.Unknown, AgentToolPolicyCatalog.BuiltIn.Classify(name));
        Assert.Equal(mutation ? ToolInvocationClassification.Mutation : ToolInvocationClassification.Read, policy.Classification);
        Assert.Equal(mutation, policy.RequiresApprovalByDefault);
        Assert.Equal(mutation, policy.IsStateChanging);
        Assert.Equal(mutation ? ToolCapabilitySideEffectKind.InternalStateMutation : ToolCapabilitySideEffectKind.InternalDataRead, policy.SideEffectKind);
        Assert.Equal(ToolCapabilityOperationRequirementKind.None, policy.OperationRequirementKind);
        Assert.Empty(policy.OperationRequirements);
        Assert.Empty(policy.TargetScopeRequirements);
        Assert.False(policy.CanMutateProduct);
        Assert.False(policy.CanExecuteExternalAction);
        Assert.Equal(!mutation, policy.CanReadExternalTarget);
        Assert.False(policy.CanWriteManagedArtifact);
        Assert.Equal(ToolCapabilityBrowserProofRole.None, policy.BrowserProofRole);
        Assert.Equal(mutation ? ToolCapabilityIdempotencyDescriptor.StateChanging : ToolCapabilityIdempotencyDescriptor.Idempotent, policy.IdempotencyDescriptor);
        Assert.Equal(sensitive ? "prompt-curator-approval-redacted-v1" : null, policy.BusinessArgumentRetentionScheme);

        var function = AIFunctionFactory.Create(() => "ok", name, "Stable tool contract.");
        AITool tool = mutation ? new ApprovalRequiredAIFunction(function) : function;
        var legacyEntry = string.Join((char)0x1f, name, mutation ? "Mutation" : "Read", mutation ? "approval" : "direct", function.JsonSchema.GetRawText());
        var legacyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(legacyEntry))).ToLowerInvariant();
        Assert.Equal(legacyHash, MafToolsetFingerprint.ComputeContractFingerprint([tool], policies));
    }

    [Fact]
    public void Catalog_copies_mutable_contribution_collections_and_rejects_duplicate_ownership() {
        var operations = new[] { "operation-before" };
        var targets = new[] { "target-before" };
        var source = PromptGalleryToolPolicy.Capabilities[0] with {
            OperationRequirements = [new ToolCapabilityProcessOperationRequirement(operations)],
            TargetScopeRequirements = targets
        };
        var policies = new AgentToolPolicyCatalog([source]);
        operations[0] = "operation-after";
        targets[0] = "target-after";
        Assert.True(policies.TryResolve(source.Name, out var saved));
        Assert.Equal("operation-before", Assert.Single(Assert.Single(saved.OperationRequirements).AnyOf));
        Assert.Equal("target-before", Assert.Single(saved.TargetScopeRequirements));
        Assert.Throws<ArgumentException>(() => new AgentToolPolicyCatalog([source, source]));
        Assert.Throws<ArgumentException>(() => new AgentToolPolicyCatalog([ToolCapabilityRegistry.Capabilities.First()]));
    }

    [Fact]
    public void Module_composition_resolves_all_seven_policies_without_duplicate_registration() {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddAgentFrameworkModule(configuration);
        services.AddAgentFrameworkModule(configuration);
        using var provider = services.BuildServiceProvider();
        var policies = provider.GetRequiredService<AgentToolPolicyCatalog>();
        Assert.Equal(7, provider.GetServices<ToolCapabilityMetadata>().Count());
        Assert.All(PromptGalleryToolPolicy.Capabilities, policy => Assert.True(policies.TryResolve(policy.Name, out _)));
        Assert.Same(policies, provider.GetRequiredService<AgentToolPolicyCatalog>());
    }

    [Fact]
    public void Legacy_approval_audit_preserves_scheme_hash_and_private_content_redaction() {
        var policies = new AgentToolPolicyCatalog(PromptGalleryToolPolicy.Capabilities);
        const string arguments = "{\"request\":{\"promptArtifactId\":\"item-42\",\"content\":\"private-body\",\"title\":\"private-title\"}}";
        var audit = AgentToolInvocationPolicyMetadata.ProtectApprovalArgumentsForAudit(PromptGalleryToolPolicy.PromptGalleryDraftUpdate, arguments, policies);
        using var parsed = JsonDocument.Parse(audit);
        Assert.Equal("prompt-curator-approval-redacted-v1", parsed.RootElement.GetProperty("retentionScheme").GetString());
        Assert.Equal(64, parsed.RootElement.GetProperty("argumentsSha256").GetString()!.Length);
        Assert.Contains("item-42", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("private-body", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("private-title", audit, StringComparison.Ordinal);
        Assert.Equal(audit, AgentToolInvocationPolicyMetadata.ProtectPreviouslyProtectedApprovalArgumentsForExport(
            PromptGalleryToolPolicy.PromptGalleryDraftUpdate, audit, policies));
    }

    [Fact]
    public async Task Contributed_operation_requirements_are_enforced_at_invocation() {
        var contribution = PromptGalleryToolPolicy.Capabilities[0] with {
            Name = "module_external_query",
            OperationRequirementKind = ToolCapabilityOperationRequirementKind.Static,
            OperationRequirements = [ToolCapabilityProcessOperationRequirement.Any(ProcessOperationContractNames.ExecuteExternalAction)]
        };
        var context = new ToolInvocationPolicyContext(Guid.NewGuid(), "Module tool operator", contribution.Name,
            new Dictionary<string, string>(), ToolInvocationClassification.Read, IsKnownTool: true,
            AutoApprovalAllowed: false, ApprovalWrapperAvailable: false, "run-1", "process-step", "process-1", "step-1",
            ProcessStepAllowedOperations: []) {
            DeclaredCapability = contribution,
            PathArguments = ToolInvocationPathArgumentSet.Empty
        };
        var denied = await new DefaultAgentToolInvocationPolicy().EvaluateAsync(context, CancellationToken.None);
        Assert.Equal(ToolInvocationDecisionKind.Deny, denied.Kind);
        var allowed = await new DefaultAgentToolInvocationPolicy().EvaluateAsync(context with {
            ProcessStepAllowedOperations = [ProcessOperationContractNames.ExecuteExternalAction]
        }, CancellationToken.None);
        Assert.Equal(ToolInvocationDecisionKind.Allow, allowed.Kind);
    }

    [Fact]
    public void Runtime_and_UI_display_keep_the_existing_safe_arguments() {
        var policies = new AgentToolPolicyCatalog(PromptGalleryToolPolicy.Capabilities);
        const string arguments = "{\"request\":{\"promptArtifactId\":\"item-42\",\"content\":\"private-body\",\"includeArchived\":true}}";
        var display = AgentToolArgumentDisplayFormatter.DescribeArguments(arguments, PromptGalleryToolPolicy.PromptGalleryCatalogSearch, policies);
        Assert.Contains("item-42", display, StringComparison.Ordinal);
        Assert.Contains("includeArchived", display, StringComparison.Ordinal);
        Assert.DoesNotContain("private-body", display, StringComparison.Ordinal);
        Assert.Equal(display, MafToolInvocationArgumentFormatter.DescribeArguments(arguments, PromptGalleryToolPolicy.PromptGalleryCatalogSearch, policies));
    }
}
