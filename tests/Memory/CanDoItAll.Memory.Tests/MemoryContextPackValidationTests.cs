using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Memory.Tests;

public sealed class MemoryContextPackValidationTests
{
    [Fact]
    public async Task Query_clears_feedback_handle_when_profile_does_not_support_feedback()
    {
        var driver = new ContextPackDriver(CreateValidPack());
        using var root = CreateServiceProvider(driver);
        using var scope = root.CreateScope();
        var services = scope.ServiceProvider;
        var profile = CreateProfile();
        await services.GetRequiredService<IMemoryProviderProfileStore>()
            .UpsertAsync(profile, DateTimeOffset.UtcNow);

        var result = await services.GetRequiredService<IMemoryOperationHandler>()
            .ExecuteQueryAsync(MemoryOperationRequestBuilder.Query(
                MemoryOperationCaller.Tool("memory.feedback-honesty", MemoryWorkerIntegrityTestData.CreateRequester()),
                MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.ContextQuerySync) with
                {
                    ExplicitProviderId = profile.InstanceId
                },
                new MemoryContextQueryRequest(
                    "feedback honesty",
                    [MemoryCapabilityIds.ContextQuerySync],
                    MemorySourceProvenance.None),
                MemoryLedgerRetentionPolicy.Expiring(
                    DateTimeOffset.UtcNow.AddHours(1),
                    DateTimeOffset.UtcNow.AddHours(2))));

        Assert.Equal(MemoryOperationHandlerStatus.Completed, result.Status);
        Assert.Null(result.Output?.FeedbackHandle);
        Assert.Null(result.FeedbackHandle);
        Assert.Null(result.OperationRecord?.Extensions.GetContextDelivery());
    }

    [Theory]
    [InlineData(InvalidContextPackKind.TooManySections, "section limit")]
    [InlineData(InvalidContextPackKind.TooManyCitations, "citation limit")]
    [InlineData(InvalidContextPackKind.TooManyBytes, "UTF-8 byte budget")]
    [InlineData(InvalidContextPackKind.InvalidConfidence, "confidence")]
    [InlineData(InvalidContextPackKind.MissingRequiredText, "malformed context pack")]
    public async Task Invalid_provider_context_pack_fails_without_partial_output(
        InvalidContextPackKind kind,
        string expectedDiagnostic)
    {
        var driver = new ContextPackDriver(CreatePack(kind));
        using var root = CreateServiceProvider(driver);
        using var scope = root.CreateScope();
        var services = scope.ServiceProvider;
        var profile = CreateProfile();
        await services.GetRequiredService<IMemoryProviderProfileStore>()
            .UpsertAsync(profile, DateTimeOffset.UtcNow);
        var request = new MemoryContextQueryRequest(
            "validate provider output",
            [MemoryCapabilityIds.ContextQuerySync],
            MemorySourceProvenance.None)
        {
            Context = MemoryRequestContext.Default with
            {
                Budget = new MemoryBudget(2, 1_000, 1_000, TimeSpan.FromSeconds(10))
            }
        };

        var result = await services.GetRequiredService<IMemoryOperationHandler>()
            .ExecuteQueryAsync(MemoryOperationRequestBuilder.Query(
                MemoryOperationCaller.Tool("memory.validation", MemoryWorkerIntegrityTestData.CreateRequester()),
                MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.ContextQuerySync) with
                {
                    ExplicitProviderId = profile.InstanceId
                },
                request,
                MemoryLedgerRetentionPolicy.Expiring(
                    DateTimeOffset.UtcNow.AddHours(1),
                    DateTimeOffset.UtcNow.AddHours(2))));

        Assert.Equal(MemoryOperationHandlerStatus.DriverFailed, result.Status);
        Assert.Equal(MemoryLedgerStatus.Failed, result.OperationRecord?.Status);
        Assert.Null(result.Output);
        Assert.Contains(expectedDiagnostic, result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    private static ServiceProvider CreateServiceProvider(IMemoryProviderDriver driver)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseInMemoryDatabase($"memory-context-validation-{Guid.NewGuid():N}"));
        services.AddSingleton(driver);
        services.AddGenericMemoryModule();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static MemoryProviderProfile CreateProfile() =>
        new(
            MemoryProviderInstanceId.Parse("provider.validation"),
            "Validation provider",
            MemoryProviderDriverKind.Mock,
            IsEnabled: true,
            MemoryProviderHealthState.Healthy,
            MemoryProviderWorkspaceScope.AllWorkspaces,
            SelectionTags: [],
            MemoryProviderProfilePolicy.Default,
            new MemoryProviderManifest(
                MemoryProviderKind.Parse("memory.mock"),
                MemoryProtocolVersion.Current,
                [new MemoryCapabilityDescriptor(MemoryCapabilityIds.ContextQuerySync, "1", Supported: true)],
                MemoryProviderInteractionSupport.SyncQueryOnly,
                UiSurfaces: [],
                new MemoryProviderLimits(2, 1, 4, TimeSpan.FromMinutes(1)),
                MemoryExtensionData.Empty));

    private static MemoryContextPack CreatePack(InvalidContextPackKind kind)
    {
        var citations = kind == InvalidContextPackKind.TooManyCitations
            ? new[]
            {
                new MemoryCitation("memory://one", "one"),
                new MemoryCitation("memory://two", "two")
            }
            : [new MemoryCitation("memory://one", "one")];
        var section = new MemoryContextSection(
            "Memory",
            kind == InvalidContextPackKind.TooManyBytes ? new string('x', 2_000) : "context",
            citations,
            kind == InvalidContextPackKind.InvalidConfidence ? 1.1m : 0.8m);
        var sections = kind == InvalidContextPackKind.TooManySections
            ? new[] { section, section, section }
            : [section];
        return new MemoryContextPack(
            MemoryContextPackId.New(),
            kind == InvalidContextPackKind.MissingRequiredText ? " " : "summary",
            sections,
            Warnings: [],
            ProviderConfidence: 0.8m,
            FeedbackHandle: MemoryFeedbackHandle.Parse("memory-feedback:untrusted"));
    }

    private static MemoryContextPack CreateValidPack() =>
        new(
            MemoryContextPackId.New(),
            "summary",
            [new MemoryContextSection(
                "Memory",
                "context",
                [new MemoryCitation("memory://one", "one")],
                0.8m)],
            Warnings: [],
            ProviderConfidence: 0.8m,
            FeedbackHandle: MemoryFeedbackHandle.Parse("memory-feedback:untrusted"));

    public enum InvalidContextPackKind
    {
        TooManySections = 0,
        TooManyCitations = 1,
        TooManyBytes = 2,
        InvalidConfidence = 3,
        MissingRequiredText = 4
    }

    private sealed class ContextPackDriver(MemoryContextPack contextPack) : IMemoryProviderDriver
    {
        public MemoryProviderDriverKind DriverKind => MemoryProviderDriverKind.Mock;

        public Task<MemoryProviderDriverResult> ExecuteContextQueryAsync(
            MemoryProviderProfile provider,
            MemoryOperationRecord operation,
            MemoryContextQueryRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(MemoryProviderDriverResult.ContextPackResult(contextPack, "provider response"));
    }
}
