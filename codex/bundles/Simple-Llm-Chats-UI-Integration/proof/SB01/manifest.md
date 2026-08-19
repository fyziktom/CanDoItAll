# Proof Manifest — SB01

- Start commit: `271ce22e3a62ddbb5b5c6129da79363a4b63ee81`.
- Candidate commit: `c744a485f` (`chore(bundle): preserve predecessor UI proof`).
- SharedInfo commit/hash: `7b7808e8591d7219f40826cf0e5624e182981d90`.
- Proof tier: `Standard`.
- Changed source paths and line ranges: none; SB01 changed `.gitignore` lines 20-23 and durable bundle proof only.
- Context-only paths: the six scoped product projects named in the current-state inventory plus the Conversations component characterization tests.
- CodeAnalytics impacted-test request/result: not applicable to the final production/test diff because it is empty. The service correctly rejected an empty change set; a discarded probe that treated `.gitignore` as code produced an invalid `AllSuppliedSuites` fallback and was not used for selection.
- Workspace health and discovery counts: Components workspace healthy, 113 projects and 922 source tests; the owning `CanDoItAll.Tests.Components.Conversations` namespace discovered exactly 25 runtime cases.
- Required selectors run: `FullyQualifiedName~CanDoItAll.Tests.Components.Conversations`; 25 passed, 0 failed, 0 skipped.
- Conditional selectors promoted/deferred and why: none; there is no product or test source change.
- Builds/static checks: filtered discovery built the Components workspace successfully; prepared bundle validation passed all five validators.
- Behavioral positive case: existing neutral conversation component characterization remains green.
- Negative/boundary case: the selected set includes blank presentation-key rejection and Markdown raw-HTML suppression.
- Browser viewport and scenario methods: not applicable; SB01 owns no visible change.
- Screenshot/open-overlay review: not applicable.
- Architecture snapshot/dependency/cycle result: `snap-20260816171034-d26d371e`; six projects, 965 types, 8,555 members, 87 service registrations, no blocking errors and no project cycle. Two pre-existing AgentFramework module/type cycles and non-blocking generated-type/DI-interpretation diagnostics were recorded without expanding scope.
- Secret/sensitive-content scan: 116 predecessor text artifacts scanned; zero credential-like suspect files.
- Acceptance result: pass.
- Reopen/invalidation result: reopen if any predecessor checksum fails, proof becomes ignored/missing, or a later Agent regression contradicts the baseline.
- Progression decision: CP0 passes; SB02 may start. Simple Chat UI activation remains locked.
- Artifact hashes: all 279 predecessor `CHECKSUMS.sha256` entries exist and match; recovered proof is 134 files / 4.91 MB.

