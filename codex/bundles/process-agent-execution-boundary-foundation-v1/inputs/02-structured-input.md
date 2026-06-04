# Structured Input

## Request Summary

- Review the completed provider-hardening follow-up branch.
- Decide whether full Process Core separation should start now or whether smaller preparation is needed first.
- Prepare and execute a multi-phase bundle with refactor checkpoints.
- Avoid small, medium, and mobile UI proof because this work targets PC and large-screen usage.

## Branch Review Summary

- Previous MAF/product-tool provider decoupling is present and must be preserved.
- `ProcessRunAutomationDispatchService` still directly references AgentFramework execution services, models, and failure cases.
- Full Process Core extraction remains too risky until AgentFramework execution coupling is isolated behind a process-owned boundary.

## Execution Decision

Start the process agent execution boundary foundation. Do not start full Process Core extraction in this bundle.
