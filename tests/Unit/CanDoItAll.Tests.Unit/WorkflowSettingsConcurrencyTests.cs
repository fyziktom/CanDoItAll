using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowSettingsConcurrencyTests {
    [Fact]
    public void A_clock_rollback_still_advances_the_revision_at_database_precision() {
        var previous = DateTimeOffset.UtcNow.AddDays(1);
        previous = new(previous.Ticks - previous.Ticks % TimeSpan.TicksPerMicrosecond, TimeSpan.Zero);
        Assert.Equal(previous.AddTicks(TimeSpan.TicksPerMicrosecond), WorkflowEditRevision.Next(previous));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_peer_write_invalidates_the_original_even_when_values_are_unchanged(bool unchanged) {
        var store = new InMemoryWorkflowCatalogStore();
        var catalog = new InMemoryWorkflowCatalogService(store, new WorkflowDefinitionValidator());
        var peer = new InMemoryWorkflowCatalogService(store, new WorkflowDefinitionValidator());
        var original = await catalog.SaveSettingsAsync(WorkflowSettings.Default);
        var winner = await peer.SaveSettingsAsync(unchanged ? original : Changed(original, 800));
        Assert.NotEqual(original.UpdatedAtUtc, winner.UpdatedAtUtc);
        await Assert.ThrowsAsync<WorkflowSettingsConcurrencyException>(() => catalog.SaveSettingsAsync(Changed(original, 512)));
        Assert.Equal(JsonSerializer.Serialize(winner), JsonSerializer.Serialize(await catalog.GetSettingsAsync()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_same_original_can_commit_only_one_create_or_replacement(bool existing) {
        var store = new InMemoryWorkflowCatalogStore();
        var catalog = new InMemoryWorkflowCatalogService(store, new WorkflowDefinitionValidator());
        var peer = new InMemoryWorkflowCatalogService(store, new WorkflowDefinitionValidator());
        var original = existing ? await catalog.SaveSettingsAsync(WorkflowSettings.Default) : await catalog.GetSettingsAsync();
        var results = await Task.WhenAll(Attempt(catalog, Changed(original, 512)), Attempt(peer, Changed(original, 1024)));
        var winner = Assert.Single(results.OfType<WorkflowSettings>());
        Assert.Equal(JsonSerializer.Serialize(winner), JsonSerializer.Serialize(await catalog.GetSettingsAsync()));
    }

    [Fact]
    public async Task An_original_cannot_create_an_absent_settings_row() {
        var catalog = new InMemoryWorkflowCatalogService(new WorkflowDefinitionValidator());
        await Assert.ThrowsAsync<WorkflowSettingsConcurrencyException>(() => catalog.SaveSettingsAsync(
            WorkflowSettings.Default with { UpdatedAtUtc = DateTimeOffset.UtcNow }));
        Assert.Null((await catalog.GetSettingsAsync()).UpdatedAtUtc);
    }

    [Fact]
    public async Task The_acknowledged_revision_round_trips_and_authorizes_the_next_write() {
        var catalog = new InMemoryWorkflowCatalogService(new WorkflowDefinitionValidator());
        var accepted = await catalog.SaveSettingsAsync(WorkflowSettings.Default);
        Assert.NotNull(accepted.UpdatedAtUtc);
        var read = await catalog.GetSettingsAsync();
        Assert.Equal(JsonSerializer.Serialize(accepted), JsonSerializer.Serialize(read));
        var next = await catalog.SaveSettingsAsync(Changed(read, 512));
        Assert.True(next.UpdatedAtUtc > accepted.UpdatedAtUtc);
        Assert.Equal(0, next.UpdatedAtUtc!.Value.Ticks % TimeSpan.TicksPerMicrosecond);
    }

    [Fact]
    public async Task Mutating_a_returned_voice_object_cannot_change_the_saved_settings() {
        var catalog = new InMemoryWorkflowCatalogService(new WorkflowDefinitionValidator());
        var accepted = await catalog.SaveSettingsAsync(WorkflowSettings.Default);
        var expected = accepted.VoiceSettings!.SampleText;
        accepted.VoiceSettings.SampleText = "Changed outside the owner";
        Assert.Equal(expected, (await catalog.GetSettingsAsync()).VoiceSettings!.SampleText);
    }

    [Fact]
    public async Task Existing_settings_cannot_be_replaced_without_the_original_observation() {
        var catalog = new InMemoryWorkflowCatalogService(new WorkflowDefinitionValidator());
        var accepted = await catalog.SaveSettingsAsync(WorkflowSettings.Default);
        var stale = WorkflowSettings.Default with {
            ArtifactPolicy = WorkflowSettings.Default.ArtifactPolicy with { MaxInlinePayloadCharacters = 512 }
        };
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => catalog.SaveSettingsAsync(stale));
        Assert.Equal(JsonSerializer.Serialize(accepted), JsonSerializer.Serialize(await catalog.GetSettingsAsync()));
    }

    private static WorkflowSettings Changed(WorkflowSettings original, int limit)
        => original with { ArtifactPolicy = original.ArtifactPolicy with { MaxInlinePayloadCharacters = limit } };

    private static async Task<WorkflowSettings?> Attempt(InMemoryWorkflowCatalogService catalog, WorkflowSettings candidate) {
        try {
            return await catalog.SaveSettingsAsync(candidate);
        } catch (WorkflowSettingsConcurrencyException) {
            return null;
        }
    }
}
