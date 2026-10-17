using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Tests.Integration.Runtime;

internal enum ReceiptDiagnosticPhase { Ready, Started, StopRequested, Stopped }

internal sealed record ReceiptDiagnosticIdentity(int Schema, Guid Nonce, int ProcessId,
    DateTime ProcessStartedUtc, string RowId, string SourceIdentity, string Runtime);

internal sealed record ReceiptDiagnosticMessage(ReceiptDiagnosticIdentity Identity, ReceiptDiagnosticPhase Phase,
    bool Success, long TraceBytes, string? TraceSha256, string? FailureType);

internal sealed class AgentReceiptStoreDiagnosticSession(string root, ReceiptDiagnosticIdentity identity) {
    internal const string RootVariable = "CDITALL_RECEIPT_DIAGNOSTIC_ROOT";
    internal const string NonceVariable = "CDITALL_RECEIPT_DIAGNOSTIC_NONCE";

    internal static AgentReceiptStoreDiagnosticSession? CreateIfEnabled(string outputRoot, string rowId, string sourceIdentity) {
        var root = Environment.GetEnvironmentVariable(RootVariable);
        var nonceText = Environment.GetEnvironmentVariable(NonceVariable);
        if (root is null && nonceText is null) {
            return null;
        }
        if (root is null || !Guid.TryParseExact(nonceText, "N", out var nonce) || nonce == Guid.Empty) {
            throw new InvalidOperationException("Diagnostic collection requires an explicit owned root and nonce.");
        }
        root = ReceiptDiagnosticProtocol.RequireRoot(root);
        var relative = Path.GetRelativePath(outputRoot, root);
        if (relative is "." or ".." || Path.IsPathRooted(relative) ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) {
            throw new InvalidOperationException("Diagnostic control must be a dedicated child of the owned row output.");
        }
        if (Directory.EnumerateFileSystemEntries(root).Any()) {
            throw new InvalidOperationException("Diagnostic control requires a fresh empty owned directory.");
        }
        if (Environment.GetEnvironmentVariable("DOTNET_EnableEventPipe") is not null ||
            Environment.GetEnvironmentVariable("COMPlus_EnableEventPipe") is not null) {
            throw new InvalidOperationException("The explicit diagnostic session cannot coexist with startup EventPipe configuration.");
        }
        using var process = Process.GetCurrentProcess();
        var identity = new ReceiptDiagnosticIdentity(1, nonce, Environment.ProcessId,
            process.StartTime.ToUniversalTime(), rowId, sourceIdentity, RuntimeInformation.FrameworkDescription);
        ReceiptDiagnosticProtocol.RequireIdentity(identity);
        return new(root, identity);
    }

    internal async Task BeginAsync() {
        ReceiptDiagnosticProtocol.Write(root, new(identity, ReceiptDiagnosticPhase.Ready, true, 0, null, null));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var acknowledgement = await ReceiptDiagnosticProtocol.WaitAsync(root, identity, ReceiptDiagnosticPhase.Started, deadline.Token);
        if (!acknowledgement.Success) {
            throw new InvalidOperationException("The owned diagnostic collector refused to start.");
        }
    }

    internal async Task CompleteAsync() {
        ReceiptDiagnosticProtocol.Write(root, new(identity, ReceiptDiagnosticPhase.StopRequested, true, 0, null, null));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var acknowledgement = await ReceiptDiagnosticProtocol.WaitAsync(root, identity, ReceiptDiagnosticPhase.Stopped, deadline.Token);
        if (!acknowledgement.Success || acknowledgement.TraceBytes <= 0 ||
            acknowledgement.TraceBytes > ReceiptDiagnosticProtocol.MaximumTraceBytes ||
            !ReceiptDiagnosticProtocol.IsHash(acknowledgement.TraceSha256)) {
            throw new InvalidOperationException("The owned diagnostic collector did not confirm a closed trace.");
        }
    }
}

