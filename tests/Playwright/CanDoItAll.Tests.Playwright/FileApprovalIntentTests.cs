using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Playwright;

public sealed class FileApprovalIntentTests {
    private static readonly Guid ProjectId = Guid.Parse("cc110000-0000-0000-0000-000000000001");
    private static readonly FileApprovalIntent Intent = new(ProjectId, "parent", "Fixture file", "fixture-content", "proof.md", "text/markdown", "proof/proof.md");

    [Theory]
    [InlineData("proof/another.md", "fixture-content", false)]
    [InlineData("proof/proof.md", "changed-content", false)]
    [InlineData("proof/proof.md", "fixture-content", true)]
    [InlineData("external:fixture/proof.md", "fixture-content", false)]
    public void Changed_workspace_proposal_never_matches_immutable_intent(string path, string content, bool overwrite) {
        Assert.NotEqual(FileProposalRefusal.None, Intent.Validate(Write(path, content, overwrite), new HashSet<string>()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Changed_project_or_parent_is_refused(bool changeProject) {
        var payload = Asset(changeProject ? Guid.NewGuid() : ProjectId, changeProject ? "parent" : "sibling");
        Assert.Equal(FileProposalRefusal.WrongTarget, Intent.Validate(payload, new HashSet<string>()));
    }

    [Fact]
    public void Exact_write_and_attachment_pass_once_and_second_attachment_is_refused() {
        Assert.Equal(FileProposalRefusal.None, Intent.Validate(Write(Intent.WorkspacePath!, Intent.Content, false), new HashSet<string>()));
        var payload = Asset(ProjectId, "parent");
        Assert.Equal(FileProposalRefusal.None, Intent.Validate(payload, new HashSet<string>()));
        Assert.Equal(FileProposalRefusal.DuplicateEffect, Intent.Validate(payload, new HashSet<string> { ProjectStructureToolPolicy.ProjectStructureAssetCreate }));
    }

    [Theory]
    [InlineData("{\"path\":\"proof/proof.md\",\"content\":\"fixture-content\"}")]
    [InlineData("{\"path\":\"proof/proof.md\",\"content\":\"fixture-content\",\"overwrite\":false,\"extra\":true}")]
    [InlineData("{\"path\":\"proof/proof.md\",\"content\":\"fixture-content\",\"overwrite\":true,\"overwrite\":false}")]
    public void Ambiguous_or_changed_schema_is_refused(string json) {
        Assert.Equal(FileProposalRefusal.InvalidSchema, Intent.Validate(Payload(ToolContractCatalog.WorkspaceWriteFile, json), new HashSet<string>()));
    }

    private static AgentToolPreparedPayload Asset(Guid project, string parent) {
        var request = new ProjectProcessAssetCreateProposal(project, new(ProjectObjectType.File, Intent.Title, ParentNodeKey: parent,
            SourceWorkspacePath: Intent.WorkspacePath, SourceFileName: Intent.FileName, SourceContentType: Intent.ContentType));
        return Payload(ProjectStructureToolPolicy.ProjectStructureAssetCreate, JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    private static AgentToolPreparedPayload Write(string path, string content, bool overwrite)
        => Payload(ToolContractCatalog.WorkspaceWriteFile, JsonSerializer.Serialize(new { path, content, overwrite }));

    private static AgentToolPreparedPayload Payload(string tool, string json)
        => new(tool, 1, AgentToolProtocolEnvelope.ComputeDigest(json), json, AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.OwnerReceipt);
}
