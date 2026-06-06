# Structured Input

- Continue the `maf-processes-refactor` branch with module-local dispatcher isolation.
- Preserve all process automation behavior while reducing ownership in `ProcessRunAutomationDispatchService.Dispatch.cs`.
- Do not start Process Core or production driver APIs.
- Execute enough phased subbundles and critical gates that later Codex work cannot collapse the refactor into shallow wrappers.
- Keep UI, browser, mobile and viewport proof out of scope for this runtime/service refactor.
