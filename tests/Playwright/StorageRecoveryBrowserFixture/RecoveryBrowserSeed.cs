using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Playwright.StorageRecovery;

internal static partial class RecoveryBrowserSeed {
    private static readonly byte[] Content = "{\"original\":true}"u8.ToArray();
    internal sealed record Prepared(RecoveryBrowserCase Description, WorkflowStructureOutputPlan Plan,
        StorageStablePlacementReceipt Storage, string PreparedCommand, string Run);

    public static async Task<Prepared> CreateAsync(IServiceProvider provider, bool nativeCommitted) {
        await using var fixture = await CreateFixtureAsync(provider, prepareOutput: false);
        var request = fixture.Request with { ObjectType = ProjectObjectType.File, ObjectSubtype = "json",
            Media = new("proof.json", "application/json", Convert.ToBase64String(Content)) };
        var plan = fixture.Plan with { Kind = WorkflowStructureOutputKind.Asset };
        plan = plan with { Fingerprint = ProjectWorkflowContributionFingerprint.Create(plan, request) };
        request = BindRequest(request, plan, fixture.Request.WorkflowMutationAdmission!.Authority);
        var output = await OutputStore(fixture.Services).PrepareAsync(plan);
        var fault = new CommitFault(nativeCommitted);
        var failing = Owner(fixture.Services, Factory(fixture.Services, new ObserveReceiptCommands(fault), fault));
        Assert.Same(fault.Failure, await Assert.ThrowsAsync<ArgumentException>(() => failing.CreateWorkflowContributionAsync(plan, request,
            storagePlacementIntentId: output.StoragePlacementIntentId)));
        Assert.True(fault.SawReceiptInsert);
        var stored = await fixture.Services.GetRequiredService<StorageStablePlacementService>().FindAsync(new(output.StoragePlacementIntentId!.Value));
        Assert.Equal(StorageStablePlacementState.Completed, stored!.State);
        var receipt = Assert.IsType<StorageStablePlacementReceipt>(stored.Receipt);
        Assert.Equal(nativeCommitted, await fixture.Owner.FindWorkflowContributionAsync(plan.Identity) is not null);
        Assert.Null((await OutputStore(fixture.Services).FindAsync(plan.Identity))!.Receipt);
        var run = await fixture.Services.GetRequiredService<IWorkflowRunStore>().GetRunAsync(plan.Identity.Occurrence.RunId);
        return new(new(plan.ProjectId, receipt.IntentId.Value, nativeCommitted, plan.Identity.Occurrence.RunId.Value,
            Convert.ToHexString(SHA256.HashData(Content))), plan, receipt, await RetainedCommandAsync(fixture.Services, plan), JsonSerializer.Serialize(run));
    }

    public static async Task<RecoveryBrowserProof> VerifyAsync(IServiceProvider services, Prepared seed, int driverResolutions) {
        Assert.Equal(0, driverResolutions);
        var native = await services.GetRequiredService<ProjectWorkbenchService>().FindWorkflowContributionAsync(seed.Plan.Identity);
        Assert.NotNull(native);
        Assert.Equal(seed.Storage.IntentId.Value, native.Receipt!.AssetId);
        Assert.Equal(seed.Plan.ProjectLifetime, native.Receipt.ProjectLifetime);
        Assert.Equal(native.Receipt, (await OutputStore(services).FindAsync(seed.Plan.Identity))!.Receipt);
        var storage = await services.GetRequiredService<StorageStablePlacementService>().FindAsync(seed.Storage.IntentId);
        Assert.Equal(seed.Storage, storage!.Receipt);
        var retained = await RetainedCommandAsync(services, seed.Plan);
        Assert.Equal(seed.PreparedCommand, retained);
        var run = await services.GetRequiredService<IWorkflowRunStore>().GetRunAsync(seed.Plan.Identity.Occurrence.RunId);
        Assert.Equal(seed.Run, JsonSerializer.Serialize(run));
        var content = await services.GetRequiredService<ProjectStructureAgentService>().GetAssetContentAsync(seed.Plan.ProjectId, native.Node.Id);
        Assert.Equal(Content, Convert.FromBase64String(content.Base64Data));
        var executions = await services.GetRequiredService<IAgentFrameworkWorkspaceService>().ListExecutionRunsAsync(new());
        Assert.Empty(executions);
        await using var history = await services.GetRequiredService<IDbContextFactory<ProviderHistoryDbContext>>().CreateDbContextAsync();
        var requests = await history.Set<HistoryEntryRow>().CountAsync();
        Assert.Equal(0, requests);
        await using var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        Assert.Single(await database.Set<ProjectObjectRecord>().Where(row => row.ProjectId == seed.Plan.ProjectId && row.NodeKey == native.Node.Id).ToArrayAsync());
        return new(seed.Description.IntentId, native.Node.Id, Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(content.Base64Data))),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(retained))), true, true, driverResolutions, executions.Count, requests);
    }

    private static async Task<string> RetainedCommandAsync(IServiceProvider services, WorkflowStructureOutputPlan plan) {
        await using var context = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        var row = await context.Set<ProjectWorkflowContributionRecord>().SingleAsync(item => item.RunId == plan.Identity.Occurrence.RunId.Value);
        return JsonSerializer.Serialize(new { row.PlanJson, row.RequestJson, row.StoragePlacementIntentId, row.PreparedAtUtc,
            row.DatabaseProfileId, row.ProjectId, row.ProjectLifetimeId });
    }
}

internal sealed class RecoveryDriverProbe(IEnumerable<IStorageDriver> drivers) : IStorageDriverRegistry {
    private readonly StorageDriverRegistry inner = new(drivers);
    private int attempts;
    public bool Armed { get; set; }
    public int Attempts => Volatile.Read(ref attempts);
    public IReadOnlyCollection<StorageProviderKind> RegisteredKinds => inner.RegisteredKinds;
    public bool TryResolve(StorageProviderKind providerKind, out IStorageDriver driver) {
        RequireNoDispatch();
        return inner.TryResolve(providerKind, out driver);
    }
    public IStorageDriver Resolve(StorageProviderKind providerKind) {
        RequireNoDispatch();
        return inner.Resolve(providerKind);
    }
    private void RequireNoDispatch() {
        if (Armed) {
            Interlocked.Increment(ref attempts);
            throw new InvalidOperationException("Recovery must reuse the prepared bytes without resolving another driver.");
        }
    }
}
