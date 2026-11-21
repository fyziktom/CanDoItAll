using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class RemoteTemplateProviderTests {
    [Fact]
    public void Exact_published_model_wins_over_matching_names_and_local_defaults() {
        var first = Option("same", true);
        var second = Option("same", true);
        var local = Option("same", false);
        var requirement = new RemoteTemplateModelRequirement("Writer", ProviderProfilePurpose.Chat, ModelName: "same", PublishedModel: second.Choice.Model);
        Assert.Equal(second.Choice, RemoteTemplateProviderCatalog.Preferred(requirement, [first, local, second]));
        Assert.Null(RemoteTemplateProviderCatalog.Preferred(requirement, [first, local]));
        Assert.Null(RemoteTemplateProviderCatalog.Preferred(requirement with { PublishedModel = null }, [first, second]));
    }

    [Fact]
    public void Missing_source_binding_requires_review_and_bulk_choices_must_preserve_capabilities() {
        var local = Option("local", false);
        var source = Option("default", true);
        var requirement = new RemoteTemplateModelRequirement("Conversation", ProviderProfilePurpose.Chat);
        Assert.Null(RemoteTemplateProviderCatalog.Preferred(requirement, [local]));
        Assert.Equal(source.Choice, RemoteTemplateProviderCatalog.Preferred(requirement, [local, source]));
        Assert.True(local.Supports(requirement));
        Assert.False(local.Supports(requirement with { Vision = true }));
        Assert.False(local.Supports(requirement with { StructuredOutput = true }));
        Assert.False(local.Supports(requirement with { Tools = true }));
        Assert.False(local.Supports(requirement with { Purpose = ProviderProfilePurpose.ImageGeneration }));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("//example.org/private")]
    [InlineData("data:image/svg+xml;base64,PHN2Zz4=")]
    [InlineData("data:image/jpeg;base64,not-valid")]
    [InlineData("data:image/jpeg;base64,iVBORw0KGgo=")]
    [InlineData("https://user:secret@sample.example/identity.png")]
    public void Avatar_preview_ignores_unsupported_or_credential_bearing_references(string image) =>
        Assert.Null(RemoteTemplateAvatar.ForDisplay(image));

    [Fact]
    public void Avatar_preview_accepts_bundled_external_and_bounded_embedded_images() {
        var bundled = AgentAvatarImageCatalog.BundledRobotAvatarUrls[0];
        Assert.Equal(bundled, RemoteTemplateAvatar.ForDisplay(bundled));
        const string external = "https://assets.example.org/profiles/editor.png";
        Assert.Equal(external, RemoteTemplateAvatar.ForDisplay(external));
        const string embedded = "data:image/png;base64,iVBORw0KGgo=";
        Assert.Equal(embedded, RemoteTemplateAvatar.ForDisplay(embedded));
        Assert.Null(RemoteTemplateAvatar.ForDisplay("data:image/png;base64," +
            Convert.ToBase64String(new byte[AgentAvatarImagePolicy.MaxAvatarBytes + 1])));
    }

    private static RemoteTemplateModelOption Option(string name, bool source) {
        var id = Guid.NewGuid();
        var model = source ? SharedProviderRoutingModelIdCodec.Create(new(id), name).Value : name;
        return new(new(id, model), "Provider", name, ProviderProfilePurpose.Chat, source, true, false, false, false);
    }
}
