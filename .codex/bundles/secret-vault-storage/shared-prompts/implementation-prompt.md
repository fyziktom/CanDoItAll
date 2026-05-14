# Implementation Prompt

Implement the current subbundle only.

- Read the root README, phase plan, traceability, and current subbundle README before editing.
- Keep raw secret values out of database metadata, agent configuration, workflow definition JSON, project-structure metadata, logs, activity records, and screenshots.
- Use the existing CanDoItAll architecture first: `Modules.Security` for the vault/catalog, AgentFramework models for agent/workflow settings, BaseLib components for UI controls.
- Use DPAPI `CurrentUser` only in the Windows vault; unsupported future providers must fail explicitly.
- Add focused tests next to existing unit/component patterns.
- Update `reviews/01-execution-report.md` with commands, proof, browser analytics, and gate rows before closing the subbundle.
