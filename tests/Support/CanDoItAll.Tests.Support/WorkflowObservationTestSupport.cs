using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Support;

public static class WorkflowObservationTestSupport {
    public static WorkflowChanges Create(EditingChangeFeed? feed = null, EditingScope? scope = null)
        => new(feed ?? new(), scope ?? new(EditingScopeKind.Dataset, Guid.NewGuid()), new(), NullLogger<WorkflowChanges>.Instance);
}
