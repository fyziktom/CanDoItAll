#if DEBUG
[assembly: System.Reflection.Metadata.MetadataUpdateHandlerAttribute(typeof(CanDoItAll.Web.Infrastructure.RuntimeHotReloadTracker))]
#endif

namespace CanDoItAll.Web.Infrastructure;

public static class RuntimeHotReloadTracker
{
    private static long _generation;

    public static long CurrentGeneration => Interlocked.Read(ref _generation);

    internal static void ClearCache(Type[]? updatedTypes)
    {
    }

    internal static void UpdateApplication(Type[]? updatedTypes)
    {
        Interlocked.Increment(ref _generation);
    }
}


