using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class WorkspaceToolResultDisclosureTests {
    [Theory]
    [InlineData("sources/**", null, "sources", "**/*", false)]
    [InlineData("sources/**", "*", "sources", "**/*", true)]
    [InlineData("sources/**/*.txt", null, "sources", "**/*.txt", false)]
    [InlineData("sources**", " ", "sources", "**/*", true)]
    [InlineData("**/*.txt", null, ".", "**/*.txt", true)]
    [InlineData("", null, ".", "*", false)]
    public async Task Registered_listing_uses_the_original_owner_normalized_root_for_shorthand_and_default_arguments(
        string requested, string? pattern, string root, string normalizedPattern, bool jsonArguments) {
        await using var fixture = await CollectionFixture.CreateAsync();
        var original = fixture.Write(Path.Combine("sources", "nested", NativeCollectionName), "original selected");
        fixture.Write(Path.Combine("sources", "nested", "literal", "name.txt"), "decoy");
        var arguments = new AIFunctionArguments { ["relativePath"] = requested };
        if (pattern is not null) {
            arguments["searchPattern"] = pattern;
        }
        if (jsonArguments) {
            foreach (var key in arguments.Keys.ToArray()) {
                arguments[key] = JsonSerializer.SerializeToElement(arguments[key], AIJsonUtilities.DefaultOptions);
            }
        }
        var before = JsonSerializer.Serialize(arguments, AIJsonUtilities.DefaultOptions);
        var run = await fixture.InvokeAsync(ToolContractCatalog.WorkspaceListFiles, arguments);
        Assert.Equal(before, JsonSerializer.Serialize(arguments, AIJsonUtilities.DefaultOptions));
        Assert.True(run.Result.GetProperty("succeeded").GetBoolean());
        Assert.Equal(root, run.Result.GetProperty("rootPath").GetString());
        Assert.Equal(normalizedPattern, run.Result.GetProperty("searchPattern").GetString());
        var saved = WorkspaceToolResultEvidence.Read(run.Evidence);
        Assert.Equal(WorkspaceToolResultEvidenceState.Complete, saved.State);
        Assert.Equal(root, saved.Selection!.Root.RequestPath);
        Assert.Equal(Path.GetFullPath(Path.Combine(fixture.Root, root)), saved.Selection.Root.FullPath);
        Assert.Contains(original, saved.Selection.FullPaths);
        fixture.Write(Path.Combine("sources", "added-after-result.txt"), "not selected");
        await using (var current = await run.AuthorizeAsync()) {
            Assert.NotNull(current);
        }
        Assert.DoesNotContain("added-after-result.txt", run.Result.GetRawText(), StringComparison.Ordinal);
        File.Delete(original);
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => run.AuthorizeAsync().AsTask());
        Assert.Equal("workspace.result-disclosure-denied", denied.Code);
    }

    [Theory]
    [InlineData("sources", true)]
    [InlineData("sources/**", false)]
    public async Task An_explicit_nondefault_listing_pattern_preserves_the_original_path_interpretation(string path, bool succeeds) {
        await using var fixture = await CollectionFixture.CreateAsync();
        fixture.Write(Path.Combine("sources", "original.txt"), "original");
        var run = await fixture.InvokeAsync(ToolContractCatalog.WorkspaceListFiles,
            new AIFunctionArguments { ["relativePath"] = path, ["searchPattern"] = "*.txt" });
        Assert.Equal(succeeds, run.Result.GetProperty("succeeded").GetBoolean());
        Assert.Equal("*.txt", run.Result.GetProperty("searchPattern").GetString());
        var evidence = WorkspaceToolResultEvidence.Read(run.Evidence);
        if (succeeds) {
            Assert.Single(run.Result.GetProperty("entries").EnumerateArray());
            Assert.Equal(path, evidence.Selection!.Root.RequestPath);
            await using var current = await run.AuthorizeAsync();
            Assert.NotNull(current);
        } else {
            Assert.Empty(run.Result.GetProperty("entries").EnumerateArray());
            Assert.DoesNotContain(evidence.Paths, item => item.RequestPath == "sources");
            if (evidence.State == WorkspaceToolResultEvidenceState.Complete) {
                Assert.False(evidence.Selection!.RequiresExistingRoot);
                Assert.Equal(path, evidence.Selection.Root.RequestPath);
            } else {
                var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => run.AuthorizeAsync().AsTask());
                Assert.Equal("workspace.result-authority-unavailable", denied.Code);
            }
        }
    }
}
