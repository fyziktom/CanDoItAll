using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Editing;

namespace CanDoItAll.AgentFramework.UI.Teams;

public sealed class TeamMetadataComparison {
    public TeamMetadataComparison(AgentTeamEditorModel original, TeamMetadataSubmission local, AgentTeamEditorModel current) {
        if (original.Id is null || original.Id != local.Id || local.Id != current.Id || original.ExpectedMetadata is null ||
            current.ExpectedMetadata is null || original.ExpectedMetadata.CreatedAtUtc != current.ExpectedMetadata.CreatedAtUtc) {
            throw new InvalidOperationException("The team original is missing or belongs to a different team lifetime.");
        }
        Current = TeamMetadataSubmission.Capture(current);
        Name = new(original.Name, local.Name, current.Name, StringComparer.Ordinal);
        Description = TextMerge.Compare(original.Description, local.Description, current.Description);
        Icon = new(original.Icon, local.Icon, current.Icon, StringComparer.Ordinal);
    }

    public TeamMetadataSubmission Current { get; }
    public MergeField<string> Name { get; }
    public TextMergeResult Description { get; }
    public MergeField<string> Icon { get; }
    public bool IsResolved => Name.IsResolved && Description.Field.IsResolved && Icon.IsResolved;
}
