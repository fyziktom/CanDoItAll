# SB02 Semantic Invariants

## SB02_INV_INTERNAL_001

- Source raw note: internal class/service tools must have their own abstraction and implementation layer with mockable loading and call mechanisms.
- Expected behavior: an internal tool can be registered by typed `ImplementationKey`, resolved without raw string switches, invoked through `IInternalTool`, and exposed as a shared `CapabilityExposureDescriptor`.
- Disallowed shallow implementation: call tools through hardcoded MAF switch branches or return anonymous/dynamic output without capability identity and policy metadata.
- Failing-first proof: `bundle://proof/SB02/transcripts/failing-first-tool-implementation-contracts.txt`
- Passing proof: `bundle://proof/SB02/transcripts/passing-tool-implementation-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB02/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Tools.Abstractions/Tools.cs`, `repo://src/CanDoItAll.AgentFramework.Tools/Internal/InternalToolRegistry.cs`, `bundle://proof/SB02/transcripts/source-assertions.txt`
- Red-team negative case: resolving an unregistered implementation key throws a predictable `KeyNotFoundException`; the registry does not silently fabricate fallback tools.
- Downstream dependency check: SB08 can adapt MAF runtime calls to an injectable registry instead of keeping hidden runtime-tool-name switches.

## SB02_INV_EXTERNAL_001

- Source raw note: external process tools must have bounded generic calls with deterministic failure categories and masked diagnostics.
- Expected behavior: a non-zero process exit returns `ToolInvocationResult` failure with `ProcessExit`, executable context, exit code, correlation ID, bounded masked output, and repair hint.
- Disallowed shallow implementation: collapse external process failures into one generic setup error or leak raw tokens from stdout/stderr.
- Failing-first proof: `bundle://proof/SB02/transcripts/failing-first-tool-implementation-contracts.txt`
- Passing proof: `bundle://proof/SB02/transcripts/passing-tool-implementation-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB02/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Tools/External/ExternalProcessToolInvoker.cs`, `repo://src/CanDoItAll.AgentFramework.Tools/External/ToolDiagnostics.cs`, `bundle://proof/SB02/transcripts/source-assertions.txt`
- Red-team negative case: `SB02_INV_EXTERNAL_001` feeds stdout/stderr containing a secret and asserts the masked detail does not expose it.
- Downstream dependency check: SB10 setup-test UI/API can present actionable process diagnostics without inventing UI-specific error mapping.

## SB02_INV_EXTERNAL_002

- Source raw note: external HTTP tools must use bounded calls and preserve endpoint/status context without leaking headers or response secrets.
- Expected behavior: a non-2xx HTTP result returns `HttpStatus` with method/host/path, status code, masked headers/body, correlation ID, and repair hint.
- Disallowed shallow implementation: read unbounded response bodies, create per-call unmanaged HTTP clients, or show raw authorization headers.
- Failing-first proof: `bundle://proof/SB02/transcripts/failing-first-tool-implementation-contracts.txt`
- Passing proof: `bundle://proof/SB02/transcripts/passing-tool-implementation-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB02/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Tools/External/ExternalHttpToolInvoker.cs`, `repo://src/CanDoItAll.AgentFramework.Tools/External/ToolDiagnostics.cs`, `bundle://proof/SB02/transcripts/source-assertions.txt`
- Red-team negative case: `SB02_INV_EXTERNAL_002` provides an `Authorization` header and secret-bearing response body and asserts masked diagnostics.
- Downstream dependency check: SB10 setup testing can reuse the transport service and surface typed HTTP failure details.

## SB02_INV_EXTERNAL_003

- Source raw note: external tools must not execute arbitrary unbounded shell strings.
- Expected behavior: the process invoker rejects an executable name not present in the descriptor allow-list before starting the process runner.
- Disallowed shallow implementation: pass arbitrary command text to a shell and rely on caller validation.
- Failing-first proof: `bundle://proof/SB02/transcripts/failing-first-tool-implementation-contracts.txt`
- Passing proof: `bundle://proof/SB02/transcripts/passing-tool-implementation-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB02/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Tools/External/ExternalProcessToolInvoker.cs`, `bundle://proof/SB02/transcripts/source-assertions.txt`
- Red-team negative case: `SB02_INV_EXTERNAL_003` attempts `powershell.exe` while only `fake-audit.exe` is allowed and asserts the fake runner was not called.
- Downstream dependency check: SB06 template materialization and SB10 setup testing can fail predictably on command-policy violations.

