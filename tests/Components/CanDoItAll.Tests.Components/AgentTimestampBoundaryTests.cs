using System.Globalization;
using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Overview;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentTimestampBoundaryTests {
    [Theory]
    [InlineData("cs-CZ", 5, 0)]
    [InlineData("ar-SA", -7, 0)]
    [InlineData("cs-CZ", 5, 1)]
    [InlineData("ar-SA", -7, 1)]
    [InlineData("cs-CZ", 5, 2)]
    [InlineData("ar-SA", -7, 2)]
    [InlineData("cs-CZ", 5, 3)]
    [InlineData("ar-SA", -7, 3)]
    [InlineData("cs-CZ", 5, 4)]
    [InlineData("ar-SA", -7, 4)]
    public void Agent_presentation_timestamps_are_invariant_UTC(string culture, int offset, int boundary) {
        var previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var timestamp = new DateTimeOffset(2026, 9, 9, 12, 34, 56, TimeSpan.Zero).ToOffset(TimeSpan.FromHours(offset));
            const string expected = "2026-09-09 12:34:56 UTC";
            using var context = new BunitContext();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddCanDoItAllBaseLib();
            switch (boundary) {
                case 0:
                    Assert.Equal(expected, AgentUsageDisplay.FormatLastUsed(timestamp));
                    break;
                case 1:
                    var provider = AgentDetailsDialogAvatarGenerationTests.CreateImageProvider() with { LastCheckedAtUtc = timestamp };
                    Assert.Contains($"Last checked {expected}.", ProviderProfileDisplayAdapter.BuildStatusText(provider), StringComparison.Ordinal);
                    break;
                case 2:
                    var agent = AgentChatPanelResponsivenessTests.CreateAgent() with { UpdatedAtUtc = timestamp };
                    var card = AgentParticipantPresentationMapper.MapCard(agent, new(false, false, false, "Agent", false, false,
                        false, false, true, false, false, false, false, "", false, false, "Selected"));
                    Assert.Contains(card.Metadata, item => item.Value == $"Updated {expected}");
                    break;
                case 3:
                    var capability = new CapabilityCatalogItem(Guid.NewGuid(), CapabilityKind.Skill, "fixture", "Fixture proof", "", "", "{}",
                        CapabilityProofStatus.Verified, "Public proof", timestamp, false);
                    var proof = context.Render<CapabilityProofPanel>(parameters => parameters
                        .Add(component => component.Capabilities, new[] { capability })
                        .Add(component => component.SelectedCapabilityIds, Array.Empty<Guid>()));
                    Assert.Contains(expected, proof.Markup, StringComparison.Ordinal);
                    break;
                default:
                    var memory = new AgentMemoryRecord(Guid.NewGuid(), Guid.NewGuid(), default, "Public memory", "Content", "fixture", 1, "{}", timestamp);
                    var history = context.Render<MemoryHistoryPanel>(parameters => parameters.Add(component => component.Entries, new[] { memory }));
                    Assert.Contains(expected, history.Markup, StringComparison.Ordinal);
                    break;
            }
        } finally {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
