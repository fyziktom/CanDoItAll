using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowComponentConcurrencyTests {
    [Fact]
    public async Task An_existing_component_requires_its_original_before_replacing_instructions() {
        var catalog = new InMemoryWorkflowCatalogService(new WorkflowDefinitionValidator());
        var request = CreateRequest();
        var original = await catalog.SaveComponentAsync(request);
        await Assert.ThrowsAsync<WorkflowComponentConcurrencyException>(() => catalog.SaveComponentAsync(request with {
            Id = original.Id,
            Instructions = "Unobserved older replacement instructions."
        }));
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(await catalog.GetComponentAsync(original.Id)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Empty_and_stale_originals_cannot_replace_a_component(bool stale) {
        var catalog = new InMemoryWorkflowCatalogService(new WorkflowDefinitionValidator());
        var request = CreateRequest();
        var original = await catalog.SaveComponentAsync(request);
        var winner = await catalog.SaveComponentAsync(request with { Id = original.Id, ExpectedRevision = original.Revision, Name = "Winner" });
        await Assert.ThrowsAsync<WorkflowComponentConcurrencyException>(() => catalog.SaveComponentAsync(request with {
            Id = original.Id, ExpectedRevision = stale ? original.Revision : Guid.Empty
        }));
        AssertSame(winner, await catalog.GetComponentAsync(original.Id));
    }

    [Fact]
    public async Task A_chosen_identity_is_create_only_and_an_original_cannot_create_a_missing_row() {
        var catalog = new InMemoryWorkflowCatalogService(new WorkflowDefinitionValidator());
        var request = CreateRequest() with { Id = WorkflowComponentId.New() };
        await Assert.ThrowsAsync<WorkflowComponentConcurrencyException>(() => catalog.SaveComponentAsync(request with { ExpectedRevision = Guid.NewGuid() }));
        Assert.Null(await catalog.GetComponentAsync(request.Id!.Value));
        var accepted = await catalog.SaveComponentAsync(request);
        await Assert.ThrowsAsync<WorkflowComponentConcurrencyException>(() => catalog.SaveComponentAsync(request));
        AssertSame(accepted, await catalog.GetComponentAsync(accepted.Id));
    }

    [Fact]
    public async Task A_noop_has_a_new_revision_and_returns_the_exact_readable_acknowledgement() {
        var catalog = new InMemoryWorkflowCatalogService(new WorkflowDefinitionValidator());
        var request = CreateRequest();
        var original = await catalog.SaveComponentAsync(request);
        var accepted = await catalog.SaveComponentAsync(request with { Id = original.Id, ExpectedRevision = original.Revision });
        Assert.NotEqual(Guid.Empty, accepted.Revision);
        Assert.NotEqual(original.Revision, accepted.Revision);
        Assert.NotEqual(Guid.Empty, accepted.LifetimeId);
        Assert.Equal(original.LifetimeId, accepted.LifetimeId);
        AssertSame(accepted, await catalog.GetComponentAsync(accepted.Id));
        AssertSame(accepted, Assert.Single(await catalog.ListComponentsAsync()));
    }

    [Fact]
    public async Task A_deleted_lifetime_cannot_delete_or_replace_a_recreated_component() {
        var catalog = new InMemoryWorkflowCatalogService(new WorkflowDefinitionValidator());
        var request = CreateRequest();
        var original = await catalog.SaveComponentAsync(request);
        await catalog.DeleteComponentAsync(new(original.Id, original.Revision));
        var recreated = await catalog.SaveComponentAsync(request with { Id = original.Id, Instructions = "Recreated instructions" });
        Assert.NotEqual(original.LifetimeId, recreated.LifetimeId);
        await Assert.ThrowsAsync<WorkflowComponentConcurrencyException>(() => catalog.DeleteComponentAsync(new(original.Id, original.Revision)));
        await Assert.ThrowsAsync<WorkflowComponentConcurrencyException>(() => catalog.SaveComponentAsync(request with { Id = original.Id, ExpectedRevision = original.Revision }));
        AssertSame(recreated, await catalog.GetComponentAsync(original.Id));
    }

    [Fact]
    public async Task Empty_or_missing_delete_originals_cannot_remove_a_component() {
        var catalog = new InMemoryWorkflowCatalogService(new WorkflowDefinitionValidator());
        var original = await catalog.SaveComponentAsync(CreateRequest());
        await Assert.ThrowsAsync<WorkflowComponentConcurrencyException>(() => catalog.DeleteComponentAsync(new(original.Id, Guid.Empty)));
        await Assert.ThrowsAsync<WorkflowComponentConcurrencyException>(() => catalog.DeleteComponentAsync(new(WorkflowComponentId.New(), original.Revision)));
        AssertSame(original, await catalog.GetComponentAsync(original.Id));
    }

    private static void AssertSame<T>(T expected, T actual) => Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));

    [Fact]
    public async Task Submitted_and_returned_secret_collections_do_not_alias_the_store() {
        var catalog = new InMemoryWorkflowCatalogService(new WorkflowDefinitionValidator());
        var secret = new AgentAllowedSecretReference(Guid.NewGuid(), "Synthetic secret", "Workflow proof");
        var submitted = new List<AgentAllowedSecretReference> { secret };
        var original = await catalog.SaveComponentAsync(CreateRequest() with { Permissions = AgentPermissionsPolicy.Default with { AllowedSecrets = submitted } });
        submitted.Clear();
        var storedSecrets = (await catalog.GetComponentAsync(original.Id))?.Permissions.AllowedSecrets;
        Assert.NotNull(storedSecrets);
        Assert.Equal(secret, Assert.Single(storedSecrets));
        Assert.IsType<List<AgentAllowedSecretReference>>(original.Permissions.AllowedSecrets).Clear();
        var listed = Assert.Single(await catalog.ListComponentsAsync());
        var listedSecrets = listed.Permissions.AllowedSecrets;
        Assert.NotNull(listedSecrets);
        Assert.Equal(secret, Assert.Single(listedSecrets));
        Assert.IsType<List<AgentAllowedSecretReference>>(listed.Permissions.AllowedSecrets).Clear();
        storedSecrets = (await catalog.GetComponentAsync(original.Id))?.Permissions.AllowedSecrets;
        Assert.NotNull(storedSecrets);
        Assert.Equal(secret, Assert.Single(storedSecrets));
    }

    private static LlmCallComponentSaveRequest CreateRequest() => new(null, "Concurrent reusable step", null,
        "gpt-6-luna", WorkflowModality.Text, new(0.2, 800, false, string.Empty),
        "Return the workflow payload unchanged.", WorkflowValueShape.Text, WorkflowValueShape.Text, AgentPermissionsPolicy.Default);
}
