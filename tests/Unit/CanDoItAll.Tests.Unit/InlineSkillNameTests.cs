using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using ModelCapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;

namespace CanDoItAll.Tests.Unit;

public sealed class InlineSkillNameTests
{
    [Theory]
    [InlineData("garden-planning-knowledge")]
    [InlineData("skill1")]
    [InlineData("1-skill")]
    public void SkillNameTryCreateAcceptsProviderCompatibleNames(string value)
    {
        Assert.True(SkillName.TryCreate(value, out var name));
        Assert.Equal(value, name.Value);
    }

    [Theory]
    [InlineData("Garden Planning Knowledge")]
    [InlineData("garden--planning")]
    [InlineData("-garden")]
    [InlineData("garden-")]
    [InlineData("garden_planning")]
    [InlineData("gard\u00e9n")]
    public void SkillNameTryCreateRejectsProviderIncompatibleNames(string value)
    {
        Assert.False(SkillName.TryCreate(value, out _));
    }

    [Fact]
    public void SkillNameNormalizeConvertsDisplayTextToProviderCompatibleName()
    {
        var name = SkillName.Normalize(" Garden  Planning Knowledge ");

        Assert.Equal("garden-planning-knowledge", name.Value);
    }

    [Fact]
    public void CatalogNormalizerRepairsPersistedInlineSkillName()
    {
        var normalized = InlineSkillConfigurationNormalizer.Normalize(
            ModelCapabilityKind.Skill,
            "garden-planning-knowledge",
            """
            {
              "skillSource": "inline",
              "inlineSkill": {
                "name": "Garden Planning Knowledge",
                "instructions": "Plan a garden."
              }
            }
            """);

        using var document = JsonDocument.Parse(normalized);
        Assert.Equal(
            "garden-planning-knowledge",
            document.RootElement.GetProperty("inlineSkill").GetProperty("name").GetString());
    }
}
