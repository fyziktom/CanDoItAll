# SB11 proof manifest

Status: Completed. Requested behavior, architecture and final regression review passed
with the unchanged baseline failures below; no clean-repository claim. R15/N015.
Contract: bundle://proof/SB11/semantic-invariants.md.
Plan: bundle://subbundles/11-shared-access-failure-recovery/README.md.
Entry revision: 3eb4d4af0, initially clean. before-hashes.csv captures pre-edit bytes.

Incident: original run bfb2e58e-411f-4766-be91-ea952333bba1 in project
bbed9156-6935-469a-a357-d2eb4c3c028b, client 5214. HTTP 401 before any image tool.
No copied/generated token is allowed in artifacts. Registry metadata contains no token value.

## Root causes and smallest repairs

1. Original run bfb2e58e-411f-4766-be91-ea952333bba1, 10:12:59-10:13:05 UTC,
   failed before any tool invocation. Its shared-source JWT expired at 06:21:41 UTC.
   The safe transport diagnostic retained HTTP 401, but the UI formatter discarded
   that status and displayed only ProviderFailureBoundaryException. The formatter now
   consumes the existing typed safe status; MAF also retains HttpRequestException.StatusCode.
   HTTP 401/403 receive static source-authentication/permission guidance without
   leaking raw upstream bodies, URLs, secret values, or alleging expiry from 401 alone.
2. After UI renewal, run 349c6e81-8472-4923-9eec-e6b6abcf52a7 reached tools, but
   twice requested unsupported image size 1536x864. The schema did not tell the model
   its accepted dimensions, and an untyped exception became "Error: Function failed."
   The existing input contract now describes valid options; invalid options use the
   existing safe, correctable IAgentToolFailure contract. No silent size substitution.
3. Image creation then succeeded. Automatic image analysis rejected its 1,720,707-byte
   PNG because IsDataImageUrl incorrectly applied the 1 MiB text limit to base64 image
   data. Images now use the existing aggregate request budget (OpenAI: 4 MiB), with
   allocation-free base64 validation. Text/schema limits, allowed MIME types, existing
   operation-specific roles, vision capability, URL restrictions and total request caps remain.

## Production ownership and contract review

- Core/Execution/AgentProviderFailureDisplayFormatter.cs: source-specific safe user guidance.
- Maf/Runtime/Providers/MafProviderTransportBoundaryChatClient.cs: typed status capture.
- Modules.AgentFramework/AgentTools/ImageGenerationAgentRuntimeToolProvider.cs: descriptions
  and the existing structured tool-failure contract; JSON names/property types unchanged.
- SharedProviders.Http/SharedProviderRelayRequestPolicy.cs: bounded data-image validation.
- No new project, interface, runtime registry, partial class, database schema, provider
  catalog, model/effort policy, auth bypass or dependency direction change. Existing
  Core-to-Providers and Http-to-Abstractions boundaries are retained.
- Source authorization 401/403 is distinguishable from upstream auth failure: the
  source maps upstream 401/403 to UpstreamFailure/502. Local upstream failures do not
  receive shared-source token advice. Tests cover nested, invalid and missing status,
  local credentials, hostile remote messages, tool-origin failures and both MAF modes.
- No Razor or shared component-library edit. Real desktop UI uses existing source
  token/secret/connection dialogs, project chat, work history and governed file preview.
- CodeAnalytics snap-20260828101839-660712e3 and actual-diff selection records support
  the boundary review. No new project edge. Selection uncertainty is explicit in transcripts.

## Semantic evidence

| Invariant | Original negative | Successful producer/consumer evidence |
| --- | --- | --- |
| I1 safe shared status | Original run and 4ec3d3900ebf445f861083248a3ca130.json | failing-first -> final-focused-complete; repaired-401-ui.png and its UI snapshot |
| I2 enforce auth, renew through UI | Source non-admission count 0; expired-token metadata | renewed-token-metadata-ui.md; renewed-source-connection-ui.md; real authenticated usage |
| I3 actual image/asset | Original user run; context captured from the real project | image-created-run.json; image-created-ui.md; calculator-image-preview.jpg; source image count 1 |
| I4 safe correction/options | invalid-image-options.json; image-options-red.trx | final-all-focused.trx; real run uses accepted 1536x1024 |
| I5 image/text budgets | vision-budget-red-cases.trx: realistic images rejected | vision-budget-green.trx; vision-success-ui.md and work history; source vision inference |

All transcript filenames in this table are under transcripts/. UI screenshots are
in this folder. Full new vision-run dumps were not collected: the approved substitute
is the application's already-redacted work history plus metadata-only source audit.

## Real UI chronology (UTC, 2026-08-28)

- Original failed user run: bfb2e58e-411f-4766-be91-ea952333bba1. Preserved unchanged.
- Rebuilt negative run: 733d7387-073f-4e4d-90a9-c7cc2cd4120f. The visible failure
  now states HTTP 401 and how to renew the source connection. Screenshot inspected.
- 10:50: Source Settings/API token UI issued a 1,440-minute token for desktop-client-5214,
  restricted to api.shared-providers.catalog.read and api.shared-providers.invoke.
  Client Settings/Secrets updated existing "shared instance" secret; Providers > Shared
  provider connections > Test/Sync succeeded. The one-time bearer stayed inside the
  browser transfer and was not printed or written to proof. Source token UI was reloaded.
- Renewed image attempt: 349c6e81-8472-4923-9eec-e6b6abcf52a7 exposed the invalid-size fault.
- Successful image run: cf11744f-0b2a-4426-a1ac-9a77983da4aa, 11:08-11:09. Portfolio
  Architect read the existing Calculator requirements and called real gpt-image-1-mini
  through UI Shared OpenAI Image. PNG: 1536x1024, 1,720,707 bytes.
