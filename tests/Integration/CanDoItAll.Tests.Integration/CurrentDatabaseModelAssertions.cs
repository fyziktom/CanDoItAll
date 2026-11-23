using CanDoItAll.Modules.AgentFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CanDoItAll.Tests.Integration;

internal static class CurrentDatabaseModelAssertions {
    internal const int WorkflowEntityCount = 18;

    internal static void Canonical(IModel model) {
        Assert.Equal(187, model.GetEntityTypes().Count());
        Assert.Equal("Workspace_RemoteTemplatePublications", model.FindEntityType(typeof(RemoteTemplatePublication))?.GetTableName());
    }
}
