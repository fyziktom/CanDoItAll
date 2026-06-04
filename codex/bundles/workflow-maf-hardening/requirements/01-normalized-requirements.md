# Normalized Requirements

| ID | Requirement | Acceptance Signal |
| --- | --- | --- |
| R01 | Perform repo-local audit of Agents/Workflows/Plugins on `processes-hardening`. | Inventory file lists workflow runtime, compiler, template, seed, plugin, UI, persistence, and test surfaces. |
| R02 | Establish MAF package baseline and upgrade decision. | `Microsoft.Agents.AI*` versions are documented; upgrade/no-upgrade has rationale and tests. |
| R03 | Preserve file-backed workflow template pack. | `Templates/Workflows/manifest.yaml` remains the seed entry point; no hard-coded replacement examples. |
| R04 | Harden workflow graph validation. | Invalid graphs fail before persistence/execution with useful diagnostics. |
| R05 | Provide one native MAF compiler/adapter boundary. | Repository workflow definition can build a native MAF workflow through a testable service. |
| R06 | Use typed workflow messages. | Runtime avoids raw `object`/unvalidated string payloads except at explicit serialization boundaries. |
| R07 | Align executors with MAF C# executor patterns. | Executor adapters use `Executor`/`[MessageHandler]`/`partial` where appropriate, with reset behavior for shared state. |
| R08 | Harden plugin executor registry and contracts. | Plugins expose descriptor, schemas, capabilities, policies, and deterministic fake test support. |
| R09 | Enforce permissions and human/tool approval. | Dangerous/external plugin execution is gated by policy and tests cover approval/rejection. |
| R10 | Enforce timeout, retry, cancellation, and artifact capture policies. | Executor policies are applied consistently and visible in tests/events. |
| R11 | Separate preview and durable production execution. | In-process preview is allowed only by policy; durable-required production fails clearly when unavailable. |
| R12 | Normalize MAF workflow events into CanDoItAll run records. | Events have stable run/node/executor IDs, redaction, artifact linkage, and tests. |
| R13 | Preserve managed seed safety. | Non-managed workflow definitions are never overwritten; seed version refresh is tested. |
| R14 | Update UI and docs only after contracts stabilize. | UI displays executor/runtime/approval state without duplicating runtime logic. |
| R15 | Complete build/test/browser proof. | Execution report contains commands, outcomes, and evidence paths. |
