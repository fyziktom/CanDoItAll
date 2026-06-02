# Normalized Requirements

| ID | Requirement | Priority | Owner |
| --- | --- | --- | --- |
| R01 | Governed live process steps must fail closed when operation contract is missing, invalid, or incompatible with target scope. | P0 | SB01 |
| R02 | All shipped process templates must declare explicit operation contracts or pass a deterministic migration/lint gate before publish. | P0 | SB01 |
| R03 | Every known tool name must be represented in one canonical registry with classification, operation requirement, approval default, side-effect model, and proof requirement. | P0 | SB02 |
| R04 | Unknown tools must not silently become `Read`; only explicitly registered read-only tools may be read-only. | P0 | SB02 |
| R05 | Provider usage observations must normalize input, cached input, output, reasoning, total, provider response id, source phase, status, and raw usage payload. | P0 | SB03 |
| R06 | Process/run/workflow cost must be derived from usage observations when observations exist; legacy metric reads must be fallback-only and labeled. | P0 | SB03 |
| R07 | OpenAI billing mismatch must be validated with a reconciliation report from provider response IDs and/or usage export/API evidence. | P1 | SB03 |
| R08 | Five domain-distinct app-generation tests must run through active process automation dispatch, with non-empty agent execution runs and tool receipts. | P0 | SB04 |
| R09 | The E2E proof harness must not generate app code itself when claiming process/agent app generation. | P0 | SB04, SB05 |
| R10 | Critical proof validators must fail manual-transition-only, no-provider-run, harness-generated-code, stale lineage, and count-only proof. | P0 | SB05 |
| R11 | Process dispatch and tool policy code must be decomposed into cohesive services after behavior gates are in place. | P1 | SB06 |
| R12 | Agents, templates, skills, and active skill root must be synchronized with strict operation/tool/proof requirements. | P1 | SB07 |
| R13 | UI must distinguish known cost, estimated cost, unknown usage, zero cost, and contract-blocked state. | P1 | SB08 |
| R14 | Final closure must include a senior QA red-team report that tries to break the proof, not just confirm it exists. | P0 | SB09 |
