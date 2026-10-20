using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Held_source_uses_exact_launcher_when_an_unprivileged_executor_is_primary(bool executorFirst) {
        await using var application = await TestApplication.CreateAsync(Harness());
        await using var scope = application.Services.CreateAsyncScope();
        var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var executor = await AddSelectedExecutorAsync(fixture, permitted: false);
        var selected = executorFirst ? new[] { executor.Id, fixture.Agent.Id } : [fixture.Agent.Id, executor.Id];
        await using var held = await fixture.Writer.AcquireAgentsReadLeaseAsync(selected);
        Assert.Equal(selected[0], held.Agent!.Id);
        Assert.Equal(2, held.Agents.Length);
        Assert.Equal(fixture.Agent.Id, ProjectProcessLaunchAuthorityService.FindHeldAgent(held, fixture.Agent.Id)!.Id);
        fixture.Authority.RequireHeldSource(fixture.SavedAuthority, held, mutation: false);
        fixture.Authority.RequireHeldSource(fixture.SavedAuthority, held, mutation: true);
        Assert.False(ProjectProcessLaunchAuthorityService.FindHeldAgent(held, executor.Id)!.Permissions.CanUseTools);
    }

    [Theory]
    [InlineData(Revocation.Tools)]
    [InlineData(Revocation.Read)]
    [InlineData(Revocation.Write)]
    [InlineData(Revocation.ExactLifetime)]
    public async Task Held_source_revocation_cannot_borrow_a_different_primary_executors_grants(Revocation revocation) {
        await using var application = await TestApplication.CreateAsync(Harness());
        await using var scope = application.Services.CreateAsyncScope();
        var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var executor = await AddSelectedExecutorAsync(fixture, permitted: true);
        await fixture.RevokeAsync(revocation);
        await using var held = await fixture.Writer.AcquireAgentsReadLeaseAsync([executor.Id, fixture.Agent.Id]);
        Assert.Equal(executor.Id, held.Agent!.Id);
        Assert.True(held.Agent.Permissions.CanUseTools);
        Assert.True(AgentProjectStructureAccessMetadata.Read(held.Agent.ConfigurationJson).CanWrite);
        Assert.Throws<ProcessLaunchAuthorityRejectedException>(() =>
            fixture.Authority.RequireHeldSource(fixture.SavedAuthority, held, mutation: true));
        if (revocation == Revocation.Write) {
            fixture.Authority.RequireHeldSource(fixture.SavedAuthority, held, mutation: false);
        } else {
            Assert.Throws<ProcessLaunchAuthorityRejectedException>(() =>
                fixture.Authority.RequireHeldSource(fixture.SavedAuthority, held, mutation: false));
        }
    }

    [Fact]
    public async Task Held_source_missing_from_selected_catalog_is_not_replaced_by_executor() {
        await using var application = await TestApplication.CreateAsync(Harness());
        await using var scope = application.Services.CreateAsyncScope();
        var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var executor = await AddSelectedExecutorAsync(fixture, permitted: true);
        await fixture.Writer.UpdateCatalogAsync(catalog => catalog with {
            Agents = catalog.Agents.Where(agent => agent.Id != fixture.Agent.Id).ToArray()
        });
        await using var held = await fixture.Writer.AcquireAgentsReadLeaseAsync([executor.Id, fixture.Agent.Id]);
        Assert.Equal(executor.Id, Assert.Single(held.Agents).Id);
        Assert.Null(ProjectProcessLaunchAuthorityService.FindHeldAgent(held, fixture.Agent.Id));
        Assert.Throws<ProcessLaunchAuthorityRejectedException>(() =>
            fixture.Authority.RequireHeldSource(fixture.SavedAuthority, held, mutation: false));
        Assert.Throws<ProcessLaunchAuthorityRejectedException>(() =>
            fixture.Authority.RequireHeldSource(fixture.SavedAuthority, held, mutation: true));
    }

    private static async Task<AgentDefinition> AddSelectedExecutorAsync(Fixture fixture, bool permitted) {
        var executor = fixture.Agent with {
            Id = Guid.NewGuid(), Name = "Distinct selected executor", TemplateKey = string.Empty, Tags = [],
            Permissions = fixture.Agent.Permissions with { CanUseTools = permitted },
            ConfigurationJson = permitted ? fixture.Agent.ConfigurationJson : AgentProjectStructureAccessMetadata.Write("{}", new())
        };
        await fixture.Writer.UpdateCatalogAsync(catalog => catalog with { Agents = [.. catalog.Agents, executor] });
        return executor;
    }
}
