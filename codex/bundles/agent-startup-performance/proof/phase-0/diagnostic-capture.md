# Bounded dispatch timing capture

Only the scratch helper was compiled. Application code, binaries, logging levels, configuration, security, images and data were unchanged for this baseline. No raw `.nettrace`, HTTP URL, header, request/response body, prompt or credential was persisted.

The retained source is under `diagnostic-helper/`. The executable used on both operating systems is the same `StartupDispatchCapture.dll`; SHA-256 is `AFBC63722CB8D696EE3E667009254C1F8ABBB9A593C56E28433760F415D96952`. `diagnostic-helper/binary-sha256.json` identifies the complete dependency set. Keep the original ignored output under `.artifacts/agent-startup-performance/diagnostics/bin/Release/net10.0` for candidate capture; do not rebuild or change the filter between baseline and candidate.

The project references cached Microsoft.Diagnostics.NETCore.Client 0.2.510501 and Microsoft.Diagnostics.Tracing.TraceEvent 3.1.21 directly. It has no package references and required no tool installation. Build command, run before sampling:

```powershell
dotnet build .artifacts/agent-startup-performance/diagnostics/StartupDispatchCapture.csproj --configuration Release --no-restore -p:ImportDirectoryBuildProps=false -p:ImportDirectoryBuildTargets=false -p:NuGetAudit=false --verbosity minimal
```

Final helper build: zero warnings and zero errors. Each platform ran `dotnet StartupDispatchCapture.dll --self-check`. This self-check starts only an in-process loopback test endpoint, performs one helper-owned HTTP POST carrying synthetic URL/header/body sentinels, and checks that output has one POST start and a matching synthetic run ID, with no sentinel leakage or unexpected keys. It does not call either application or any model. See both `capture-self-check-*.jsonl` artifacts.

## Capture boundary and filtering

The EventPipe provider is `Microsoft-Diagnostics-DiagnosticSource`, Informational, keyword mask `0x803`, 16 MB circular buffer, and no rundown. Only these filter specifications are enabled:

```text
HttpHandlerDiagnosticListener/System.Net.Http.HttpRequestOut.Start:-TraceId=*Activity.TraceId;SpanId=*Activity.SpanId;ParentSpanId=*Activity.ParentSpanId;Method=Request.Method.Method
HttpHandlerDiagnosticListener/System.Net.Http.HttpRequestOut.Stop:-TraceId=*Activity.TraceId;SpanId=*Activity.SpanId;ParentSpanId=*Activity.ParentSpanId;StatusCode=Response.StatusCode
[AS]CanDoItAll.AgentFramework/Stop:-TraceId;SpanId;ParentSpanId;OperationName;RunTags=Tags.*Enumerate
```

The leading `-` disables default payload projection. HTTP output permits only timestamp/relative time, validated hexadecimal trace/span/parent IDs, an allowed method, and response status. Framework activity tags are decoded only in memory to find the GUID `agentframework.execution_run_id`; other tags and operation text are not serialized. The inspected framework activity source sets identifier/provider/model metadata, not prompts or credentials. Activity Stop is required because run tags are applied after activity start. Unexpected projected fields are counted; no broad event payload is dumped.

`http-send-start` is the .NET HTTP diagnostic send-start boundary. It is a real HTTP-send observation, distinct from the preceding application `Run` log or `provider.call` activity. It is not a packet-level socket/wire timestamp. `http-send-stop` measures response-header arrival, not the first assistant token. `agent-run-trace` supplies a run GUID only after its activity ends. Therefore a live incomplete sample may have a send row before its run association arrives.

The extractor requires an exact equality between the HTTP parent span and a framework run-mapped activity span, with the same trace ID. It rejects ambiguous associations. It does not use trace ID alone or guess from nearby timestamps. It records persisted phase timestamps separately; these log timestamps precede the awaited durable write and are not equivalent to the time the browser displayed the stage.

## Attach and stop

