# SB039 Proof Manifest

## Status
Completed.

## Objective
Gate M: prove launch API compatibility.

## Owned Requirements And Notes
- Requirement IDs: REQ-001, REQ-002, REQ-003, REQ-004, REQ-015 launch API subset.
- Critical invariant contract: `bundle://proof/SB039/semantic-invariants.md`
- Downstream dependency: SB040-SB042 Process Core/domain boundary validation may start after launch API compatibility is source-backed.

## Changed File Hashes
| File | SHA-256 |
| --- | --- |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/reviews/01-execution-report.md` | `44a6653c900fdf09441d092ade903dc4a65290c7c5a7b0387ee8589472c3bbdd` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/subbundles/SB037/README.md` | `d461c3167c544543b985f486a89a93452eb3598fbea210dabc16ac467fb63865` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/subbundles/SB038/README.md` | `1e22cd2e8fb2129279f3c47f92660fa49b363dc3353ed1d2fd65fd534e6838aa` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/subbundles/SB039/README.md` | `4645c6f6e4f0f5873bd6d690cfcd649297067482d2ff52b9061ae45c3cce368e` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB037/api-launch-endpoints-compatibility-matrix.md` | `c9c86d57eb0729d66ca5cf831999a63d70bcaa8af147a7770c769949a94221f6` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB038/project-global-launch-plan-migration-guards-proof.md` | `cdca2e4821447f1cdc1e91bf23344de41bb943d2af4d0dbcd4639cc70a847776` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB039/transcripts/launch-api-compatibility-tests.txt` | `a2f8d45ff4d0692d6cf2ee2990a9f3d30eb8957cd19b161256bae8d18b98ae95` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB039/transcripts/source-assertions.txt` | `329dc54471587428a2ced23c0c597e57090d66b975e64289d4e94c001195aba2` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB039/transcripts/no-transient-bundle-path-scan.txt` | `deea5a0c065f7a6e8e0debfb1fec1adececab257a22bb0813fbc2312881191b1` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB039/transcripts/anti-stub-and-runtime-host-drift-scan.txt` | `ae338c9943c30443c98d56f687fde73cfcb9eb4b3346a0179df12d3e65ea830f` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB039/red-team/shallow-launch-api-proof-rejected.md` | `d75a4591197e3e2c07c34b39f245ccce3f8bf89e12f3148cc0cafa415dce528a` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB039/semantic-invariants.md` | `fe4ad62151c3e2d4858b082dc95711555a53cda35dab421af86c3af652d9f43b` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB039/SB039-launch-api-compatibility.trx` | `988fe87c40bc618ba534e49c592de596809ff70abfc2da7eae69f03dfe0486c3` |

## Command Transcripts
- Focused integration run: `bundle://proof/SB039/transcripts/launch-api-compatibility-tests.txt`
- Source assertions: `bundle://proof/SB039/transcripts/source-assertions.txt`
- No transient bundle-path scan: `bundle://proof/SB039/transcripts/no-transient-bundle-path-scan.txt`
- Anti-stub/runtime-host drift scan: `bundle://proof/SB039/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Red-team shallow launch proof rejection: `bundle://proof/SB039/red-team/shallow-launch-api-proof-rejected.md`

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Direct run start | `ProcessesService.StartRunAsync` | Process runtime/outbox | Creates durable run rows and dispatch outbox records | Rejects missing/unpublished definition |
| Launch-plan execution | `ProcessesService.ExecuteLaunchPlanAsync` | Process runtime | Delegates to `StartRunAsync` with `LaunchPlanId` | Rejects not-ready and duplicate execution |
| Project-structure launch plan | Project-structure process start API | Process launch workspace | Creates project-scoped launch plan and route with `launchPlanId` | Rejects lost bridge context |
| Project-structure execution | Project-structure process start API | Process runtime/project structure | Starts run and route with `runId`, preserving project/node context | Rejects definition-only route proof |
| Launch context migration | Launch planning service | Project/global process workspace | Infers/reuses project-structure launch plan when context is unambiguous | Rejects duplicate open plan for same context |
| Runtime status projection | Launch plan read model | Launch UI/API consumers | Surfaces generated runtime run failure | Rejects stale planning-only status |

## Closure
- Shallow-pass trap: A fake pass could cite launch UI/read models without proving actual start/execution and migration guards.
- Adversarial negative proof: `bundle://proof/SB039/red-team/shallow-launch-api-proof-rejected.md`
- Semantic positive proof: `bundle://proof/SB039/transcripts/launch-api-compatibility-tests.txt`
- Anti-stub audit: `bundle://proof/SB039/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Raw-note closure: Launch APIs remain compatible across direct, project, launch-plan, and project-structure contexts without runtime driver host drift.
