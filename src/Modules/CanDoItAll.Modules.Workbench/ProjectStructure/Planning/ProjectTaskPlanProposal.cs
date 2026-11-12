using System.Text.Json;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectTaskPlanDraft(int Version, IReadOnlyList<ProjectTaskPlanDraftItem> Tasks);
internal sealed record ProjectTaskPlanDraftItem(string Title, string Description, string Notes, int Priority);

internal sealed class ProjectTaskPlanProposal {
    internal const int CurrentVersion = 1;
    internal const int MaximumTasks = 100;
    internal const int MaximumTitleCharacters = 200;
    internal const int MaximumRawBytes = 262_144;
    internal const int MaximumEncodedBytes = 1_048_576;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private ProjectTaskPlanProposal(ProjectTaskPlanDraftItem[] tasks) {
        Tasks = Array.AsReadOnly(tasks);
    }

    internal IReadOnlyList<ProjectTaskPlanDraftItem> Tasks { get; }

    internal static ProjectTaskPlanProposal Normalize(ProjectTaskPlanDraft draft) {
        ArgumentNullException.ThrowIfNull(draft);
        var input = draft.Tasks;
        if (draft.Version != CurrentVersion || input is null) {
            throw new ProjectTaskPlanBasisException(ProjectTaskPlanBasisRefusal.InvalidSource);
        }
        var count = input.Count;
        if (count == 0) {
            throw new ProjectTaskPlanBasisException(ProjectTaskPlanBasisRefusal.InvalidSource);
        }
        ProjectTaskPlanBasisLimits.RequireBytes(count, MaximumTasks);
        var captured = new ProjectTaskPlanDraftItem[count];
        long bytes = 0;
        for (var index = 0; index < captured.Length; index++) {
            var item = input[index];
            if (item is null || item.Priority is < 0 or > 6) {
                throw new ProjectTaskPlanBasisException(ProjectTaskPlanBasisRefusal.InvalidSource);
            }
            bytes += ProjectTaskPlanBasisLimits.TextBytes(item.Title);
            bytes += ProjectTaskPlanBasisLimits.TextBytes(item.Description);
            bytes += ProjectTaskPlanBasisLimits.TextBytes(item.Notes);
            ProjectTaskPlanBasisLimits.RequireBytes(bytes, MaximumRawBytes);
            captured[index] = item;
        }
        for (var index = 0; index < captured.Length; index++) {
            var item = captured[index];
            var title = item.Title.Trim();
            if (title.Length == 0) {
                throw new ProjectTaskPlanBasisException(ProjectTaskPlanBasisRefusal.InvalidSource);
            }
            ProjectTaskPlanBasisLimits.RequireBytes(title.Length, MaximumTitleCharacters);
            captured[index] = new(title, item.Description.Trim(), item.Notes.Trim(), item.Priority);
        }
        using var encoded = new BoundedStream();
        JsonSerializer.Serialize(encoded, new ProjectTaskPlanDraft(CurrentVersion, captured), Options);
        return new(captured);
    }

    private sealed class BoundedStream : MemoryStream {
        public override void Write(byte[] buffer, int offset, int count) {
            RequireCapacity(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer) {
            RequireCapacity(buffer.Length);
            base.Write(buffer);
        }

        private void RequireCapacity(int count) {
            ProjectTaskPlanBasisLimits.RequireBytes(Length + count, MaximumEncodedBytes);
            var required = (int)Length + count;
            if (required > Capacity) {
                Capacity = (int)Math.Min(MaximumEncodedBytes, Math.Max(required, Math.Max(256L, 2L * Capacity)));
            }
        }
    }
}