Native target PID58036 was attached by collector PID51868. The collector was started hidden using `Start-Process`, with stdout redirected to `native-http-capture.jsonl` and stderr to its separate log. Its command arguments were:

```text
StartupDispatchCapture.dll --pid 58036 --seconds 1800 --stop-file C:\repositories\CanDoItAll\.artifacts\agent-startup-performance\diagnostics\stop-native-phase0.signal
```

Client target PID7 was attached by collector PID3177 through its existing `/tmp/dotnet-diagnostic-7-8543158-socket`. The container already has a writable `/tmp` tmpfs while its root filesystem remains read-only. A direct `docker cp` attempt failed because of the read-only root. The helper files were instead transferred as a tar stream through `docker exec -i ... tar -xf - -C /tmp/agent-startup-diagnostics-20260831`, using only the existing authorized writable tmpfs and the container's existing user. No mounts, image or security flags were changed. The host `docker exec` process PID46772 was started hidden and streams sanitized JSONL directly to `client-http-capture.jsonl`; raw binary event data never crosses into that artifact.

```text
docker exec candoitall-shared-providers-manual-client-a-1 dotnet /tmp/agent-startup-diagnostics-20260831/StartupDispatchCapture.dll --pid 7 --seconds 1800 --stop-file /tmp/agent-startup-diagnostics-20260831.stop
```

Both ready records were received at approximately 2026-08-31T13:25:28Z, before the first measured send. The collectors stop after 1800 seconds or when their exact stop files are created. Explicit stop uses creation of the native stop file and `docker exec ... touch /tmp/agent-startup-diagnostics-20260831.stop`; it does not signal or terminate either application. Capture-stop status and final counts are recorded in each JSONL stream. Temporary helper files may remain under `/tmp` until their owned cleanup; their presence does not alter app settings.

For candidate capture, first identify the new actual target PIDs/socket, use this same helper and filter, unique candidate output/stop paths, and a fresh bounded capture. Do not reuse a stop file that already exists. Begin UI sends only after both ready records. No app builds, tests or synthetic benchmarks may run while sampling.

## Clocks and limitations

Event timestamps use each target host's UTC clock. HTTP/run event relative timestamps are from a single EventPipe session; do not mix relative clocks across processes. `clock-alignment.json` records three Windows-before/container-date/Windows-after brackets. The tightest round trip was229.2533 ms, yielding Docker minus Windows midpoint +6.0729 ms with conservative uncertainty ±114.62665 ms. Docker persisted-run to Docker HTTP-send elapsed time uses the same clock and avoids this cross-host uncertainty.

The helper's overhead has not been separately quantified. It enables a narrow low-volume EventPipe source and no stack, allocation or CPU sampling; identical instrumentation is required for both samples. HTTP diagnostics observe the client request to publisher on5214, not the publisher's later upstream dispatch. This is the intended boundary for measuring client preparation. Provider/relay time and first visible UI content remain separate measurements.

Both baseline collectors stopped cleanly at2026-08-31T13:41:04Z. Each recorded six HTTP starts, twelve run-activity mappings and zero unexpected arguments; both stderr files were empty. `capture-stop-verification.json` confirms no remaining collector host processes. All twelve HTTP starts have exact direct-parent run associations in the timing artifacts.

The isolated test PostgreSQL container was created only after sampling completed. See `isolated-test-postgres.json`. Tests must dot-source the private bootstrap in their own launcher process and use unique database leases. Stop/remove only this owned disposable test container after the isolated tests and before candidate live sampling, so it does not add background load absent from baseline. Do not stop any pre-existing database.

Reference for the runtime boundary: [.NET built-in HTTP activities](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-builtin-activities) and [DiagnosticSource EventSource filter implementation](https://source.dot.net/System.Diagnostics.DiagnosticSource/System/Diagnostics/DiagnosticSourceEventSource.cs.html). The precise filter and run association above are verified by the retained self-check and actual baseline events.
