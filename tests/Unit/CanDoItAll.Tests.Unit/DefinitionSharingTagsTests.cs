using CanDoItAll.RemoteInstances;

namespace CanDoItAll.Tests.Unit;

public sealed class DefinitionSharingTagsTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true, "shared")]
    [InlineData(true, false, "api")]
    [InlineData(true, true, "shared", "api")]
    public void Current_settings_replace_reserved_tags_and_preserve_custom_tags(bool api, bool shared, params string[] expected) {
        string[] original = ["Team", " Shared ", "API", "api-client", "shared", "shared-services"];
        string[] custom = ["Team", "api-client", "shared-services"];

        var projected = DefinitionSharingTags.Project(original, api, shared);

        Assert.Equal(custom, DefinitionSharingTags.Custom(original));
        Assert.Equal(expected.Concat(custom), projected);
        Assert.Equal(projected, DefinitionSharingTags.Project(projected, api, shared));
        Assert.Equal(custom, DefinitionSharingTags.Project(projected, apiAvailable: false, sharedTemplate: false));
    }
}
