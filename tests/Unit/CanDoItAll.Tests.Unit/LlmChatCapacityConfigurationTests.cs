using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Composition;
using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit.LlmChats;

public sealed class LlmChatCapacityConfigurationTests
{
    [Fact]
    public void Configured_streaming_and_dispatch_values_bind_from_configuration()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["LlmChats:Streaming:MinimumChunkBytes"] = "128",
            ["LlmChats:Streaming:MaximumChunkBytes"] = "2048",
            ["LlmChats:Streaming:MaximumResponseCharacters"] = "10000",
            ["LlmChats:Streaming:MaximumResponseBytes"] = "40000",
            ["LlmChats:Dispatcher:WorkerCount"] = "3",
            ["LlmChats:Dispatcher:CandidateBatchSize"] = "24",
            ["LlmChats:Dispatcher:MaximumQueuedAge"] = "00:10:00",
            ["LlmChats:Dispatcher:MaximumOperationDuration"] = "01:00:00",
            ["LlmChats:Transfer:MaximumRecordsPerCollection"] = "12000",
            ["LlmChats:Transfer:MaximumTotalRecords"] = "50000"
        });

        provider.GetRequiredService<IStartupValidator>().Validate();
        var streaming = provider.GetRequiredService<LlmChatStreamingOptions>();
        var dispatcher = provider.GetRequiredService<LlmChatExecutionLeaseOptions>();
        var transfer = provider.GetRequiredService<LlmChatTransferOptions>();
        Assert.Equal((128, 2048, 10_000, 40_000), (
            streaming.MinimumChunkBytes,
            streaming.MaximumChunkBytes,
            streaming.MaximumResponseCharacters,
            streaming.MaximumResponseBytes));
        Assert.Equal((3, 24, TimeSpan.FromMinutes(10), TimeSpan.FromHours(1)), (
            dispatcher.WorkerCount,
            dispatcher.CandidateBatchSize,
            dispatcher.MaximumQueuedAge,
            dispatcher.MaximumOperationDuration));
        Assert.Equal((12_000, 50_000), (
            transfer.MaximumRecordsPerCollection,
            transfer.MaximumTotalRecords));
    }

    [Fact]
    public void Omitted_configuration_preserves_validated_safe_defaults()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>());

        provider.GetRequiredService<IStartupValidator>().Validate();
        var streaming = provider.GetRequiredService<LlmChatStreamingOptions>();
        var dispatcher = provider.GetRequiredService<LlmChatExecutionLeaseOptions>();
        var transfer = provider.GetRequiredService<LlmChatTransferOptions>();
        Assert.Equal(new LlmChatStreamingOptions(), streaming);
        Assert.Equal(new LlmChatExecutionLeaseOptions(), dispatcher);
        Assert.Equal(new LlmChatTransferOptions(), transfer);
        Assert.Equal(1, dispatcher.WorkerCount);
    }

    [Fact]
    public void Invalid_streaming_bound_combination_fails_startup()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["LlmChats:Streaming:MaximumChunkBytes"] =
                (LlmChatStreamingLimits.MaximumPersistedEventTextBytes + 1).ToString()
        });

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains("LLM Chat streaming configuration is invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Chunk_bound_cannot_exceed_persisted_event_text_bound()
    {
        var options = new LlmChatStreamingOptions
        {
            MaximumChunkBytes = LlmChatStreamingLimits.MaximumPersistedEventTextBytes + 1
        };

        Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
    }

    [Fact]
    public void Aggregate_bound_cannot_exceed_canonical_message_bound()
    {
        var characterOptions = new LlmChatStreamingOptions
        {
            MaximumResponseCharacters = LlmMessage.MaximumTextLength + 1
        };
        var byteOptions = new LlmChatStreamingOptions
        {
            MaximumResponseCharacters = 1_000,
            MaximumResponseBytes = 4_001
        };

        Assert.Throws<ArgumentOutOfRangeException>(characterOptions.Validate);
        Assert.Throws<ArgumentOutOfRangeException>(byteOptions.Validate);
    }

    [Fact]
    public void Availability_distinguishes_registration_from_progress()
    {
        var signal = new LlmChatOperationDispatchSignal();
        Assert.Equal(new(0, 0), signal.Availability);

        using var worker = signal.RegisterExecutor();
        Assert.True(signal.Availability.IsRegistered);
        Assert.True(signal.Availability.HasIdleWorker);
        Assert.False(signal.Availability.IsSaturated);

        using (signal.BeginProgress())
        {
            Assert.Equal(new(1, 1), signal.Availability);
            Assert.True(signal.Availability.IsSaturated);
        }

        Assert.Equal(new(1, 0), signal.Availability);
    }

    private static ServiceProvider BuildProvider(IReadOnlyDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCanDoItAllRuntimeModules(
            configuration,
            new TestHostEnvironment(AppContext.BaseDirectory, "CanDoItAll.Tests.Unit"),
            AppContext.BaseDirectory);
        return services.BuildServiceProvider();
    }
}
