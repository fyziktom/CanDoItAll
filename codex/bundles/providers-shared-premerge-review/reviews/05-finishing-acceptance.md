# Finishing acceptance — 2026-08-31

Status: **Pass for the requested live acceptance lane; overall merge closure remains Blocked on the two separate gates below.** All three updated applications are running for manual testing. No commit, push or merge was performed.

## Running applications

- Local: http://localhost:5032/projects/f28c07cd-982c-4d2d-bcf2-3e60a32eca72/structure. Normal Release HTTP Development launch profile, Web PID 58036 (parent dotnet 22496), existing database/workspace/settings retained.
- Publisher: http://localhost:5210/agents?tab=request-history. Docker container candoitall-shared-providers-manual-central-1.
- Client: http://localhost:5214/projects/e008de34-69eb-4fea-9b47-b4c23991b17d/structure. Docker container candoitall-shared-providers-manual-client-a-1.
- The later 5120 reference in the request was resolved against live topology to 5210; no service was moved to 5120.
- Both containers use candoitall-app:premerge-aadd95315-catalog, image sha256:b6b502a5487bd8ba7b21a2c3afe18e4846d273fa0445b387a98bf85acda73089. Existing mounted files, databases, private networks, loopback ports, secret-file mounts, resource limits, non-root identity and read-only root filesystem were preserved. Prior app containers are retained stopped for rollback; never start an old and new container against the same data concurrently.
- Local Web DLL SHA-256: 60F188E37C58754076D6F462C236120EA7B63FB55ADC55C0A8924428F603A83D. Source baseline aadd953150e7f659e4060ced6505621c705ea61f plus the finishing UI repair. The export records workingTreeClean:false rather than claiming a clean commit.
- Current identities and health are recorded in bundle://proof/SB09/finishing/runtime.json. All three /health responses passed with HTTP200; both Docker applications were healthy.

## Real behavioral acceptance

| Check | Actual production path and result | Durable evidence |
| --- | --- | --- |
| Local PDF versus Excel | Spreadsheet Analyst run 8bb3f139-9ea0-47bc-9cfa-54fe6e390190, 36 steps, 1m44s. Located project nodes, statted both files, read workbook summary/range, converted the actual PDF after a single inspected approval, restored the matching checkpoint and completed. Purchase and selling-range values match for all three models. | bundle://proof/SB09/finishing/localAgentProof.json; 5032-work-history.png |
| Shared agent | Portfolio Architect run 181fb822-158f-48cd-b3b4-d110a2c900fe, 18 steps, 1m17s. Existing Calculator project: project_structure_read, project_structure_asset_content_get for the Markdown proposal, project_structure_asset_text_get for the SVG. Answer compares all three requirements, cites concrete content and correctly limits what a static SVG proves. | bundle://proof/SB09/finishing/sharedAgentProof.json; 5214-work-history.png |
| Shared Simple Chat | Existing C# Architect definition revision1, conversation 0d246c7b-1163-4e39-9f0c-036412f206a7. First answer calculates260 and explains await; follow-up remembers260 and subtracts17 to get243. Reloading the conversation preserves both turns. | bundle://proof/SB09/finishing/simpleChatProof.json; simpleChatReloadProof.json; 5214-simple-chat-reloaded.png |
| Publisher persistence | Five new SharedRelay / CompleteChat / Succeeded rows: three agent calls and two Simple Chat calls, all mapped to upstream gpt-5.6-luna. Managed credential attribution, complete usage, correlation, canonical links, expiry and partial price evidence are visible after a fresh history query. | bundle://proof/SB09/finishing/publisherFinalRows.json; publisherAgentProof.json; publisherSimpleProof.json; publisher screenshots |
| Project Agents button | Original page drove IFloatingAgentChatCoordinator, but the rendered host listens to IConversationShellCoordinator. The page now uses the rendered coordinator for visibility, events and Agents filtering. Hide externally, reopen through project toolbar, then hide again works. | catalog-before.trx (1 expected failure), catalog-after.trx (4 passed), 5032-catalogue.jpg, 5032-project-normal.jpg |

