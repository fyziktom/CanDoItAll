using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Tests.Unit;

internal static class TestExternalTargetPathRegistry
{
    internal static IExternalTargetPathRegistry Create()
        => new ExternalTargetPathRegistry();
}
