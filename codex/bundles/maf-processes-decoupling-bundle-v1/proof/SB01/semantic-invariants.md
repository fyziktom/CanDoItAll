# SB01 Semantic Invariants

## Invariant `SB01-INV-001`

- Invariant ID: SB01-INV-001

- Source raw note: MAF and Processes must be decoupled in small steps, and the implementation must not simplify or omit behavior.
- Expected behavior: Before refactoring starts, the bundle has durable proof of the exact MAF -> Processes coupling points, process tool inventory, affected test areas, and dispatcher out-of-scope boundary.
- Disallowed shallow implementation: A baseline that checks only a count, only string literals, or only the csproj reference could miss policy-constant tool names or hidden source coupling.
- Failing-first test and transcript: `bundle://proof/SB01/transcripts/source-coupling-grep.txt` is intentionally positive at baseline because SB05 must later make the same dependency guard fail for Processes references in MAF.
- Passing test and transcript: `bundle://proof/SB01/transcripts/process-tool-name-extract.txt` proves exact-name parity between the current source surface and `bundle://inventories/01-process-tool-parity-inventory.md`.
- Changed source files and hashes: No production source changed in SB01. Baseline hashes are recorded in `bundle://proof/SB01/source-assertions/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB01/transcripts/source-coupling-grep.txt`, `bundle://proof/SB01/transcripts/process-builder-grep.txt`, and `bundle://proof/SB01/transcripts/dispatcher-partial-inventory.txt`.
- Red-team negative case: The extraction resolves `AgentToolInvocationPolicyMetadata` constants, so a string-only shallow scan cannot pass while missing `processes_definition_role_add`, `processes_template_baseline_scenarios_list`, or `processes_template_live_run_profiles_list`.
- Downstream dependency check: SB02 can start because the exact current coupling points and 23-tool process surface are known; SB05/SB06 must cite this invariant when removing the dependency and proving parity.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative test |
| --- | --- | --- | --- | --- |
| N/A | SB01 introduces no production signal, state, record, or event. | N/A | N/A | N/A |
