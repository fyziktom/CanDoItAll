# Verification Host Beta Boundary

## Current alpha shape
`ProcessVerificationRuntimeHost` currently provides synchronous `Verify(ProcessVerificationHostRequest)` and records audit entries into `InMemoryProcessVerificationAuditStore`.

## Required beta shape
- `VerifyAsync(ProcessVerificationHostRequest, CancellationToken)`.
- Structured `ProcessVerificationHostResult` with accepted/denied/error states.
- Expected denials must not throw: unsupported lane, disabled lane, empty payload, invalid scope, mutation operation, invalid evidence.
- Unexpected programming errors may still throw in test/fail-fast paths but must be explicitly classified.
- Durable audit persistence must exist behind an interface and query service.
- Host options must be validated on startup.
- Host must expose health/status facts without running verification.

## Still denied
No host method may accept `object`, raw JSON without typed envelope, shell command, file path, HTTP URL to fetch, workspace path to write, finalizer command, transition request, retry request, or provider repair request.
