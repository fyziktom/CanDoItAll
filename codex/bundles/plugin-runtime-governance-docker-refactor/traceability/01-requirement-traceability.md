# Requirement Traceability

| Requirement | Primary subbundle | Supporting subbundles | Proof required |
| --- | --- | --- | --- |
| `R001` install/enable separate from grants | `SB02` | `SB04`, `SB05` | Unit and integration tests show installed/enabled plugin still denied without grants. |
| `R002` declarations separate from approvals | `SB02` | `SB01`, `SB05` | Manifest validation and grant evaluator tests. |
| `R003` strongly typed grants | `SB02` | `SB07` | Domain/persistence tests and architecture guardrails. |
| `R004` fail-fast denied capabilities | `SB02` | `SB05` | Denied proxy tests and workflow runtime denial tests. |
| `R005` trusted actor audit | `SB04` | `SB07` | API/application tests reject caller-supplied actor trust. |
| `R006` no raw shell/service exposure | `SB03` | `SB08` | Guardrail tests and code review. |
| `R007` generic host-tool capability | `SB03` | `SB06` | Recipe registry tests and sample Docker plugin proof. |
| `R008` explicit PowerShell grants | `SB03` | `SB04` | Denied-by-default tests and UI grant proof. |
| `R009` explicit Docker grants | `SB03` | `SB06` | Docker recipe grant tests. |
| `R010` Docker dangerous defaults denied | `SB03` | `SB06` | Argument validation tests for forbidden Docker options. |
| `R011` plugin-safe environment | `SB03` | `SB07` | Tests prove OpenAI and unrelated secrets are excluded. |
| `R012` plugin executor runnable only when valid | `SB05` | `SB02`, `SB04` | Workflow catalog and runtime tests. |
| `R013` workflow validation missing grants | `SB05` | `SB04` | Workflow editor browser proof and validation tests. |
| `R014` Docker logs summarized by LLM node | `SB06` | `SB05` | Sample workflow run proof. |
| `R015` bounded workflow/output payloads | `SB03` | `SB06`, `SB07` | Output cap, truncation, and artifact tests. |
| `R016` permission settings UI | `SB04` | `SB08` | Browser screenshots and assertions. |
| `R017` persisted connections | `SB04` | `SB07` | Integration tests for settings, health state, and concurrency. |
| `R018` focused grant/connection APIs | `SB04` | `SB02` | API tests and typed error tests. |
| `R019` UI validation proof | `SB04` | `SB05`, `SB08` | Execution report browser analytics rows. |
| `R020` EF projection and no N+1 | `SB07` | `SB04`, `SB05` | Query-shape review and integration tests. |
| `R021` no large logs in EF | `SB07` | `SB06` | Artifact storage proof and DB assertions. |
| `R022` workflow grant check performance | `SB07` | `SB05` | Run-scoped grant snapshot tests or measurement. |
| `R023` observability and redaction | `SB07` | `SB03`, `SB05`, `SB06` | Audit/receipt tests with redaction assertions. |
| `R024` final validation | `SB08` | all | Completed validator, tests, browser proof, and architecture review. |
| `R025` plugin API development control | `SB04` | `SB06`, `SB08` | API tests and Qdrant validation use plugin APIs instead of direct DB mutation. |
| `R026` Qdrant workflow proof | `SB06` | `SB03`, `SB04`, `SB05`, `SB08` | End-to-end workflow proof starts or verifies Qdrant via Docker plugin and reads logs. |