- Attached "Calculator UI Proposal", node custom:e06c5abebeb9430c9f623b7b56e4d39b,
  under Main (custom:4a0df0fde0bc42599d651c9ffae9d5d1). Metadata/content readbacks
  succeeded. Original requirements remain: Blazor PWA WASM, Windows-style calculator,
  and right-column calculation history. Canvas has six nodes, previously five.
- Selected this node via Object index, then Expand preview through governed FileInteraction.
  The image loaded at natural 1536x1024 and was visually inspected: calculator and
  right-hand History panel. Raw unsigned storage URLs correctly returned 401; no
  browser JWT or unsigned-route workaround was used to make the preview render.
- Final-image analysis run: 7adcda1b-ceb9-4dcc-a9bc-85dc2587ee4a, 11:34:57-11:35:13.
  UI explicitly requested pixel analysis of the existing asset, with no new image or
  project mutation. Work history records project_structure_asset_image_analyze on
  the exact node. Successful result describes display 2,484 and History entries
  60+8=68, 32x78=2,496, matching the actual image. No second image was generated.
- Source source-image-and-vision-usage.csv: image request 0HNO4UGTKO5E8:00000003 is
  Succeeded/Complete, ImageCount=1. Final vision request 0HNO4UVG53I7U:00000003 is
  Succeeded/Complete, gpt-5.6-luna, input/output tokens 1952/279. All surrounding
  chat calls also succeeded. Prices remain Unavailable rather than fabricated rates.
- generated-asset-metadata.json records the exact stored PNG hash and length after
  rebuild. vision-request-correlation.csv records non-secret request/trace metadata;
  correlation to this UI run uses the subject, matching operation window and tool
  evidence; the source does not store the client run GUID as its correlation ID.

## Test selection and original results

- Frozen discovery and exact TRX identities are reconciled by Run-Tests.ps1. Three
  deferred Unit theories expand to 37 runtime cases; Integration has one deferred
  theory. Zero/missing/unselected tests fail the runner. No full-suite green claim.
- Red: status 34 pass/7 fail; image options 0 pass/4 fail; image budget 3 pass/4 fail.
  An initial image-budget fixture compile failure is preserved separately, not counted
  as behavioral red evidence.
- Green focused: final-all-focused 69/69; vision-budget-green 60/60. These are distinct
  class sets: 129 focused cases total, including real schema/producer tests.
- Final Unit: vision-unit-complete, 7,059 passed / 1 failed / 0 skipped, total 7,060.
  The failure is the existing WorkflowCatalogTests.ComponentLibraryAcceptsStructuredOutputForOllama
  missing llama3.2 price fixture, already documented in SB06/SB07/SB09. The temporary
  process-cancellation failure in the earlier broad run passed all 13 isolated class
  cases and passed in this final full Unit run. No unrelated fixture was changed.
- Final Integration: vision-integration-complete, 1,133 passed / 10 failed / 1 skipped,
  total 1,144. Frozen 1,139 identities reconcile exactly after one deferred theory
  expands into six cases. All ten failed identities and complete failure messages
  match SB09 after normalizing generated GUIDs and one ephemeral localhost port.
  The opt-in live Ollama test remains skipped. No new failed identities or causes.
- broad-regression-results.md and transcripts/final-regression-comparison.json record
  the exact Unit/Integration comparison. Eleven existing failures remain; no unrelated
  fixture was edited to make the suites green. Two JWT-shaped fixture strings in the
  completed Integration TRX were mechanically redacted; credential-redaction.json
  retains before/after hashes and confirms identical result identities and counters.
- Selection records: impact-selection.json, impact-selection-final.json and
  impact-selection-vision.json. Static dispatch/reference uncertainty required the
  supplied suites. The final Http owner adds Integration. No solution-wide or Components
  gate is claimed: no UI component code changed.

## Deployment and validity

- Final image: candoitall-shared-providers-ui:shared-access-20260828-3.
  Image ID: sha256:331c761d543ef936f82456630dd98f03cecde12e9e36dbf24a6e18123d70435b.
- docker-build-vision.txt and restart-source-client-vision.txt/restart-5214-vision.txt
  preserve the actual commands. final-host-health.json records all three running
  containers, retained named mounts and HTTP 200 Healthy on 5210, 5212 and 5214.
- Source/client rollback containers retained. No database/volume reset, 5032 restart,
  unrelated agent/provider settings change, alternate upstream or test-only production branch.
- Image generation ran on image -2. Its three repaired source files are unchanged
  in final image -3. The only additional production source change is IsDataImageUrl,
  outside ImageGenerations validation. I3 therefore remains valid; I5 was rerun through
  final image -3. The stored preview is also rechecked after rebuild.
- deployed-owner-hashes.csv compares both images without networking or data mounts.
  Core and MAF assembly bytes match; Http and its consuming Module assembly were
  rebuilt. No claim is made that every assembly is byte-identical. Source-diff review,
  final Unit real-tool regressions and the separate final vision producer proof
  establish the bounded invalidation above.
- Token expires 2026-08-29 10:50 UTC (06:50 America/La_Paz). Renewal is bounded and
  manual; authentication policy was not weakened. The separate 5212 token is expired
  and was not changed by the 5214 incident recovery. HTTP health is not an auth test.
- changed-files.csv and proof-hashes.csv tie final review to exact source/evidence
  bytes. Reopen if these owner bytes, credentials, request caps, tool schema or actual
  deployment change; passing mock tests alone cannot preserve live-flow acceptance.
