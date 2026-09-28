using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;

const string RebuildOption = "--rebuild";
const string MigrateOption = "--migrate-legacy";
if (args.Length is < 1 or > 5) {
    Console.Error.WriteLine("Usage: dotnet run --project tools/UsageIndex -- <workspace-root> [<scope-kind> <scope-key>] [--rebuild] [--migrate-legacy]");
    return 1;
}
var rebuild = args.Contains(RebuildOption, StringComparer.Ordinal);
var migrate = args.Contains(MigrateOption, StringComparer.Ordinal);
var values = args.Where(value => value != RebuildOption && value != MigrateOption).ToArray();
if (values.Length is not (1 or 3)) {
    Console.Error.WriteLine("Supply a workspace root and, optionally, a scope kind and key.");
    return 1;
}
var root = Path.GetFullPath(values[0]);
if (!Directory.Exists(root)) {
    Console.Error.WriteLine("The workspace root does not exist.");
    return 1;
}
var scope = values.Length == 3 ? new WorkspaceScopeDescriptor(values[1].ToLowerInvariant() switch {
    "organization" => WorkspaceScopeKind.Organization,
    "project" => WorkspaceScopeKind.Project,
    "tenant" => WorkspaceScopeKind.Tenant,
    "process" => WorkspaceScopeKind.Process,
    _ => throw new ArgumentException("Use organization, project, tenant or process for the scope kind.")
}, values[2]) : WorkspaceScopeDescriptor.Sandbox;
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    cancellation.Cancel();
};
var maintenance = new FileProviderUsageIndexMaintenance(root, scope);
try {
    if (migrate) {
        Console.WriteLine("Migrating canonical legacy workspace storage before usage indexing.");
        var store = new FileSandboxWorkspaceStore(root, scope);
        await store.LoadExecutionAsync(cancellation.Token);
        await store.LoadExecutionSummaryAsync(cancellation.Token);
    }
    FileUsageIndexProgress progress;
    do {
        progress = await maintenance.ProcessAsync(100, rebuild, cancellation.Token);
        rebuild = false;
        Console.WriteLine($"Indexed {progress.Processed}/{progress.Total}; complete={progress.Complete}.");
    } while (!progress.Complete);
    return 0;
} catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
    Console.Error.WriteLine("Indexing cancelled. Run the same command to resume.");
    return 2;
}
