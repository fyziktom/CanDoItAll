using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Memory.Tests;

internal sealed class MemoryTestHostEnvironment : IHostEnvironment
{
    public static MemoryTestHostEnvironment Instance { get; } = new();

    public string EnvironmentName { get; set; } = Environments.Development;

    public string ApplicationName { get; set; } = "CanDoItAll.Memory.Tests";

    public string ContentRootPath { get; set; } = Path.GetTempPath();

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
