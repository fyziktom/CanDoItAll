# Assumptions And Risks

## Assumptions

- Inactivity means no MCP tool call has started or completed within the configured timeout.
- Long-running tool calls, especially SSH Ops operations, must not be interrupted solely because they run longer than the idle timeout.
- Configuration should default to enabled idle shutdown for the two requested MCPs.

## Risks

- If activity is marked only at tool completion, a long SSH operation could be stopped mid-call. The implementation must track active operations separately from last activity.
- If the timeout is too short for SSH Ops, agents may reconnect frequently during multi-step remote work. SSH Ops should use a longer default than Components.
- If the shared host service hard-codes project behavior, future MCPs will inherit unsuitable lifecycle policy. Defaults belong in per-project options.

## Critical Path Risks

- The shared idle service is the critical foundation. If it mishandles active calls or cancellation, both MCPs can shut down incorrectly.
- Tool wrapper instrumentation is the only reliable common activity boundary visible in the current code.

## Validation Risks

- Process-level idle shutdown is timing-sensitive. Unit tests should use a fake clock or very short intervals instead of sleeping for real production durations.
- Full stdio protocol integration is outside this bundle unless the package exposes a practical in-proc test harness. Build plus service-level behavior is sufficient for this small lifecycle change.

## Reopen Triggers

- Reopen the subbundle if a tool call can still leave the process running indefinitely after the idle window.
- Reopen the subbundle if active SSH operations can be stopped while still running.
- Reopen the subbundle if either requested MCP does not bind idle shutdown settings from its own configuration.
