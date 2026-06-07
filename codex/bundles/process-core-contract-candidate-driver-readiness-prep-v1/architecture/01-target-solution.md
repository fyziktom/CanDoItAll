# Target Solution

## Target Shape
- Keep all runtime code inside the existing process module for this bundle.
- Move only behavior-preserving DTO, adapter, query, projection, and pure-rule boundaries that make later extraction decisions easier.
- Make side effects explicit at application/infrastructure boundaries: EF, storage, workspace, AgentFramework, project-structure mutation, claim lifecycle, materialization, and transition writes must remain named application behavior.
- Keep future driver work as documentation and readiness scoring only.

## Explicit Non-Goals
- No new `CanDoItAll.Processes.Core` or equivalent Core project.
- No production driver interfaces, registries, DI registrations, tools, or runtime dispatch.
- No UI or mobile behavior changes.

## Evidence Required
- Critical gates must prove parity with focused tests, source assertions, anti-stub scans, no-Core/no-driver scans, and artifact-backed proof under `bundle://proof/SBxx/`.
- The final scorecard must state which areas are candidates for a later narrow Core extraction and which must remain application-local.
