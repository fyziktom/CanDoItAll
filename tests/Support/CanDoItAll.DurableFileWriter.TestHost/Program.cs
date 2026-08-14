using CanDoItAll.Infrastructure;

if (args.Length != 3 || !string.Equals(args[0], "write-and-wait-before-commit", StringComparison.Ordinal))
{
    return 64;
}

string targetPath = Path.GetFullPath(args[1]);
string readyPath = Path.GetFullPath(args[2]);
string managedRoot = Path.GetDirectoryName(targetPath)
    ?? throw new InvalidOperationException("The test target does not have a parent directory.");
var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
await writer.WriteTextAsync(
    managedRoot,
    targetPath,
    "uncommitted replacement",
    beforeCommit: _ =>
    {
        File.WriteAllText(readyPath, "ready");
        return new ValueTask(Task.Delay(Timeout.InfiniteTimeSpan));
    });
return 0;