## SB02_INV_EXTERNAL_004

- Source raw note: external process calls must be bounded by timeout and produce deterministic diagnostics.
- Expected behavior: process timeout maps to `CapabilityDiagnosticCategory.Timeout` with the configured timeout value and repair hint.
- Disallowed shallow implementation: rely only on caller cancellation or report timeout as generic cancellation/start failure.
- Failing-first proof: `bundle://proof/SB02/transcripts/failing-first-tool-implementation-contracts.txt`
- Passing proof: `bundle://proof/SB02/transcripts/passing-tool-implementation-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB02/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Tools/External/ExternalProcessToolInvoker.cs`, `bundle://proof/SB02/transcripts/source-assertions.txt`
- Red-team negative case: `SB02_INV_EXTERNAL_004` injects a timeout failure and asserts the typed timeout diagnostic, not cancellation.
- Downstream dependency check: SB05 hardening can verify timeout behavior before SB08 reconnects runtime execution.

## SB02_INV_EXTERNAL_005

- Source raw note: setup tests for external tools must preserve structured validation failures.
- Expected behavior: the setup-test service returns `CapabilitySetupTestResult` with the original `SchemaValidation` diagnostic and field path when an external tool returns JSON missing required schema properties.
- Disallowed shallow implementation: translate schema mismatch into a generic setup error or mark setup successful because the process exited with code zero.
- Failing-first proof: `bundle://proof/SB02/transcripts/failing-first-tool-implementation-contracts.txt`
- Passing proof: `bundle://proof/SB02/transcripts/passing-tool-implementation-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB02/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Tools/Setup/ToolSetupTestService.cs`, `repo://src/CanDoItAll.AgentFramework.Tools/External/ExternalProcessToolInvoker.cs`, `bundle://proof/SB02/transcripts/source-assertions.txt`
- Red-team negative case: `SB02_INV_EXTERNAL_005` returns `{"status":"missing ok"}` while the descriptor requires `ok`.
- Downstream dependency check: SB10 setup UI/API can display exact schema repair information.

## SB02_INV_EXTERNAL_006

- Source raw note: external HTTP calls must be bounded by timeout and produce deterministic diagnostics.
- Expected behavior: HTTP timeout maps to `CapabilityDiagnosticCategory.Timeout` with endpoint context, configured timeout, and repair hint.
- Disallowed shallow implementation: rely on `HttpClient.Timeout` exceptions with inconsistent categories or hide endpoint context.
- Failing-first proof: `bundle://proof/SB02/transcripts/failing-first-tool-implementation-contracts.txt`
- Passing proof: `bundle://proof/SB02/transcripts/passing-tool-implementation-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB02/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Tools/External/ExternalHttpToolInvoker.cs`, `bundle://proof/SB02/transcripts/source-assertions.txt`
- Red-team negative case: `SB02_INV_EXTERNAL_006` injects a timeout and asserts typed timeout, configured duration, and endpoint context.
- Downstream dependency check: SB05/SB10 can reason about process and HTTP timeout categories uniformly.

## SB02_INV_POLICY_001

