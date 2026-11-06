using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Collaboration;

public interface ICollaborationWorkspaceOwner {
    Task<CollaborationWorkspaceModel> GetWorkspaceAsync(Guid? selectedThreadId = null, CancellationToken cancellationToken = default,
        CollaborationListWindow window = default);
    Task<Result<Guid>> CreateThreadAsync(CollaborationThreadCreateRequest request, CancellationToken cancellationToken = default);
    Task<Result> AppendMessageAsync(CollaborationMessageWriteRequest request, CancellationToken cancellationToken = default);
    Task<Result> MarkThreadAsReadAsync(CollaborationReadObservation expected, CancellationToken cancellationToken = default);
}
