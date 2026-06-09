# SB010 Proof Manifest

## Status
Passed.

## Changed Files
| Path | SHA256 | Notes |
| --- | --- | --- |
| `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs` | `B647BDF3682AB939B8452E42007ABD658E0F70562F26B7FC65B1CAB120990815` | New project-scoped browser proof for `/projects/{projectId}/processes`. |

## Commands
| Command | Result | Transcript |
| --- | --- | --- |
| `dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter FullyQualifiedName~Project_scoped_process_workspace_SB010_INV_001_preserves_project_and_launch_plan_context --logger "trx;LogFileName=SB010-project-scoped-process-launch.trx" --results-directory codex\bundles\process-runtime-live-e2e-openai-hardening-v1\proof\SB010\test-results` | Passed 1 test | `bundle://proof/SB010/transcripts/project-scoped-process-launch-playwright.txt` |

## Proof Artifacts
| Artifact | SHA256 | Bytes |
| --- | --- | ---: |
| `bundle://proof/SB010/project-scoped-process-launch-proof.md` | `81949E9C26E20C0E892CDE7A3A707EFBE784760954E52095AC2225E8E7D2660B` | 2516 |
| `bundle://proof/SB010/transcripts/project-scoped-process-launch-playwright.txt` | `7F2BE5D413184F5F3708CB843F03DCA7FB401F33D0367D03830CCF32CEA765C9` | 9417 |
| `bundle://proof/SB010/test-results/SB010-project-scoped-process-launch.trx` | `B70AF334E0E8D77EC03552590D5446C2FABC08C4C82455CE1FB8D4E2A51F1222` | 3164 |
| `bundle://proof/SB010/transcripts/project-scoped-process-launch-source-assertions.txt` | `365189FE26B47C9C94192C8860DDB4B9F7BBFBEE5BE32345DDF65C66E4985353` | 3396 |
| `bundle://proof/SB010/transcripts/anti-stub-and-runtime-host-drift-scan.txt` | `D8890E17267160436191BEB89D6B8F59CCC90A4FD2069BE1364AA0224DE9007D` | 340 |
| `bundle://proof/SB010/transcripts/no-transient-bundle-path-scan.txt` | `6F3B9A86FE52C2B8FD26BBF90CFC91C30BB3F3BE9A533822A4E88D6A259A5736` | 298 |
| `bundle://proof/SB010/transcripts/no-unexpected-ui-media-drift-scan.txt` | `28A7A9D588731C310FE33342C4A83F9191616BEB187CAD97686F49A8BED1A623` | 1541 |
| `bundle://proof/SB010/transcripts/prepared-validator-after-sb010.txt` | `ABE977F36135D7C5B6086B2C468B2F8C8BB0B1A21494A91E59F706E073015672` | 125 |
| `bundle://proof/SB010/screenshots/01-project-template-selected-large-desktop.png` | `1634A8E3356F0FBB9117EEA09BF6B93DC6A3FAA860BE7E99A38F5E1804DFD217` | 265048 |
| `bundle://proof/SB010/screenshots/02-project-launch-plan-created-large-desktop.png` | `FA955C3CAD0DD3580B4C06920EEF2D3BB9251762F09046E2EE3A9B632313433B` | 292319 |
| `bundle://proof/SB010/screenshots/03-project-launch-plan-query-large-desktop.png` | `3246DBDD55057608AB87C9D0E4403EE15C0DF072E7973DDD24787DBF0AC8AA4F` | 285803 |

## Source Assertions
- `repo://src/CanDoItAll.Modules.Processes/Pages/ProjectProcessesPage.razor` maps `/projects/{ProjectId:guid}/processes` to `ProcessWorkspace ProjectId="@ProjectId"`.
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.TemplateLibrary.cs` passes `ProjectId` to `CreateProcessImportEnvelope`.
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Launch.cs` passes `ProjectId` into `ProcessLaunchCreateRequest` and binds `launchPlanId` from query string.
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Loading.cs` loads launch plans with `ListLaunchPlansAsync(selectedProcessId, ProjectId, cancellationToken)`.
- `repo://src/CanDoItAll.Web/Api/ProcessesApi.cs` exposes project-filtered definitions and launch-plan list endpoints.

## Production Behavior Artifact Matrix
| Behavior | Producer | Consumer | Lifecycle | Evidence |
| --- | --- | --- | --- | --- |
| Project-scoped process definition import | `ProcessWorkspace.TemplateLibrary.cs` | `ProcessesService.ImportAsync` and definitions API | UI imports template from project workspace, persisted definition carries `ProjectId`, API readback filters by project | `project-scoped-process-launch-source-assertions.txt`, `project-scoped-process-launch-playwright.txt` |
| Project-scoped launch-plan creation | `ProcessWorkspace.Launch.cs` | `ProcessesService.CreateLaunchPlanAsync` and launch-plan API | UI creates draft launch plan from project workspace, persisted launch plan carries `ProjectId`, query route reload selects it | `project-scoped-process-launch-playwright.txt`, screenshots 02 and 03 |
