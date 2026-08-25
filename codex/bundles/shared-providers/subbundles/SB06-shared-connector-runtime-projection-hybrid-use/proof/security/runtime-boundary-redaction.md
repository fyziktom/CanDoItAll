# SB06 runtime boundary, redaction, and cancellation

State: `PASS` for implementation, focused proof, and final independent security re-audit; no P1/P2
blocker remains.

## Typed disclosure boundary

`ProviderFailureDisclosurePolicy` classifies a source-managed profile only by
`ProviderCredentialPurpose.SourceAccessToken`; connector-key string matching is not used as the
security decision. It defines typed health/runtime operations and deterministic public messages:

- source-managed health succeeded;
- source-managed health failed;
- source-managed request failed.

`ProviderFailureBoundaryException` retains the provider ID and operation as typed properties for
internal handling but deliberately has no raw inner exception and exposes only the safe message.
Personal providers bypass this sanitizer and retain their existing detailed diagnostics.

## Boundary coverage

The disclosure policy is applied at the raw OpenAI driver health boundary, Workspace catalog
health/test-chat boundary, generic provider runtime completion boundary, MAF streaming transport
boundary, module runtime gateway, and workflow failure diagnostic mapping. The module gateway writes
the selected safe message to activity on source-managed failures. Successful/failing shared health
does not rewrite the source-managed profile with raw probe details.

The source-token credential resolver uses `source access credential` as its diagnostic source and
returns generic unavailable/could-not-resolve text. It does not format the secret-record GUID or
exception type for a source-managed binding. Personal secret-record behavior is unchanged.

## Focused negative proof

`ConcreteProviderDriverTests` passes 54/54. Its existing health fact now injects a private base URI,
secret GUID, credential marker, prompt marker, and raw `HttpRequestException` text. The returned
source-managed health result is the deterministic safe failure and contains none of those markers;
the ordinary personal-provider health result still contains its actionable upstream detail.

`SharedProviderRuntimeProjectionIntegrationTests` passes 16/16. Its disconnected deterministic
runtime fact verifies the exposed failure includes only the safe runtime message and excludes the
private base URL, source token, and prompt marker.

`MafProviderTransportBoundaryChatClientTests` passes 13/13. The source-managed transport fact wraps
the raw cause in the typed disclosure boundary and verifies the public message omits provider ID,
model, and raw cause. The lane also covers non-streaming and streaming failure, enumerator advance,
disposal, idle/absolute watchdogs, dispatch serialization, and a non-cooperative timed-out transport.

`MafWorkflowExecutorFailureDiagnosticsTests` passes 4/4. The source-managed diagnostic retains the
safe runtime message while serialized detail excludes provider ID, routing model, and raw cause.

## Access-context containment

The transient handler depends only on singleton `IHttpContextAccessor`. On each outbound request it
resolves the scoped accessor through the current request services, adds the canonical header only to
that `HttpRequestMessage`, and preserves an existing header. It does not capture scoped state and
does not mutate cached client default headers.

The frozen runtime integration lane sends context A, context B, then no context through the same
cached client. The server observes the three independent values without leakage. Background
execution with no active HTTP context sends no context header.

## Cancellation semantics

Caller-requested cancellation is caught and rethrown before disclosure handling in the module
gateway. The production-DI fact in the 16/16 lane verifies canceled health and send calls remain
`OperationCanceledException`. The 13/13 MAF boundary lane separately verifies requested cancellation
is not reclassified, including disposal, while internal timeout/cancellation remains a typed
provider transport failure.

## Audio denial

Source-managed audio is classified by the same typed source-token binding, not connector text.
Speech-to-text and text-to-speech entry points invoke `ProviderAudioCapabilityPolicy` before
credential resolution or HTTP dispatch. Both operations throw `ProviderAudioCapabilityException`
with deterministic safe public text, zero outbound requests, and typed provider/operation properties.

The voice selector excludes source-managed profiles. An explicit persisted shared voice ID resolves
to empty instead of silently falling back to a personal provider; personal voice providers remain
eligible and their existing STT/TTS behavior is unchanged. Post-repair lanes pass 54/54 concrete
drivers, 16/16 feature/voice policy, and 29/29 agent voice regression.

## Data excluded from public/activity failure surfaces

- secret values and authorization values;
- source-token secret-record GUIDs;
- private base URI, host, port, and raw network text;
- prompt/request content and response body;
- provider GUID and routing model in sanitized exception text;
- raw inner exceptions for source-managed boundary failures.

No live-provider, paid-provider, broad, browser, Playwright, or multi-instance lane ran. Docker/log
scans remain SB07/SB12-owned.
