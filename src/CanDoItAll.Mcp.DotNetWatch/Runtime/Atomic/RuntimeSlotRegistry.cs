using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Mcp.DotNetWatch.Configuration;

namespace CanDoItAll.Mcp.DotNetWatch.Runtime.Atomic;

public sealed class RuntimeSlotRegistry(RuntimeConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public RuntimeSlotState GetState(string logicalAppId)
    {
        var safeAppId = Sanitize(logicalAppId);
        var logicalAppDirectory = Path.Combine(configuration.RuntimeSlotRoot, safeAppId);
        var historyDirectory = Path.Combine(logicalAppDirectory, "history");
        var transactionsDirectory = Path.Combine(logicalAppDirectory, "transactions");
        var slotADirectory = Path.Combine(logicalAppDirectory, "slot-a");
        var slotBDirectory = Path.Combine(logicalAppDirectory, "slot-b");
        Directory.CreateDirectory(historyDirectory);
        Directory.CreateDirectory(transactionsDirectory);
        Directory.CreateDirectory(slotADirectory);
        Directory.CreateDirectory(slotBDirectory);

        var activePointerPath = Path.Combine(logicalAppDirectory, "active.json");
        var record = File.Exists(activePointerPath)
            ? Read<LogicalAppRecord>(activePointerPath)
            : new LogicalAppRecord(logicalAppId, null, null, null, null, null, null, false);

        return new RuntimeSlotState(
            App: record,
            LogicalAppDirectory: logicalAppDirectory,
            ActivePointerPath: activePointerPath,
            HistoryDirectory: historyDirectory,
            TransactionsDirectory: transactionsDirectory,
            SlotADirectory: slotADirectory,
            SlotBDirectory: slotBDirectory);
    }

    public string SelectInactiveSlot(RuntimeSlotState state)
        => string.Equals(state.App.CurrentSlotId, "slot-a", StringComparison.OrdinalIgnoreCase) ? "slot-b" : "slot-a";

    public string GetSlotPayloadPath(RuntimeSlotState state, string slotId)
        => Path.Combine(GetSlotDirectory(state, slotId), "payload");

    public string GetSlotArtifactsPath(RuntimeSlotState state, string slotId)
        => Path.Combine(GetSlotDirectory(state, slotId), "artifacts");

    public string GetSlotManifestPath(RuntimeSlotState state, string slotId)
        => Path.Combine(GetSlotDirectory(state, slotId), "manifest.json");

    public SlotManifest? ReadSlotManifest(RuntimeSlotState state, string? slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId))
        {
            return null;
        }

        var manifestPath = GetSlotManifestPath(state, slotId);
        return File.Exists(manifestPath) ? Read<SlotManifest>(manifestPath) : null;
    }

    public AtomicTransactionRecord? ReadTransaction(RuntimeSlotState state, string? transactionId)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
        {
            return null;
        }

        var transactionPath = Path.Combine(state.TransactionsDirectory, $"{transactionId}.json");
        return File.Exists(transactionPath) ? Read<AtomicTransactionRecord>(transactionPath) : null;
    }

    public void SaveSlotManifest(RuntimeSlotState state, SlotManifest manifest)
        => Write(GetSlotManifestPath(state, manifest.SlotId), manifest);

    public void SaveTransaction(RuntimeSlotState state, AtomicTransactionRecord transaction)
        => Write(Path.Combine(state.TransactionsDirectory, $"{transaction.TransactionId}.json"), transaction);

    public void SaveLogicalApp(RuntimeSlotState state, LogicalAppRecord app)
    {
        Write(state.ActivePointerPath, app);
        var historyPath = Path.Combine(state.HistoryDirectory, $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{app.LogicalAppId}.json");
        Write(historyPath, app);
    }

    public RuntimeRevisionData CreatePublishedRevision(string logicalAppId, string slotId, string payloadRoot)
    {
        var signature = ComputeDirectoryHash(payloadRoot);
        return new RuntimeRevisionData(
            Kind: "PublishedBundle",
            Value: $"{logicalAppId}:{slotId}:{signature}",
            ObservedUtc: DateTimeOffset.UtcNow,
            IsConfirmed: true);
    }

    public AtomicStatusSnapshot GetSnapshot(string logicalAppId)
    {
        var state = GetState(logicalAppId);
        var activeSlot = ReadSlotManifest(state, state.App.CurrentSlotId);
        var activeTransaction = ReadTransaction(state, state.App.LastCommittedTransactionId);
        return new AtomicStatusSnapshot(
            App: state.App,
            ActiveSlot: activeSlot,
            CandidateSlot: ReadSlotManifest(state, SelectInactiveSlot(state)),
            ActiveTransaction: activeTransaction);
    }

    private static string ComputeDirectoryHash(string payloadRoot)
    {
        var files = Directory.Exists(payloadRoot)
            ? Directory.GetFiles(payloadRoot, "*", SearchOption.AllDirectories).OrderBy(static path => path, StringComparer.OrdinalIgnoreCase).ToArray()
            : [];
        using var sha256 = SHA256.Create();
        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(payloadRoot, file);
            var pathBytes = Encoding.UTF8.GetBytes(relative);
            sha256.TransformBlock(pathBytes, 0, pathBytes.Length, null, 0);
            var data = File.ReadAllBytes(file);
            sha256.TransformBlock(data, 0, data.Length, null, 0);
        }

        sha256.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha256.Hash ?? []).ToLowerInvariant();
    }

    private static T Read<T>(string path)
        => JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)
           ?? throw new InvalidOperationException($"Could not deserialize '{path}'.");

    private static void Write<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
    }

    private static string Sanitize(string value)
        => string.Concat(value.Select(static ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-')).Trim('-');

    private static string GetSlotDirectory(RuntimeSlotState state, string slotId)
        => string.Equals(slotId, "slot-a", StringComparison.OrdinalIgnoreCase) ? state.SlotADirectory : state.SlotBDirectory;
}
