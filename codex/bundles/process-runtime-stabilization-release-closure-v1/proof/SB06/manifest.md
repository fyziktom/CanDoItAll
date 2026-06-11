# SB06 Proof Manifest

- Subbundle: `SB06`
- Status: `Completed`
- Owned requirements: `REQ-007`, `REQ-008`
- Raw notes: make the final stabilization decision before further Process Core extraction.
- Semantic invariant contract: `bundle://proof/SB06/semantic-invariants.md`
- Release decision: `bundle://reviews/02-release-decision.md`
- Bundle start SHA: `430496c5e7217a847e9172dcc0c2fba57f75f75c`

## Changed File Hashes

SB06 is release-decision-only. It adds no new production `src` changes and no new test-code changes beyond SB01-SB05. The hashed SB06 artifacts below are the release-decision evidence.

| Path | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `bundle://reviews/02-release-decision.md` | `N/A (new)` | `c090faa97e4bf79f7a04e5e63d74f61f93ea58a4215364142fa7ef7935a9582c` |
| `bundle://proof/SB06/transcripts/build.txt` | `N/A (new)` | `a162aaa27d8eb4cd938df6a657cf47bff198023176d4bed2ac08f2104bbf71fe` |
| `bundle://proof/SB06/transcripts/unit-tests-rerun.txt` | `N/A (new)` | `1455e052d421423e9324c6f34ed6a3ae44d76db33ed4a54e6a9296fcac11f05c` |
| `bundle://proof/SB06/transcripts/focused-integration-matrix.txt` | `N/A (new)` | `e5c52b3bb80aa668851df3ea2ac0bdb7c7922ec48143430b0b62849a216c7b49` |
| `bundle://proof/SB06/transcripts/focused-playwright-final.txt` | `N/A (new)` | `d864109a93e07f3c9e706ac067f64b5857da81402b97dbc8dd4dde6f9724e5da` |
| `bundle://proof/SB06/transcripts/live-openai-classification.txt` | `N/A (new)` | `8d1f94e27db2e0be863e95652b21cca937054d116226ae301c83301eaf1316f2` |
| `bundle://proof/SB06/transcripts/code-first-ratio.txt` | `N/A (new)` | `52cf994e07c7b1c68c210ebfb2c7d8611345df4f07f42ebe6cf4bb5266c8a22a` |
| `bundle://proof/SB06/transcripts/screenshot-inventory.txt` | `N/A (new)` | `0dcb29121a93f8ff647ad7c1bc188bfe35d94c1b973b2407b235628f5f788216` |
| `bundle://proof/SB06/transcripts/source-assertions.txt` | `N/A (new)` | `1473aeddb7353b462854c5ac66e0aaddd43918d6636bd525dd763d22704907e5` |
| `bundle://proof/SB06/transcripts/anti-stub-audit.txt` | `N/A (new)` | `a2ba7916d7d8b1a677f7cc2666c196520652a762106565d5c9f9e007af2579eb` |
| `bundle://proof/SB06/transcripts/boundary-scan.txt` | `N/A (new)` | `728bd063a78dd6a5bcd2bcb9313586e69d86fe1e742ac4e5161f40757411c542` |
| `bundle://proof/SB06/transcripts/red-team-verifier.txt` | `N/A (new)` | `36597ee2aac6cbbb07db58248b24be1db63dc7f8a2a13030be6f40cfc02e786c` |
| `bundle://proof/SB06/transcripts/prepared-validator-final.txt` | `N/A (new)` | `51263c644c2d05527172836addbed856c28aa6b1cbb01f67e917ec876f9ec7c5` |
| `bundle://proof/SB06/transcripts/completed-validator-final.txt` | `N/A (new)` | `395b85a7d25f5d77d069185fe4ab3b0b10ea4b1be39dd57ab692950af4854ad4` |

## Command Transcripts

