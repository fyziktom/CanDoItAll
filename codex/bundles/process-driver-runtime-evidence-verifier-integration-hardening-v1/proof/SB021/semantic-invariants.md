# SB021 Semantic Invariants

- Invariant ID: SB021_INV_001
- Source raw note: Keep Core descriptor consumers allow-listed
- Expected behavior: Runtime evidence package consumes Core descriptors from a separate driver package and source scan proves Core has no reverse driver dependency.
- Disallowed shallow implementation: Returning a success flag, status row, or template text without enforcing the read-only evidence boundary and without command/source proof is not enough.
- Failing-first test: N/A process non-production compatibility closure
- Passing test: bundle://proof/SB018/transcripts/focused-runtime-evidence-consistency-tests-after-uri-policy-fix.txt
- Changed source files: bundle://proof/SB045/changed-file-hashes.md
- Production assertions: repo://src/CanDoItAll.Processes.Drivers.RuntimeEvidence/CanDoItAll.Processes.Drivers.RuntimeEvidence.csproj and repo://src/CanDoItAll.Processes.Core; source audit bundle://proof/SB045/transcripts/source-boundary-and-anti-stub-audit-after-uri-policy.txt
- Red-team negative case: N/A process non-production compatibility closure; source audit confirms no runtime, DI, file/network, UI/media, TODO, or NotImplemented drift.
- Downstream dependency check: P07 downstream contract compatibility checked by SB024 and API-boundary proof.

## Notes
- Core consumer boundary closed with repo:// source references and bundle:// proof transcripts.