- Source raw note: capability restrictions must apply to internal, external, and provider-native tools through the common access policy evaluator without tool-only suppressors.
- Expected behavior: descriptors for internal mutation, external process, and provider-native tools map into `CapabilityExposureDescriptor` and are denied by operation, tag, and runtime-tool-name selectors.
- Disallowed shallow implementation: keep separate filtering rules per tool family or compare raw tool-name strings inside the invoker.
- Failing-first proof: `bundle://proof/SB02/transcripts/failing-first-tool-implementation-contracts.txt`
- Passing proof: `bundle://proof/SB02/transcripts/passing-tool-implementation-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB02/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Tools/Descriptors/ToolExposureDescriptorFactory.cs`, `repo://src/CanDoItAll.AgentFramework.Tools/Descriptors/ToolDescriptorFactory.cs`, `bundle://proof/SB02/transcripts/source-assertions.txt`
- Red-team negative case: `SB02_INV_POLICY_001` denies all three descriptor families through the shared SB01 evaluator and expects no allowed capabilities.
- Downstream dependency check: SB08 can consume one effective capability set for tools instead of preserving MAF-specific hidden filters.

## SB02_INV_PARITY_001

- Source raw note: existing workspace, dotnet, browser, provider-native, finalizer, process, project-structure, and image-generation names and policy metadata must remain stable.
- Expected behavior: every existing `ToolCapabilityRegistry` metadata entry maps into a descriptor without runtime name drift, approval/side-effect drift, or missing operation classifications.
- Disallowed shallow implementation: define new tool names or metadata only for the happy-path examples and ignore the current catalog.
- Failing-first proof: `bundle://proof/SB02/transcripts/failing-first-tool-implementation-contracts.txt`
- Passing proof: `bundle://proof/SB02/transcripts/passing-tool-implementation-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB02/changed-file-hashes.txt`
- Production assertions: `repo://tests/CanDoItAll.Tests.Unit/ToolImplementationContractsTests.cs`, `repo://src/CanDoItAll.AgentFramework.Tools/Descriptors/ToolDescriptorFactory.cs`, `bundle://proof/SB02/transcripts/source-assertions.txt`
- Red-team negative case: the test collects all registry drift failures and fails if any existing metadata entry loses runtime name, side-effect, approval, or classification parity.
- Downstream dependency check: SB08 reconnection can preserve current process/workflow behavior while moving tool execution behind descriptors.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `ToolInvocationResult` | `repo://src/CanDoItAll.AgentFramework.Tools.Abstractions/Tools.cs`, `repo://src/CanDoItAll.AgentFramework.Tools/External/ExternalProcessToolInvoker.cs`, `repo://src/CanDoItAll.AgentFramework.Tools/External/ExternalHttpToolInvoker.cs` | `repo://tests/CanDoItAll.Tests.Unit/ToolImplementationContractsTests.cs` | `bundle://proof/SB02/transcripts/passing-tool-implementation-contracts.txt` | `bundle://proof/SB02/transcripts/failing-first-tool-implementation-contracts.txt` |
| `CapabilitySetupTestResult` | `repo://src/CanDoItAll.AgentFramework.Tools/Setup/ToolSetupTestService.cs` | `repo://tests/CanDoItAll.Tests.Unit/ToolImplementationContractsTests.cs` | `bundle://proof/SB02/transcripts/passing-tool-implementation-contracts.txt` | `SB02_INV_EXTERNAL_005` schema-mismatch case |
| `CapabilityExposureDescriptor` | `repo://src/CanDoItAll.AgentFramework.Tools/Descriptors/ToolExposureDescriptorFactory.cs` | `repo://tests/CanDoItAll.Tests.Unit/ToolImplementationContractsTests.cs` | `bundle://proof/SB02/transcripts/passing-tool-implementation-contracts.txt` | `SB02_INV_POLICY_001` shared-denial case |
| `CapabilityDiagnostic` | `repo://src/CanDoItAll.AgentFramework.Tools/External/ToolDiagnostics.cs` | `repo://tests/CanDoItAll.Tests.Unit/ToolImplementationContractsTests.cs` | `bundle://proof/SB02/transcripts/passing-tool-implementation-contracts.txt`, `bundle://proof/SB02/transcripts/static-performance-scan.txt` | `SB02_INV_EXTERNAL_001`, `SB02_INV_EXTERNAL_002`, `SB02_INV_EXTERNAL_003`, `SB02_INV_EXTERNAL_004`, `SB02_INV_EXTERNAL_006` |

## Anti-Stub Audit

- `bundle://proof/SB02/transcripts/anti-stub-audit.txt`