- Build transcript: `bundle://proof/SB06/transcripts/build.txt`
- Initial unit run with cleanup-only PostgreSQL failure: `bundle://proof/SB06/transcripts/unit-tests.txt`
- Failed-test rerun transcript: `bundle://proof/SB06/transcripts/unit-rerun-failed-test.txt`
- Clean full unit rerun transcript: `bundle://proof/SB06/transcripts/unit-tests-rerun.txt`
- Focused integration matrix transcript: `bundle://proof/SB06/transcripts/focused-integration-matrix.txt`
- Final Playwright proof transcript: `bundle://proof/SB06/transcripts/focused-playwright-final.txt`
- Live OpenAI classification transcript: `bundle://proof/SB06/transcripts/live-openai-classification.txt`
- Live OpenAI settings guard transcript: `bundle://proof/SB06/transcripts/live-openai-settings-tests.txt`
- Screenshot inventory: `bundle://proof/SB06/transcripts/screenshot-inventory.txt`
- Code-first ratio transcript: `bundle://proof/SB06/transcripts/code-first-ratio.txt`
- Source assertion transcript: `bundle://proof/SB06/transcripts/source-assertions.txt`
- Boundary scan transcript: `bundle://proof/SB06/transcripts/boundary-scan.txt`
- Anti-stub audit transcript: `bundle://proof/SB06/transcripts/anti-stub-audit.txt`
- Red-team verifier transcript: `bundle://proof/SB06/transcripts/red-team-verifier.txt`
- Prepared-stage final validator transcript: `bundle://proof/SB06/transcripts/prepared-validator-final.txt`
- Completed-stage final validator transcript: `bundle://proof/SB06/transcripts/completed-validator-final.txt`

## Semantic Adequacy

- Invariant ID: `SB06_INV_001`
- Invariant ID: `SB06_INV_002`
- Invariant ID: `SB06_INV_003`
- Invariant ID: `SB06_INV_004`
- Shallow-pass trap: a final closure could cite green runtime tests while ignoring live-smoke opt-in absence or the explicit code-first ratio gate.
- Adversarial negative proof: `bundle://proof/SB06/transcripts/red-team-verifier.txt` rejects merge-ready closure because `RatioPass: False`, rejects counting live OpenAI proof because explicit opt-in variables are absent, and confirms the deterministic matrix remains green.
- Semantic positive proof: the build, unit rerun, focused integration matrix, and Playwright launch-to-completion transcripts are all green.
- Source assertion proof: `bundle://proof/SB06/transcripts/source-assertions.txt` verifies required prior manifests, SB06 transcripts, release-decision markers, code-first ratio markers, live-skip markers, and screenshot inventory.
- Boundary proof: `bundle://proof/SB06/transcripts/boundary-scan.txt` verifies SB06 adds no production `src` changes and leaves process-owned runtime boundary proof in SB02-SB05.
- Anti-stub audit: `bundle://proof/SB06/transcripts/anti-stub-audit.txt` reports no TODO, HACK, NotImplemented, stub, or fake-pass markers in added `src`/`tests` lines.

## Production Behavior Artifact Matrix

| Signal or record | Producer | Consumer | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Build result | `dotnet build CanDoItAll.slnx --configuration Debug --no-restore` | Release decision | Build transcript reports successful build with 0 warnings and 0 errors. | Source assertion fails if the transcript lacks the 0-warning/0-error markers. |
| Unit-suite status | `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Debug --no-restore --no-build` | Release decision | Clean rerun transcript reports 1142 total and 1142 passed. The initial cleanup-only PostgreSQL permission failure is preserved in its original transcript and rerun proof. | Source assertion fails if the cleanup failure is hidden or if the clean rerun does not show 1142/1142. |
| Focused runtime matrix | Integration tests across SB01-SB05 runtime proof | Release decision | Focused integration transcript reports 21 total and 21 passed, including code-first guards, representative templates, runtime-host readback, and scheduler/workflow lifecycle. | Red-team verifier rejects manual-transition-only claims and requires the focused runtime matrix transcript. |
| Browser launch-to-completion proof | Playwright large-desktop project/project-structure process launch | Release decision and browser analytics | Final Playwright transcript reports the project-structure launch-to-completed-run proof passed; screenshot inventory hashes eight large-desktop screenshots. | Red-team verifier rejects UI-only claims without completed-run proof and screenshot inventory. |
| Live OpenAI classification | Environment-variable gate and live settings guard tests | Release decision | Classification transcript reports the API key is present but explicit opt-in/model/timeout/token-budget variables are absent, so live smoke is skipped and not counted. Settings guard transcript reports 7/7 passed. | Red-team verifier rejects counting skipped live smoke as deterministic proof. |
| Code-first ratio gate | Explicit bundle-start diff and untracked worktree scan | Release decision | Ratio transcript reports `SourceAndTestChangedLines: 652`, `BundleChangedLines: 3668`, required 18340, and `RatioPass: False`. | Red-team verifier rejects merge-ready closure while `RatioPass: False`. |

## Closure Decision

- Entry gate: Passed because SB05 scheduler/workflow lifecycle proof completed.
- Closure gate: Passed for an evidence-backed final release decision.
- Release decision: `Not merge-ready`.
- Progression decision: Do not proceed to merge as-is. Deterministic runtime stabilization proof is green, but the bundle's code-first ratio gate remains failed.
