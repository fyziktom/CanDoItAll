# SB08 final verifier and red-team audit

Status: Completed

## Checks

| Risk | Verification | Result |
| --- | --- | --- |
| MAF package/API baseline still old or ambiguous | `unit-targeted-regression.txt`; package/source assertions | Passed. MAF stable packages are on 1.8.0 and A2A preview packages are aligned. |
| Unreachable human nodes pause the whole graph | `unit-targeted-regression.txt`; `integration-targeted-regression.txt` | Passed. Tests cover execution-position-aware human input. |
| Approval-required executors can perform effects without approval | `unit-targeted-regression.txt`; `integration-targeted-regression.txt` | Passed. Approval denial/missing gate paths fail before executor effects. |
| Event payloads lose node/executor/request identity or leak raw large payloads | `unit-targeted-regression.txt`; `source-assertions-risky-invariants.txt` | Passed. Typed event envelopes and payload policy are present and tested. |
| Checkpoint metadata implies supported resume | `integration-targeted-regression.txt`; `source-assertions-risky-invariants.txt` | Passed. Metadata checkpoints are exposed with explicit resume unavailability. |
| Plugin observer order disables plugin audit logs | `integration-targeted-regression.txt` | Passed. Composite observer and plugin audit sink registration are tested. |
| Plugin manifest capabilities allow secret/network/host/external-write mismatch | `unit-targeted-regression.txt`; `integration-targeted-regression.txt` | Passed. Manifest validator rejects mismatches. |
| Default tests invoke live Gmail, Office365, or Docker effects | `integration-targeted-regression.txt` | Passed. Bundled plugin proof uses deterministic fake-mode preview paths. |
| DurableTask/AzureFunctions appear runnable without registration | `integration-targeted-regression.txt`; `component-targeted-regression.txt`; SB07 browser proof | Passed. Durable backends are planned/disabled and rejected on save/test-run/start. |
| Dynamic `BindAsExecutor` strategy remains undecided | `architecture/03-maf-executor-binding-decision.md`; `source-assertions-risky-invariants.txt` | Passed. Decision is explicit: use dynamic binding for graph-authored workflows; revisit generated executors only for static workflow families with proof. |

## Residual Follow-up Triggers

- Add a real durable backend before enabling durable production workflow policies.
- Add trusted checkpoint blob storage and compatibility validation before enabling resume.
- Add repository CI workflows if the project standard requires GitHub Actions in this repository.
- Revisit source-generated MAF executors only with benchmark or Native AOT evidence.
