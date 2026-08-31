using System.Collections;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;

internal static class Program {
    private const string BridgeSource = "Microsoft-Diagnostics-DiagnosticSource";
    private const string FrameworkSource = "CanDoItAll.AgentFramework";
    private const string HttpSource = "HttpHandlerDiagnosticListener";
    private static readonly object OutputGate = new();
    private static readonly Regex RunIdPattern = new(@"\[agentframework\.execution_run_id, ([0-9a-fA-F-]{32,36})\]", RegexOptions.CultureInvariant);
    private static int capturedHttpStarts;
    private static int capturedRunMappings;
    private static int unexpectedArguments;

    private static async Task<int> Main(string[] args) {
        try {
            var selfCheck = args.Contains("--self-check", StringComparer.Ordinal);
            var pid = selfCheck ? Environment.ProcessId : int.Parse(Value(args, "--pid"));
            var duration = TimeSpan.FromSeconds(selfCheck ? 4 : int.Parse(Value(args, "--seconds", "1200")));
            var stopFile = Value(args, "--stop-file", string.Empty);
            var specifications = string.Join('\n',
                "HttpHandlerDiagnosticListener/System.Net.Http.HttpRequestOut.Start:-TraceId=*Activity.TraceId;SpanId=*Activity.SpanId;ParentSpanId=*Activity.ParentSpanId;Method=Request.Method.Method",
                "HttpHandlerDiagnosticListener/System.Net.Http.HttpRequestOut.Stop:-TraceId=*Activity.TraceId;SpanId=*Activity.SpanId;ParentSpanId=*Activity.ParentSpanId;StatusCode=Response.StatusCode",
                "[AS]CanDoItAll.AgentFramework/Stop:-TraceId;SpanId;ParentSpanId;OperationName;RunTags=Tags.*Enumerate");
            var provider = new EventPipeProvider(BridgeSource, EventLevel.Informational, 0x803,
                new Dictionary<string, string> { ["FilterAndPayloadSpecs"] = specifications });
            var client = new DiagnosticsClient(pid);
            using var session = client.StartEventPipeSession(provider, requestRundown: false, circularBufferMB: 16);
            using var events = new EventPipeEventSource(session.EventStream);
            events.Dynamic.All += Process;
            var eventTask = Task.Run(events.Process);
            Write(new { kind = "ready", targetPid = pid, capturePid = Environment.ProcessId,
                utc = DateTimeOffset.UtcNow, clockFrequency = Stopwatch.Frequency, maximumSeconds = duration.TotalSeconds,
                filterVersion = 1, rawTracePersisted = false });
            if (selfCheck) {
                await Task.Delay(500);
                await SelfCheckAsync();
            }
            var timer = Stopwatch.StartNew();
            while (timer.Elapsed < duration && !eventTask.IsCompleted && (stopFile.Length == 0 || !File.Exists(stopFile))) {
                await Task.Delay(250);
            }
            session.Stop();
            await eventTask.WaitAsync(TimeSpan.FromSeconds(15));
            Write(new { kind = "stopped", utc = DateTimeOffset.UtcNow, capturedHttpStarts, capturedRunMappings, unexpectedArguments });
            if (selfCheck && (capturedHttpStarts != 1 || capturedRunMappings < 1 || unexpectedArguments != 0)) {
                return 2;
            }
            return 0;
        } catch (Exception exception) {
            Write(new { kind = "capture-failed", failureType = exception.GetType().FullName });
            return 1;
        }
    }

    private static string Value(string[] args, string key, string? defaultValue = null) {
        var index = Array.IndexOf(args, key);
        if (index >= 0 && index + 1 < args.Length) {
            return args[index + 1];
        }
        return defaultValue ?? throw new ArgumentException($"Missing option {key}.");
    }

