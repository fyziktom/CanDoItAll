using System.Text.Json;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.AgentContext;
using CanDoItAll.Modules.Workbench.Pages.Components.ProjectStructure;
using CanDoItAll.Modules.Workbench.ProjectStructure;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed partial class ProjectStructureAgentChatContextProviderTests {
    [Theory]
    [InlineData(ProjectStructureAgentChatView.Canvas, 1)]
    [InlineData(ProjectStructureAgentChatView.Gantt, 2)]
    public void Actual_provider_publication_and_invocation_roundtrip_exact_owner_attachments(ProjectStructureAgentChatView view, int expectedAttachments) {
        using var context = CreateContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var agent = CreateAgent();
        var projectId = Guid.NewGuid();
        var surface = CreateSurface(projectId, "Original project", [CreateNode("node:original", "Original title")]);
        RegisterProviderServices(context, registry, agent);
        var now = DateTimeOffset.UtcNow;
        var observation = new ProjectStructureGanttObservation(projectId, ProjectStructureGanttObservationCompleteness.Ready,
            7, 4, 2, 1, 0, now, now.AddDays(3), ["Original schedule warning"], "original-row-order", null, now);
        var cut = context.Render<ProjectStructureAgentChatContextProvider>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.ProjectName, surface.ProjectName)
            .Add(component => component.Surface, surface)
            .Add(component => component.ActiveView, view)
            .Add(component => component.GanttObservation, observation)
            .Add(component => component.ContextAccessState, AgentChatContextAccessState.Ready));
        cut.WaitForAssertion(() => Assert.Equal(expectedAttachments, registry.Capture()!.Attachments.Length));
        var captured = registry.Capture()!;
        var generation = captured.Attachments[0].DatabaseProfileGeneration;
        var invocation = AgentChatContextInvocationFactory.Create(captured, agent.Id, null, "Read captured facts.",
            AgentExecutionOperationId.New(), generation, DateTimeOffset.UtcNow);
        var original = invocation.Options.TransientContext!;
        var codecs = new AgentChatContextAttachmentPersistence([new ProjectStructureInvocationSnapshotCodec(), new ProjectStructureGanttObservationCodec()]);
        Assert.True(codecs.CanCapture(original));
        var serialized = JsonSerializer.Serialize(codecs.Capture(original));
        var saved = JsonSerializer.Deserialize<AgentToolAdmittedRuntimeContext>(serialized)!;
        var restored = new AgentChatContextAttachmentPersistence([new ProjectStructureInvocationSnapshotCodec(), new ProjectStructureGanttObservationCodec()]).Restore(saved);
        Assert.Equal(AgentChatContextDigest.Compute(original), AgentChatContextDigest.Compute(restored));
        Assert.Equal(expectedAttachments, restored.Attachments.Length);
        for (var index = 0; index < original.Attachments.Length; index++) {
            var before = original.Attachments[index];
            var after = restored.Attachments[index];
            Assert.Equal(before.AttachmentType, after.AttachmentType);
            Assert.Equal(before.ContentFingerprint, after.ContentFingerprint);
            Assert.Equal(before.CoverageFingerprint, after.CoverageFingerprint);
            Assert.Equal(before.FreshnessFingerprint, after.FreshnessFingerprint);
            Assert.Equal(before.CapturedAtUtc, after.CapturedAtUtc);
            Assert.Equal(before.FreshUntilUtc, after.FreshUntilUtc);
            Assert.Equal(AgentChatContextAttachmentFreshness.Expired, after.ResolveFreshness(generation, after.FreshUntilUtc!.Value));
            Assert.Equal(AgentChatContextAttachmentFreshness.ProfileMismatch, after.ResolveFreshness(new(generation.Value + 1), now));
        }
        var snapshot = Assert.Single(restored.GetAttachments<ProjectStructureInvocationSnapshot>());
        Assert.True(snapshot.TryGetAttachment<ProjectStructureInvocationSnapshot>(out var payload));
        Assert.Equal("Original title", Assert.Single(payload.Nodes).Title);
        if (view == ProjectStructureAgentChatView.Gantt) {
            var gantt = Assert.Single(restored.GetAttachments<ProjectStructureGanttObservationAttachment>());
            Assert.True(gantt.TryGetAttachment<ProjectStructureGanttObservationAttachment>(out var value));
            Assert.Equal(observation.ContentFingerprint, value.Observation.ContentFingerprint);
            Assert.Equal(7, value.Observation.TaskCount);
        }
    }
}