Local file ground truth was independently read using pypdf/openpyxl: ZM-x5600 purchase35000 / selling39900–42000 USD; ZM-x6600 purchase41500 / selling46000–49000; ZM-x6600A purchase66000 / selling73000–78000. PDF page1 and Pricing rows2–4 agree. Target/margin columns are formulas; uncached formula values were not presented as measured prices. The local comparison ran on the rebuilt aadd product before the UI-only coordinator repair; the provider/file/approval paths did not change afterwards. The final host separately passed the repaired toolbar behavior.

The client used its existing Markdown/SVG assets, not a copied PDF/Excel fixture. Their actual stored bytes were independently inspected: basic non-scientific arithmetic, left-hand keypad and right-hand history agree with the answer. This proves shared model routing and sequential real tool/file reads; it does not claim a second PDF conversion test on Docker. Agent approval policy was already Auto Approve on the client and was not changed. No project nodes or existing file contents were modified.

The first Simple Chat response uses an ordered-list marker of260; DOM innerText omits list markers. The DOM ol start=260 and inspected screenshot establish the first result; the second answer spells out both numbers. This is Markdown rendering, not lost streamed content.

Publisher rows (UTC): 10:47:12 usage15790/155, 10:47:21 usage19048/168, 10:47:32 usage28292/1339, 10:59:45 usage91/119, 11:00:41 usage174/40. Latest agent request f6fd8199-e940-4053-9a96-a0447d9c4597 carries the Calculator project reference. Follow-up chat request013b0bc1-741b-4a7e-8e03-ad81153d4132 has no external project reference, as expected for this standalone chat. Both are CanonicalProjection / HistoryPolicy with Detail:NotCaptured and 30-day expiry. Light capture does not duplicate full prompts; Partial estimate is not a complete billing claim. Credential IDs are attribution identifiers, not secret key material.

## Repair, build and focused validation

Production changes are limited to ProjectStructurePage.razor, its existing AgentWindows UI code-behind and one direct Workbench UI -> Conversations.Shell UI project reference. No new project, runtime partial, service layer, schema migration or XML documentation was introduced. Existing agent-completion refresh remains unchanged.

The regression test renders the real page with its registered coordinator and detects the original invisible-catalogue defect. The focused selector is:
FullyQualifiedName~Agents_toggle_tracks_the_visible_conversation_catalog_after_external_close|FullyQualifiedName~Agent_completion_reload_preserves_immediate_canvas_selection_without_capturing_javascript_state|FullyQualifiedName~ConversationShellHostTests
Expected/actual:4/4, no skips. Failing-first selected only the new test and failed the expected visible-state assertion. Builds/tests use Release, SDK10.0.303, local sibling references, one worker and isolated artifacts/premerge test outputs. PostgreSQL test control was a separate disposable container on loopback52039; no test harness was pointed at a live application database.

Final normal Web Release build: zero warnings/errors. Docker final Release publish succeeded for both preserved configurations; Docker SDK10.0.302/runtime10.0.10. Build/replace logs and both TRX files are in bundle://proof/SB09/finishing. The managed dotnetwatch launch attempt failed in its isolated template-copy output path (Windows path length, MSB3021), not during an application request. The normal original HTTP profile was rebuilt and launched successfully with dotnet run --project src/App/CanDoItAll.Web -c Release --no-build --launch-profile http. It runs hidden and is deliberately left running.

Before CodeAnalytics snapshot snap-20260831103116-dfe9b686 loaded only Workbench; its empty filtered edge result is not full-graph proof. After snapshot snap-20260831104137-dfe9b686 loaded Workbench and Conversations.Shell: direct edge in the intended direction, zero cycles; two non-blocking generated attribute DEP0002 duplicates. The explicit ProjectReference closure audit traversed52 projects/182 edges with no unresolved references or cycles, and no Shell->Workbench edge. Source and graph evidence agree.

