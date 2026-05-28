# SB11 Semantic Invariants

## Invariants

- Invariant ID: SB11-INV-001
- Source raw note: RN11 - Keep API DTO/read-model parity current after runtime/template changes.
- Expected behavior: Live-run profile API summaries expose the typed `FreshRunPolicy` that SB09 added to live-run profiles.
- Disallowed shallow implementation: Updating skill text only, returning counts without policy detail, using ad hoc JSON strings, or hardcoding a Blazor-only route response.
- Failing-first test: bundle://proof/SB11/transcripts/failing-first.txt proves the stale summary DTO shape without `FreshRunPolicy` is absent.
- Passing test: bundle://proof/SB11/transcripts/passing.txt proves `Api_live_run_profiles_expose_fresh_run_policy_contract` passes.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackScenarios.cs; repo://src/CanDoItAll.Web/Api/ProcessesApi.cs; repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs.
- Production assertions: `GET /api/processes/templates/live-run-profiles` returns a typed `ProcessTemplateLiveRunProfileSummary` with `FreshRunPolicy.RequiresFreshRun`, seeded-transition/artifact rejection, pre-dispatch checks, evidence checks, and writeback guidance.
- Red-team negative case: A caller cannot read live-run profile summaries and miss the fresh-run policy boundary while preparing a current-run process launch.
- Downstream dependency check: SB12 and SB17 can document process API and template parity using typed API behavior instead of prose-only template assumptions.

- Invariant ID: SB11-INV-002
- Source raw note: RN11 - Keep process tools aligned with HTTP API and runtime models.
- Expected behavior: MAF internal process tools include a governed read tool for live-run profiles, and the tool policy classifies it as read-only without approval.
- Disallowed shallow implementation: Documenting a tool that is not composed at runtime, exposing the profile through an unclassified process tool, or requiring mutation approval for read-only template profile inspection.
- Failing-first test: bundle://proof/SB11/transcripts/failing-first.txt proves the old no-policy summary shape is absent before the new tool can be trusted as parity surface.
- Passing test: bundle://proof/SB11/transcripts/passing.txt proves MAF process tool composition and `AgentToolInvocationPolicyTests` pass with `processes_template_live_run_profiles_list`.
- Changed source files: repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs; repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProcessTools.cs; repo://tests/CanDoItAll.Tests.Integration/MafAgentRuntimeTests.cs; repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs.
- Production assertions: `ProcessesTemplateLiveRunProfilesList` is a registered policy metadata constant, classified as `Read`, and composed by `MafAgentRuntime.ProcessToolBuilder` when process services are available.
- Red-team negative case: Agents cannot rely on prompt memory or template JSON scraping to discover live-run profile policy; the governed process tool exposes it directly.
- Downstream dependency check: SB18 final red-team can verify process tools do not drift from the live-run API surface.

- Invariant ID: SB11-INV-003
- Source raw note: RN11 - Keep OpenAPI and process API skill examples aligned with current runtime models.
- Expected behavior: OpenAPI route coverage asserts current template routes, and the process API skill documents `freshRunPolicy` fields plus the live-run profiles process tool.
- Disallowed shallow implementation: Adding undocumented runtime fields, documenting fields not returned by the API, leaving the active Codex skill stale, or relying on screenshots/browser proof for a non-UI API change.
- Failing-first test: bundle://proof/SB11/transcripts/failing-first.txt proves the old summary shape without `FreshRunPolicy` is rejected.
- Passing test: bundle://proof/SB11/transcripts/passing.txt proves OpenAPI route assertions and API field assertions pass.
- Changed source files: repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs; repo://codex/skills/candoitall-api-processes/SKILL.md.
- Production assertions: OpenAPI route tests assert template detail, envelope, mermaid, import, baseline scenarios, and live-run profiles routes; skill sync proof shows the repo and active skill copies match.
- Red-team negative case: Future docs cannot claim process template parity while omitting live-run profile fresh-run policy or the active skill sync.
- Downstream dependency check: SB12 docs/skills refresh and SB17 docs/template parity can cite this API/tool parity closure.

## Production Behavior Artifact Matrix

| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| `FreshRunPolicy` on live-run profile summaries | Template pack model projected by `ProcessesApi` and MAF process tools. | API clients, MAF agents, process operators. | Loaded from template pack, returned by live-run profile summary route and process tool. | Stale DTO-shape rejection in `bundle://proof/SB11/transcripts/failing-first.txt`. |
| `processes_template_live_run_profiles_list` | MAF internal process tool builder. | Agents with process read access. | Registered as read-only policy metadata and composed with internal process tools. | MAF/tool-policy tests in `bundle://proof/SB11/transcripts/passing.txt`. |
| Template OpenAPI route inventory | `ProcessesApi` route map. | Client generators and SB12/SB17 docs. | Asserted from `/openapi/v1.json` by focused integration test. | Missing route would fail `Api_openapi_exposes_focused_control_plane_routes`. |
| Synced process API skill | Repo skill copied to active Codex skill root. | Human and agent process API users. | Updated with live-run profile policy and active hash sync. | `bundle://proof/SB11/transcripts/skill-sync.txt`. |

## Validation

- Failing-first/adversarial proof: bundle://proof/SB11/transcripts/failing-first.txt.
- Passing proof: bundle://proof/SB11/transcripts/passing.txt.
- Source assertions: bundle://proof/SB11/transcripts/source-assertions.txt.
- Anti-stub audit: bundle://proof/SB11/transcripts/anti-stub-audit.txt.
- Changed-file hashes: bundle://proof/SB11/transcripts/changed-file-hashes.txt.
