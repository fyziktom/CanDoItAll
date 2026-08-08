# Module-owned authority and effective policy context

## Source authority

`IAgentExecutionSourceAuthorityProvider` is a Core SPI. Implementations belong to the module that
publishes and understands the source kind. The canonical resolver consumes an injected ordered set and
owns only duplicate detection, unknown-source fallback, profile fencing, and authority record creation.

Expected ownership:

- project-structure: Workbench;
- projects portfolio: owning Projects/Workbench integration;
- processes and processes-live: Processes.

## Tool policy

A contributor-enriched policy context is part of the evaluation result, not a disposable local. The
caller must use that exact effective context for:

- recoverable-denial mapping;
- block/approval guard;
- telemetry and logs;
- policy signatures and diagnostics.

A governed process run is valid only when the effective context contains the exact audit process
run/step identity and required typed restrictions. Object-reference changes are not evidence of
enrichment.
