using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentAvatarRenderingTests
{
    [Fact]
    public void Overview_usage_list_renders_shared_avatar_with_image_url()
    {
        using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();
        var avatarUrl = AgentAvatarImageCatalog.BundledAvatarUrls[0];

        var cut = context.Render<AgentOverviewUsageList>(parameters => parameters
            .Add(component => component.IsLoaded, true)
            .Add(component => component.TestId, "agent-avatar-test")
            .Add(component => component.Rows, [
                CreateRow("Delivery Manager", avatarUrl)
            ]));

        Assert.Contains(avatarUrl, cut.Markup, StringComparison.Ordinal);
        Assert.NotNull(cut.Find("[data-testid='agent-avatar-test-avatar']"));
        Assert.Empty(cut.FindAll("[data-testid='agent-avatar-test-avatar-fallback']"));
    }

    [Fact]
    public void Overview_usage_list_keeps_accessible_fallback_when_image_url_is_missing()
    {
        using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();

        var cut = context.Render<AgentOverviewUsageList>(parameters => parameters
            .Add(component => component.IsLoaded, true)
            .Add(component => component.TestId, "agent-avatar-test")
            .Add(component => component.Rows, [
                CreateRow("Fallback Agent", null)
            ]));

        Assert.Contains("Fallback Agent", cut.Markup, StringComparison.Ordinal);
        Assert.NotNull(cut.Find("[data-testid='agent-avatar-test-avatar']"));
    }

    [Fact]
    public void Overview_usage_list_renders_execution_count_as_success_even_with_failures()
    {
        using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();

        var cut = context.Render<AgentOverviewUsageList>(parameters => parameters
            .Add(component => component.IsLoaded, true)
            .Add(component => component.Rows, [
                CreateRow("Delivery Manager", null)
            ]));

        Assert.Contains(cut.FindAll(".cda-status-badge--tone-success"), badge => badge.TextContent.Contains("12 runs", StringComparison.Ordinal));
        Assert.Contains(cut.FindAll(".cda-status-badge--tone-danger"), badge => badge.TextContent.Contains("1 failed", StringComparison.Ordinal));
    }

    [Fact]
    public void Overview_usage_list_renders_total_runs_in_failure_rank()
    {
        using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();

        var cut = context.Render<AgentOverviewUsageList>(parameters => parameters
            .Add(component => component.IsLoaded, true)
            .Add(component => component.ShowFailureRank, true)
            .Add(component => component.Rows, [
                CreateRow("Delivery Manager", null)
            ]));

        Assert.Contains(cut.FindAll(".cda-status-badge--tone-danger"), badge => badge.TextContent.Contains("1 failed", StringComparison.Ordinal));
        Assert.Contains(cut.FindAll(".cda-status-badge--tone-success"), badge => badge.TextContent.Contains("12 runs", StringComparison.Ordinal));
    }

    [Fact]
    public void Unified_consumer_list_preserves_compact_avatar_led_agent_rows()
    {
        using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();
        var agentId = Guid.NewGuid();
        var avatarUrl = AgentAvatarImageCatalog.BundledAvatarUrls[0];

        var cut = context.Render<ProviderUsageConsumerList>(parameters => parameters
            .Add(component => component.TestId, "consumer-avatar-test")
            .Add(component => component.Rows, [CreateConsumerRow(agentId, ProviderUsageConsumerKind.Agent)])
            .Add(component => component.AvatarImageUrls, new Dictionary<string, string?>
            {
                [agentId.ToString("D")] = avatarUrl
            }));

        Assert.Single(cut.FindAll("[data-testid='consumer-avatar-test-row']"));
        var avatar = cut.Find("[data-testid='consumer-avatar-test-avatar']");
        Assert.Equal(avatarUrl, avatar.QuerySelector("img")?.GetAttribute("src"));
        Assert.Contains(
            cut.FindAll(".cda-status-badge--tone-success"),
            badge => badge.TextContent.Contains("12 runs", StringComparison.Ordinal));
        Assert.Contains(
            cut.FindAll(".cda-status-badge--tone-danger"),
            badge => badge.TextContent.Contains("1 failed", StringComparison.Ordinal));
        Assert.DoesNotContain("tokens", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unified_failure_ranking_keeps_failure_first_and_run_count_secondary()
    {
        using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();

        var cut = context.Render<ProviderUsageConsumerList>(parameters => parameters
            .Add(component => component.ShowFailureRank, true)
            .Add(component => component.Rows, [
                CreateConsumerRow(Guid.NewGuid(), ProviderUsageConsumerKind.SimpleChatDefinition)
            ]));

        Assert.NotNull(cut.Find("[data-testid='provider-usage-consumer-list-avatar']"));
        Assert.Contains("Simple Chat", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(
            cut.FindAll(".cda-status-badge--tone-danger"),
            badge => badge.TextContent.Contains("1 failed", StringComparison.Ordinal));
        Assert.Contains(
            cut.FindAll(".cda-status-badge--tone-success"),
            badge => badge.TextContent.Contains("12 runs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Avatar_upload_formatter_accepts_supported_image()
    {
        var dataUrl = await AvatarUploadFormatter.BuildDataUrlAsync(
            new FakeBrowserFile("avatar.png", "image/png", [1, 2, 3]));

        Assert.Equal("data:image/png;base64,AQID", dataUrl);
    }

    [Fact]
    public async Task Avatar_upload_formatter_normalizes_jpg_content_type()
    {
        var dataUrl = await AvatarUploadFormatter.BuildDataUrlAsync(
            new FakeBrowserFile("avatar.jpg", "image/jpg", [1, 2, 3]));

        Assert.Equal("data:image/jpeg;base64,AQID", dataUrl);
    }

    [Fact]
    public async Task Avatar_upload_formatter_rejects_unsupported_file_type()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => AvatarUploadFormatter.BuildDataUrlAsync(
                new FakeBrowserFile("avatar.txt", "text/plain", [1, 2, 3])));

        Assert.Contains("PNG, JPEG, WebP, or GIF", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Avatar_upload_formatter_rejects_oversized_image()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => AvatarUploadFormatter.BuildDataUrlAsync(
                new FakeBrowserFile(
                    "avatar.png",
                    "image/png",
                    [1, 2, 3],
                    AvatarUploadFormatter.MaxAvatarUploadBytes + 1)));

        Assert.Contains("128 KB or smaller", exception.Message, StringComparison.Ordinal);
    }

    private static AgentOverviewUsageRow CreateRow(string name, string? avatarImageUrl)
    {
        return new AgentOverviewUsageRow(
            Guid.NewGuid(),
            name,
            avatarImageUrl,
            RunCount: 12,
            FailedRunCount: 1,
            UsageObservationCount: 3,
            KnownUsageObservationCount: 2,
            UnknownUsageObservationCount: 1,
            InputTokens: 100,
            CachedInputTokens: 0,
            OutputTokens: 40,
            ReasoningTokens: 0,
            TotalTokens: 140,
            KnownCostUsd: 0.02m,
            LastUsedAtUtc: DateTimeOffset.UtcNow);
    }

    private static ProviderUsageConsumerRow CreateConsumerRow(
        Guid consumerId,
        ProviderUsageConsumerKind consumerKind)
    {
        return new(
            consumerKind,
            consumerId.ToString("D"),
            consumerKind == ProviderUsageConsumerKind.Agent ? "Delivery Manager" : "Test Chat",
            new(
                ExecutionCount: 12,
                FailedExecutionCount: 1,
                CancelledExecutionCount: 0,
                UsageObservationCount: 3,
                KnownUsageObservationCount: 2,
                UnknownUsageObservationCount: 1,
                PricedObservationCount: 2,
                UnpricedObservationCount: 1,
                Tokens: new(100, 0, 0, 40, 0, 140),
                KnownCostUsd: 0.02m),
            DateTimeOffset.UtcNow);
    }

    private sealed class FakeBrowserFile(
        string name,
        string contentType,
        byte[] content,
        long? size = null) : IBrowserFile
    {
        public string Name { get; } = name;

        public DateTimeOffset LastModified { get; } = DateTimeOffset.UtcNow;

        public long Size { get; } = size ?? content.LongLength;

        public string ContentType { get; } = contentType;

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
            => new MemoryStream(content, writable: false);
    }
}
