# SB18 Semantic Invariants

## Invariants

- Invariant ID: `SB18-INV-001`
- Source raw note: RN15 - final proof harness, red-team, and release readiness must close before broader real process testing.
- Expected behavior: final readiness is based on the SB15 named proof suites with isolated output paths and focused filters.
- Disallowed shallow implementation: reusing a broad `ProcessWorkspaceTests` or full-project command that already timed out as final proof.
- Failing-first test: bundle://proof/SB18/transcripts/failing-first.txt records the rejected broad component timeout and stale pending-status rejection.
- Passing test: bundle://proof/SB18/transcripts/passing.txt records unit, integration, component, opt-in PostgreSQL, and browser proof.
- Changed source files: bundle://README.md, bundle://reviews/01-execution-report.md, bundle://proof/SB18/manifest.md, bundle://proof/SB18/semantic-invariants.md, bundle://scripts/validation-commands.md, repo://Templates/Processes/README.md, repo://codex/skills/candoitall-api-processes/SKILL.md, repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs, repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs, and repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs.
- Production assertions: source assertions prove final GO status, solved raw notes, source-aligned enum guidance, live-run profile API/tool parity, generic PostgreSQL proof, component proof, and SB18 artifact-backed proof references.
- Red-team negative case: bundle://proof/SB18/transcripts/failing-first.txt rejects broad timeout proof and any remaining pending or partially solved release-readiness status.
- Downstream dependency check: no downstream subbundle remains; bundle closure depends on completed-stage validation only.

## Additional Invariants

- Invariant ID: `SB18-INV-002`
- Source raw note: RN15 - all bundle closure state must agree.
- Expected behavior: root README, execution report, subbundle status, raw-note closure, browser analytics, and final verdict all agree that SB01-SB18 are complete.
- Disallowed shallow implementation: leaving `Pending`, `Partially solved`, or open final-red-team language in release-readiness fields.
- Failing-first test: bundle://proof/SB18/transcripts/failing-first.txt records the stale marker search.
- Passing test: bundle://proof/SB18/transcripts/closure-validator.txt records completed-stage bundle validation.
- Changed source files: bundle://README.md, bundle://reviews/01-execution-report.md, and bundle://subbundles/18-final-governance-red-team-and-release-readiness/README.md.
- Production assertions: all raw notes RN01-RN15 are solved and the final verdict is GO.
- Red-team negative case: any matching pending or partially solved marker fails the stale-closure scan.
- Downstream dependency check: bundle closure only proceeds after all SB01-SB17 prerequisites are complete.

- Invariant ID: `SB18-INV-003`
- Source raw note: RN15 - final UI readiness needs real browser proof for surfaces changed by earlier phases.
- Expected behavior: the hardened process operator console route renders in a real browser and has zero browser console errors during the final red-team pass.
- Disallowed shallow implementation: relying only on component tests or hiding errors behind UI copy.
- Failing-first test: bundle://proof/SB18/transcripts/anti-stub-audit.txt records broad fixture/profile matches separately so template/test data cannot be confused with production hardcoding.
- Passing test: bundle://proof/SB18/transcripts/passing.txt and bundle://proof/SB18/browser/operator-console-final-red-team.png record browser proof.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsOperatorConsoleSection.razor and repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs.
- Production assertions: the operator console evidence is runtime/browser-backed and not a prose-only claim.
- Red-team negative case: production source diff rejects template-specific shortcuts; fixture/template matches are classified separately in bundle://proof/SB18/transcripts/anti-stub-audit.txt.
- Downstream dependency check: no downstream UI subbundle remains.

## Production Behavior Artifact Matrix

| Artifact or signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Split final proof suites | bundle://scripts/validation-commands.md | SB18 final red-team and release report | Replaces timeout-prone broad closure with named unit, integration, component, live PostgreSQL, browser, and static proof slices | bundle://proof/SB18/transcripts/failing-first.txt records broad component timeout rejection. |
| Runtime/process readiness evidence | Unit, integration, component, and PostgreSQL test projects | bundle://reviews/01-execution-report.md and final GO verdict | Build each project with isolated output paths, run focused no-build proof commands, preserve pass counts and warnings | bundle://proof/SB18/transcripts/passing.txt records each command and exit code. |
| Browser-visible operator console evidence | Playwright Browser plugin against the local process management route | bundle://reviews/01-execution-report.md Browser Validation Analytics | Open hardened operator console, check browser errors, save screenshot artifact | bundle://proof/SB18/transcripts/passing.txt records zero browser console errors and bundle://proof/SB18/browser/operator-console-final-red-team.png. |
| Final bundle closure state | bundle://README.md and bundle://reviews/01-execution-report.md | Completed-stage bundle validator | All subbundles completed, raw notes solved, final verdict GO | bundle://proof/SB18/transcripts/failing-first.txt rejects remaining pending or partially solved closure markers. |

## Validation

- Failing-first/adversarial proof: bundle://proof/SB18/transcripts/failing-first.txt.
- Passing proof: bundle://proof/SB18/transcripts/passing.txt.
- Source assertions: bundle://proof/SB18/transcripts/source-assertions.txt.
- Anti-stub audit: bundle://proof/SB18/transcripts/anti-stub-audit.txt.
- Changed-file hashes: bundle://proof/SB18/transcripts/changed-file-hashes.txt.
- Closure validator: bundle://proof/SB18/transcripts/closure-validator.txt.
