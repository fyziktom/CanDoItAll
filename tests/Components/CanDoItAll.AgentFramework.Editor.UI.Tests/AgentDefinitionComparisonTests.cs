using Bunit;
using CanDoItAll.AgentFramework.Editor.UI;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Memory.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentEditorUi;

public sealed class AgentDefinitionComparisonTests {
    [Fact]
    public void Runtime_and_permission_groups_require_an_explicit_choice_without_privilege_union() {
        var original = Original();
        original.ProviderProfileId = Guid.NewGuid();
        original.Model = "original-model";
        original.Tags = ["removed", "retained"];
        var local = AgentEditorDraftSnapshot.Copy(original);
        local.Model = "local-model";
        local.Permissions = local.Permissions with { CanUseTools = true };
        local.Tags = ["retained", "local"];
        var current = AgentEditorDraftSnapshot.Copy(original);
        current.ProviderProfileId = Guid.NewGuid();
        current.Model = "peer-model";
        current.Permissions = current.Permissions with { CanScheduleWork = true };
        current.Tags.Add("peer");
        var review = new AgentDefinitionComparison(original, local, current);
        Assert.False(review.Runtime.IsResolved);
        Assert.False(review.Permissions.IsResolved);
        Assert.Throws<InvalidOperationException>(() => review.Result());
        review.Runtime.UseCurrent();
        review.Permissions.UseCurrent();
        var result = review.Result();
        Assert.Equal(current.ProviderProfileId, result.ProviderProfileId);
        Assert.Equal("peer-model", result.Model);
        Assert.True(result.Permissions.CanScheduleWork);
        Assert.False(result.Permissions.CanUseTools);
        Assert.Equal(["local", "peer", "retained"], result.Tags.Order(StringComparer.Ordinal).ToArray());
        result.WorkspaceToolAccess.CanWriteFiles = true;
        Assert.False(current.WorkspaceToolAccess.CanWriteFiles);
    }

    [Fact]
    public void Comparison_displays_binding_decisions_but_keeps_opaque_configuration_private() {
        var original = Original();
        var local = AgentEditorDraftSnapshot.Copy(original);
        local.ConfigurationJson = "{\"private_extension\":\"local-private-value\"}";
        var current = AgentEditorDraftSnapshot.Copy(original);
        current.ConfigurationJson = "{\"private_extension\":\"peer-private-value\"}";
        var providerId = new MemoryProviderInstanceId(Guid.NewGuid().ToString("N"));
        current.MemoryAccess.ProviderBindings = [new(AgentMemoryProviderAlias.Parse("project-notes"), providerId, false, AgentMemoryProviderRequirement.Required)];
        current.MemoryAccess.ProviderAssignments = [new(MemoryProviderAssignmentScope.AgentRole, "Research", providerId)];
        var review = new AgentDefinitionComparison(original, local, current);
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        var cut = context.Render<AgentDefinitionComparisonSurface>(parameters => parameters.Add(component => component.Review, review));
        Assert.DoesNotContain("local-private-value", cut.Markup);
        Assert.DoesNotContain("peer-private-value", cut.Markup);
        Assert.Contains("project-notes", cut.Markup);
        Assert.Contains("Required", cut.Markup);
        Assert.Contains("Research", cut.Markup);
        Assert.False(review.Memory.IsResolved);
        review.Memory.UseCurrent();
        review.PreservedConfiguration.UseLocal();
        var result = review.Result();
        Assert.Equal(local.ConfigurationJson, result.ConfigurationJson);
        Assert.Equal(providerId, Assert.Single(result.MemoryAccess.ProviderBindings).ProviderInstanceId);
    }

    [Fact]
    public void Large_instructions_require_review_and_are_preserved_without_automatic_truncation() {
        var original = Original();
        original.Instructions = new('a', 600_000);
        var local = AgentEditorDraftSnapshot.Copy(original);
        local.Instructions += " local 東京";
        var current = AgentEditorDraftSnapshot.Copy(original);
        current.Instructions += " peer";
        var review = new AgentDefinitionComparison(original, local, current);
        Assert.False(review.Instructions.Field.IsResolved);
        review.Instructions.Field.UseLocal();
        Assert.Equal(local.Instructions, review.Result().Instructions);
        review.Invalidate();
        Assert.Throws<InvalidOperationException>(() => review.Result());
    }

    private static AgentEditorModel Original() => new() {
        Id = Guid.NewGuid(), ExpectedUpdatedAtUtc = DateTimeOffset.UtcNow, Name = "Comparison original",
        Permissions = AgentPermissionsPolicy.Default with { CanUseTools = false }
    };
}