The prior single frozen Stable gate remains9,424/9,424 passed. The UI-only repair invalidated the focused page/shell composition checks, not every provider/history/migration test. No second broad gate was run for documentation or hash changes. No EF/model/migration file changed; existing two isolated migration rehearsals and generated SQL remain applicable.

## Contract and skill completion

Export-ApiContract.ps1 captured both canonical5032 endpoints from the identified final process and checked byte equality, ownership and assembly hash. Actual OpenAPI3.1.1:963289 bytes,276 paths,308 operations,486 schemas; SHA-256 14FE4C527863FF84948ED96D3D7A3B16FD46D3E315E673E96EEF3911C3D2A52B. The actual counts were computed, not assumed from the previous snapshot. generatedUtc now serializes with Z so the existing validator preserves UTC under PowerShell JSON parsing.

SharedInfo snapshot, full family/operation inventory, manifest and support README are current. Exactly five packages were installed through its maintained installer: _candoitall-api-shared, candoitall-api-shared-providers, candoitall-api-agents, candoitall-api-llm-chats, candoitall-api-workflows. All11 installed files match source hashes. Four skill validators pass. Test-CanDoItAllWebOpenApi.ps1 passes with zero failures; Test-SharedInfo.ps1 passes45 skills/402 Markdown/12 PowerShell files. Evidence: api-export.json, installed-skill-hashes.json, sharedinfo-openapi-validation.log, sharedinfo-validation.log and skills-validation.log in the finishing proof folder.

## UI inspection and proof limits

App target is1920x1080 desktop; no basic BaseLib component was changed. Inspected normal project/catalogue, shared work-history overlay, publisher history normal/detail and Simple Chat screenshots. The catalogue remains a floating support surface above the project canvas; list body owns its scroll and header actions remain visible. Work-history overlay has a visible close action and scrollable steps. Publisher detail fits the viewport, including full profile hash and bottom close action; no clipping at the target size. Simple Chat uses its existing conversation transcript scroll owner and composer. Earlier narrow publisher/client captures were replaced with1920x1080 artifacts, not used as desktop layout proof.

Auto-review rejected using the live development PostgreSQL connection for the component test control; the isolated52039 container resolved that safely. Auto-review also rejected creating a new project fixture on the live client. That action was not retried through another interface; existing Calculator assets supplied the complex task. No approval is needed for either abandoned approach because useful acceptance completed without it. The unrelated8080 app and application PostgreSQL services were untouched.

The final Docker log review found one publisher and three client Antiforgery[7] deserialization errors for missing key-ring keys in the reused localhost browser. Navigation issued usable tokens and all tested interactions completed. These are recorded as stale/foreign-cookie setup noise (consistent with separate instance key rings sharing localhost), not hidden or counted as provider failures. No other fail/critical logger categories occurred in the final containers. This does not prove all possible browser-cookie/session combinations; use a fresh browser context if an old form rejects its token.

## Final consistency

Prepared and completed-stage structural validators pass with SB09 explicitly Blocked; this is not a semantic merge-closure pass. The finishing manifest verifies all45 original provider/history source/test hashes are unchanged and all11 active skill files still match. git diff --check passes. The separate temporary PostgreSQL container and its anonymous volume were removed after tests; application data services remain running. Exact command/result map: bundle://proof/SB09/finishing/commands.md.

## Remaining merge gates

1. Original deterministic three-application SB07 (central plus two independent clients) remains unexecuted under its exhausted historical lifecycle/image budget. These two live Docker instances are not a replacement. The original request for one replacement lifecycle/build under cumulative9/9 ceilings, reserving one each for SB12, remains in reviews/04-remaining-gates.md.
2. Independent implementation review is still required. This report and CodeAnalytics are implementing-agent evidence, not another independent reviewer. The user can review the concrete final diff and proof before the manual merge.

SB08 is now Completed. SB09 remains explicitly Blocked for overall merge closure, while the requested rebuild/live acceptance/repair/leave-running lane is complete.