    private static void Process(TraceEvent entry) {
        if (!string.Equals(entry.ProviderName, BridgeSource, StringComparison.Ordinal)) {
            return;
        }
        var sourceName = Payload(entry, "SourceName")?.ToString();
        if (sourceName != HttpSource && sourceName != FrameworkSource) {
            return;
        }
        var eventName = Payload(entry, "ActivityName")?.ToString() ?? Payload(entry, "EventName")?.ToString() ?? string.Empty;
        if (sourceName == HttpSource && eventName != "System.Net.Http.HttpRequestOut.Start" && eventName != "System.Net.Http.HttpRequestOut.Stop") {
            return;
        }
        var arguments = ParseArguments(Payload(entry, "Arguments"));
        var allowed = sourceName == HttpSource
            ? new HashSet<string>(["TraceId", "SpanId", "ParentSpanId", "Method", "StatusCode"], StringComparer.Ordinal)
            : new HashSet<string>(["TraceId", "SpanId", "ParentSpanId", "OperationName", "RunTags"], StringComparer.Ordinal);
        if (arguments.Keys.Any(key => !allowed.Contains(key))) {
            Interlocked.Increment(ref unexpectedArguments);
        }
        if (sourceName == HttpSource) {
            var start = eventName.EndsWith(".Start", StringComparison.Ordinal);
            if (start) {
                Interlocked.Increment(ref capturedHttpStarts);
            }
            Write(new { kind = start ? "http-send-start" : "http-send-stop",
                utc = entry.TimeStamp.ToUniversalTime(), relativeMilliseconds = entry.TimeStampRelativeMSec,
                traceId = Id(arguments, "TraceId", 32), spanId = Id(arguments, "SpanId", 16),
                parentSpanId = Id(arguments, "ParentSpanId", 16),
                method = SafeMethod(arguments.GetValueOrDefault("Method")),
                status = SafeStatus(arguments.GetValueOrDefault("StatusCode")) });
            return;
        }
        var runMatch = RunIdPattern.Match(arguments.GetValueOrDefault("RunTags") ?? string.Empty);
        if (!runMatch.Success || !Guid.TryParse(runMatch.Groups[1].Value, out var runId)) {
            return;
        }
        Interlocked.Increment(ref capturedRunMappings);
        Write(new { kind = "agent-run-trace", utc = entry.TimeStamp.ToUniversalTime(),
            relativeMilliseconds = entry.TimeStampRelativeMSec, runId,
            traceId = Id(arguments, "TraceId", 32), spanId = Id(arguments, "SpanId", 16),
            parentSpanId = Id(arguments, "ParentSpanId", 16) });
    }

    private static object? Payload(TraceEvent entry, string key) {
        return entry.PayloadNames.Contains(key, StringComparer.Ordinal) ? entry.PayloadByName(key) : null;
    }

    private static Dictionary<string, string> ParseArguments(object? value) {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (value is not IEnumerable values || value is string) {
            return result;
        }
        foreach (var item in values) {
            if (item is IDictionary<string, object> pair && pair.TryGetValue("Key", out var key)) {
                result[key.ToString() ?? string.Empty] = (pair.TryGetValue("Value", out var pairValue) ? pairValue?.ToString() : null) ?? string.Empty;
            } else if (item is IDictionary map && map.Contains("Key")) {
                result[map["Key"]?.ToString() ?? string.Empty] = map["Value"]?.ToString() ?? string.Empty;
            }
        }
        return result;
    }

    private static string? Id(IReadOnlyDictionary<string, string> values, string name, int length) {
        return values.TryGetValue(name, out var value) && value.Length == length && value.All(Uri.IsHexDigit) ? value : null;
    }

    private static string? SafeMethod(string? method) => method is "GET" or "POST" or "HEAD" or "PUT" or "DELETE" or "PATCH" or "OPTIONS" ? method : null;

    private static string? SafeStatus(string? status) => status is not null && (int.TryParse(status, out var number) && number is >= 100 and <= 599 || Enum.TryParse<HttpStatusCode>(status, out _)) ? status : null;

    private static void Write(object value) {
        lock (OutputGate) {
            Console.WriteLine(JsonSerializer.Serialize(value));
            Console.Out.Flush();
        }
    }

    private static async Task SelfCheckAsync() {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var responder = Task.Run(async () => {
            using var incoming = await listener.AcceptTcpClientAsync();
            await using var stream = incoming.GetStream();
            var buffer = new byte[4096];
            var read = await stream.ReadAsync(buffer);
            if (read == 0) {
                throw new IOException("Self-check client disconnected.");
            }
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"));
        });
        using var source = new ActivitySource(FrameworkSource);
        using (var activity = source.StartActivity("capture-self-check")) {
            activity?.SetTag("agentframework.execution_run_id", "93b40ac4f4e94bc188a4c8012a8f0440");
            activity?.SetTag("agentframework.model", "SENSITIVE_SELF_CHECK_MODEL");
            using var http = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{endpoint.Port}/SENSITIVE_SELF_CHECK_PATH?token=SENSITIVE_SELF_CHECK_QUERY");
            request.Headers.Add("X-Self-Check", "SENSITIVE_SELF_CHECK_HEADER");
            request.Content = new StringContent("SENSITIVE_SELF_CHECK_BODY");
            using var response = await http.SendAsync(request);
        }
        await responder;
    }
}