internal static class ReceiptDiagnosticProtocol {
    internal const long MaximumTraceBytes = 512L * 1024 * 1024;
    internal const long StopTraceBytes = 448L * 1024 * 1024;
    internal const int MaximumMessageBytes = 4096;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 8,
        Converters = { new JsonStringEnumConverter<ReceiptDiagnosticPhase>(allowIntegerValues: false) }
    };

    internal static string RequireRoot(string value) {
        if (!Path.IsPathFullyQualified(value)) {
            throw new InvalidOperationException("Diagnostic ownership requires an absolute directory.");
        }
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
        if (!Directory.Exists(root) || root == Path.GetPathRoot(root)) {
            throw new InvalidOperationException("The parent must pre-create the dedicated diagnostic directory.");
        }
        RequireNoReparsePoints(root);
        return root;
    }

    internal static void RequireIdentity(ReceiptDiagnosticIdentity identity) {
        if (identity is null || identity.Schema != 1 || identity.Nonce == Guid.Empty || identity.ProcessId <= 0 ||
            identity.ProcessStartedUtc == default || identity.ProcessStartedUtc.Kind != DateTimeKind.Utc ||
            !Guid.TryParseExact(identity.RowId, "N", out var rowId) || rowId == Guid.Empty ||
            !IsHash(identity.SourceIdentity) || string.IsNullOrWhiteSpace(identity.Runtime) || identity.Runtime.Length > 128) {
            throw new InvalidOperationException("Diagnostic identity is incomplete or unsupported.");
        }
    }

    internal static bool IsHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    internal static ReceiptDiagnosticIdentity ReadIdentity(string path) {
        RequireNoReparsePoints(Path.GetFullPath(path));
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var identity = JsonSerializer.Deserialize<ReceiptDiagnosticIdentity>(ReadBounded(stream), Json)
            ?? throw new InvalidOperationException("Diagnostic identity is absent.");
        RequireIdentity(identity);
        return identity;
    }

    internal static void Write(string root, ReceiptDiagnosticMessage message) {
        RequireMessage(message);
        RequireRoot(root);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, Json);
        if (bytes.Length > MaximumMessageBytes) {
            throw new InvalidOperationException("Diagnostic control message exceeds its bound.");
        }
        using var stream = new FileStream(Path.Combine(root, FileName(message.Phase)), FileMode.CreateNew,
            FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    internal static async Task<ReceiptDiagnosticMessage> WaitAsync(string root, ReceiptDiagnosticIdentity identity,
        ReceiptDiagnosticPhase phase, CancellationToken cancellationToken) {
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            RequireRoot(root);
            var path = Path.Combine(root, FileName(phase));
            try {
                RequireNoReparsePoints(path);
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var message = JsonSerializer.Deserialize<ReceiptDiagnosticMessage>(ReadBounded(stream), Json)
                    ?? throw new InvalidOperationException("Diagnostic acknowledgement is absent.");
                RequireMessage(message);
                if (message.Identity != identity || message.Phase != phase) {
                    throw new InvalidOperationException("Diagnostic acknowledgement belongs to another operation or host.");
                }
                return message;
            } catch (FileNotFoundException) {
            } catch (IOException exception) when ((exception.HResult & 0xffff) == 32) {
            }
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    private static byte[] ReadBounded(FileStream stream) {
        if (stream.Length is <= 0 or > MaximumMessageBytes) {
            throw new InvalidOperationException("Diagnostic control input exceeds its bound.");
        }
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) {
            throw new InvalidOperationException("Diagnostic control input changed during observation.");
        }
        return bytes;
    }

    private static void RequireMessage(ReceiptDiagnosticMessage message) {
        RequireIdentity(message.Identity);
        if (!Enum.IsDefined(message.Phase) || message.TraceBytes < 0 ||
            message.FailureType is { Length: > 512 } ||
            message.FailureType?.Any(character => !char.IsLetterOrDigit(character) && character is not ('.' or '_' or '+' or '`')) == true ||
            message.TraceSha256 is not null && !IsHash(message.TraceSha256)) {
            throw new InvalidOperationException("Diagnostic control state is invalid.");
        }
        if (message.Success && message.FailureType is not null || !message.Success && message.FailureType is null ||
            message.Phase != ReceiptDiagnosticPhase.Stopped && (message.TraceBytes != 0 || message.TraceSha256 is not null)) {
            throw new InvalidOperationException("Diagnostic control evidence is inconsistent.");
        }
    }

    private static string FileName(ReceiptDiagnosticPhase phase) => phase switch {
        ReceiptDiagnosticPhase.Ready => "ready.json",
        ReceiptDiagnosticPhase.Started => "started.json",
        ReceiptDiagnosticPhase.StopRequested => "stop-requested.json",
        ReceiptDiagnosticPhase.Stopped => "stopped.json",
        _ => throw new ArgumentOutOfRangeException(nameof(phase))
    };

    private static void RequireNoReparsePoints(string path) {
        for (var current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current)) {
            try {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) {
                    throw new InvalidOperationException("Diagnostic ownership contains a reparse point.");
                }
            } catch (FileNotFoundException) when (current == path) {
            }
        }
    }
}
