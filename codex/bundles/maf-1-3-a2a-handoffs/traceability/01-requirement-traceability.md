# Requirement Traceability

| Requirement | Subbundle | Validation |
|---|---|---|
| `REQ-01` | `01-maf-1-3-upgrade-contract` | Package refs show MAF 1.3; Core/Maf targeted build passes. |
| `REQ-02` | `02-default-model-and-provider-seeds` | No active `gpt-5-mini` default remains outside historical artifacts; seed/provider tests pass. |
| `REQ-03` | `03-a2a-agent-registry-and-hosting` | Typed A2A settings round-trip and deny invalid endpoint/auth/protocol cases. |
| `REQ-04` | `03-a2a-agent-registry-and-hosting` | Remote A2A agent wrapper or local stub test can run and expose skill tools. |
| `REQ-05` | `03-a2a-agent-registry-and-hosting` | Hosted endpoint mapping is explicit and disabled by default unless configured. |
| `REQ-06` | `04-handoff-workflow-runtime` | Handoff workflow test proves transfer and optional return-to-previous behavior. |
| `REQ-07` | `09-process-flow-integration` | Process dispatch/run test proves cooperation metadata affects runtime behavior. |
| `REQ-08` | `05-process-artifact-handoff-enforcement`, `09-process-flow-integration` | Downstream QA cannot proceed on missing implementation artifacts. |
| `REQ-09` | `06-tool-availability-profiles` | Role/tool profile tests prove dev/QA/business agents receive needed tools and non-roles do not. |
| `REQ-10` | `07-context-session-and-compaction-policy` | Tests or trace proof show governed process artifacts survive context/session handling. |
| `REQ-11` | `08-architecture-review-gate-1`, `10-architecture-review-gate-2`, `12-final-architecture-review-and-closure` | Review files explicitly approve closure with accepted residual risks; final review is recorded in `reviews/04-final-architecture-review-and-closure.md`. |
| `REQ-12` | `11-validation-and-operator-proof`, `12-final-architecture-review-and-closure` | Execution report captures restore/build/unit/integration/diff/bundle-validator outcomes; browser proof was not required because no visible UI changed. |
| `REQ-13` | `06-tool-availability-profiles`, `09-process-flow-integration`, `11-validation-and-operator-proof` | Live regression `NOTE-10` is closed by Maf runtime tests proving governed process software-development overrides attach scaffold/build/test/run tools and catalog `workspace-plugin` filters by effective runtime access. |
