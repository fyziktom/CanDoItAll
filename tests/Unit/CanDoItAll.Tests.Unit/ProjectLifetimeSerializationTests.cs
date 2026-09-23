using System.Text.Json;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectLifetimeSerializationTests {
    [Fact]
    public void Package_project_json_keeps_saved_identity_and_business_fields_while_restoration_gets_a_new_lifetime() {
        var legacy = """
            {"Id":"8c8ff78c-9c34-4c5c-85b6-e609baec6f33","Name":"Saved project","Slug":"saved-project","Description":"Preserved description","Objective":"Preserved objective","Status":2,"CurrentPhase":"Review","TargetDateUtc":"2026-03-04T00:00:00Z","CreatedAtUtc":"2026-01-02T03:04:05+00:00","UpdatedAtUtc":"2026-02-03T04:05:06+00:00"}
            """;
        var first = Assert.IsType<Project>(JsonSerializer.Deserialize<Project>(legacy));
        var serialized = JsonSerializer.Serialize(first);
        var restored = Assert.IsType<Project>(JsonSerializer.Deserialize<Project>(serialized));
        Assert.Equal(first.Id, restored.Id);
        Assert.NotEqual(Guid.Empty, first.LifetimeId);
        Assert.NotEqual(Guid.Empty, restored.LifetimeId);
        Assert.NotEqual(first.LifetimeId, restored.LifetimeId);
        Assert.False(first.LegacyAgentAccessBindingEligible);
        Assert.False(restored.LegacyAgentAccessBindingEligible);
        Assert.Equal(serialized, JsonSerializer.Serialize(restored));
        using var expected = JsonDocument.Parse(legacy);
        using var actual = JsonDocument.Parse(serialized);
        Assert.Equal(expected.RootElement.EnumerateObject().Select(property => property.Name).Order(),
            actual.RootElement.EnumerateObject().Select(property => property.Name).Order());
        foreach (var property in expected.RootElement.EnumerateObject()) {
            var restoredProperty = actual.RootElement.GetProperty(property.Name);
            Assert.Equal(property.Value.ValueKind, restoredProperty.ValueKind);
            Assert.Equal(property.Value.ToString(), restoredProperty.ToString());
        }
    }
}
