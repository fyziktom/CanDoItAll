using System.Runtime.CompilerServices;
using Bunit;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

internal static class ComponentTestAssemblyConfiguration
{
    [ModuleInitializer]
    public static void Configure()
    {
        BunitContext.DefaultWaitTimeout = TimeSpan.FromSeconds(30);
    }
}
