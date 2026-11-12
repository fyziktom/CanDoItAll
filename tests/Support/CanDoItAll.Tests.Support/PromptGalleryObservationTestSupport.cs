using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.Prompts;

namespace CanDoItAll.Tests.Support;

public static class PromptGalleryObservationTestSupport {
    public static PromptGalleryChanges Create(EditingChangeFeed? feed = null, IEditingChangePublisher? publisher = null) {
        var observer = feed ?? new EditingChangeFeed();
        return new(observer, publisher ?? observer, new(EditingScopeKind.Dataset, Guid.NewGuid()), new EditingOperationContext());
    }
}
