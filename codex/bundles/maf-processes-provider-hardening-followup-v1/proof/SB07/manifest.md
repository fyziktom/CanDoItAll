# SB07 Proof Manifest

- Subbundle: `SB07`
- Status: `Completed`
- Owned requirements: `RQ-008`
- Raw notes: ProcessAgentRuntimeToolProvider must not remain a 900+ line monolith after moving process tools behind the runtime-provider seam.
- Semantic invariant contract: `bundle://proof/SB07/semantic-invariants.md`

## Changed File Hashes

- Representative SHA-256: 9D13CFE641631AF70168A124E71F7784ADE629F6ED6B338DEBAACEF32EA76BE2 repo://src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.cs
- Hash manifest: `bundle://proof/SB07/source-assertions/changed-file-hashes.txt`

## Command Transcripts

- Provider split inventory and exact-name parity source audit: `bundle://proof/SB07/transcripts/provider-split-inventory.txt`
- Unit provider split guard: `bundle://proof/SB07/transcripts/process-provider-unit-tests.txt`
- Runtime provider composition exact-name parity test: `bundle://proof/SB07/transcripts/process-runtime-provider-integration-tests.txt`
- Access denial test: `bundle://proof/SB07/transcripts/process-provider-access-denial-test.txt`
- Policy tests: `bundle://proof/SB07/transcripts/agent-tool-invocation-policy-tests.txt`
- Capability evaluator test: `bundle://proof/SB07/transcripts/agent-capability-evaluator-test.txt`
- Solution build: `bundle://proof/SB07/transcripts/solution-build.txt`
- Registration and guard scan: `bundle://proof/SB07/transcripts/process-provider-registration-and-guard-scan.txt`
- Anti-stub audit: `bundle://proof/SB07/transcripts/anti-stub-audit.txt`
- Adversarial process provider monolith scan: `bundle://proof/SB07/transcripts/adversarial-process-provider-monolith-scan.txt`

## Failing-First And Passing Proof

- Adversarial negative proof: `bundle://proof/SB07/transcripts/adversarial-process-provider-monolith-scan.txt` records a non-zero scan for the old single-file process provider shape.
- Passing: `bundle://proof/SB07/transcripts/process-provider-unit-tests.txt`, `bundle://proof/SB07/transcripts/process-runtime-provider-integration-tests.txt`, `bundle://proof/SB07/transcripts/process-provider-access-denial-test.txt`, `bundle://proof/SB07/transcripts/agent-tool-invocation-policy-tests.txt`, `bundle://proof/SB07/transcripts/agent-capability-evaluator-test.txt`, and `bundle://proof/SB07/transcripts/solution-build.txt`.

## Source Assertions

- Source assertions: `bundle://proof/SB07/source-assertions/process-provider-split-source-assertions.txt`
- Changed-file hashes: `bundle://proof/SB07/source-assertions/changed-file-hashes.txt`

## Anti-Stub Audit

- Anti-stub audit transcript: `bundle://proof/SB07/transcripts/anti-stub-audit.txt`

## Browser And Host Proof

- Browser proof: N/A; SB07 changed provider source/tests only and no rendered UI route changed.
- Host proof: N/A; no desktop or process-launch behavior changed.

## Downstream Smoke Proof

- `bundle://proof/SB07/transcripts/process-runtime-provider-integration-tests.txt` proves app composition still registers the Processes provider and exposes the exact 23-tool inventory.
- `bundle://proof/SB07/transcripts/process-provider-access-denial-test.txt` and `bundle://proof/SB07/transcripts/agent-tool-invocation-policy-tests.txt` prove access and approval-policy behavior did not weaken during the split.
