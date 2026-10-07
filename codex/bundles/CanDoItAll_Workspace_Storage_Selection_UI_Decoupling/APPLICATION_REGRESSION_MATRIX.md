# Application non-regression matrix

Purpose: prevent a small picker extraction from breaking general application behavior or creating new module coupling. The source review cannot certify these behaviors by itself. Execute current targeted tests and real host journeys; a standalone sandbox is not a replacement for persistence, permissions or composition proof.

## Required current application checks

| ID | Application boundary | Concrete acceptance evidence |
|---|---|---|
| A-START | Real application composition and Settings navigation | Build Workspace, Agent module, required UI leaves, Composition and Web. Start a private production-style host. Visit actual Settings Core/API/Storage and the Agent editor. No missing DI/service/assembly, circuit exception or unintended eager driver work. |
| A-AGENT-STAGE | Picker versus parent persistence | Open the real Agent Storage access editor. Stage a distinct explicit ID list, Apply, then cancel the parent. Verify existing stored Agent access is unchanged. Reopen, stage again and explicitly Save the parent; read back exact saved IDs/flags from the actual owner. |
| A-AGENT-STATE | Other Agent state remains intact | Before picker use, edit representative instructions/name or another normal field and retain the existing parent edit lifecycle. Open/cancel/apply the child; verify instructions, capabilities, project access, secret references and file/Storage flags are not reset or broadened. Parent Save follows its existing reconciliation policy. |
| A-ACCESS | Real Storage access enforcement | Through the existing Storage runtime/integration tests, show an Agent with AllowAll=false sees/uses only allowed catalogs; another catalog remains filtered/denied. Empty list stays restricted. Read-only target does not become writable because it was selectable. No live LLM is required. |
| A-LIFETIME | Parent/profile/caller origin | While a picker/read is held, close/replace the Agent editor or change the fixture profile/caller context. Finish old work. It cannot mutate successor IDs, revive old metadata, close an unrelated dialog or cause a new save. Use established isolated lifetime fixtures, not a redesigned switch service. |
| A-CATALOG | Catalog administration is not re-coupled | Run S0 actual same-target regression and retained unknown/partial-routing/captured-input tests. Picker open/apply does not call SaveStorage/Test/Delete/routing APIs. Preserve original catalog ID and Health/routing semantics. |
| A-BOOTSTRAP | Existing catalog read behavior | Confirm the production read adapter uses the established owner and preserves its bootstrap/metadata semantics. Do not assert every first read performs zero database writes: existing owner bootstrap may write. Instead verify no new explicit catalog mutation or driver call is added by picker actions and no render/keystroke loop repeats owner bootstrap. |
| A-CORE | Ordinary Settings functionality | Retain the focused Files target-identity, Resources exact-editor readiness and Core secret/history checks affected by shared composition. Existing direct routes and Providers redirect remain reachable. No new extraction of Core controllers. |
| A-API | Previously repaired API boundary | Run the AP current-denial/cleanup negatives and relevant production denial/retry path. API credentials and accounts stay instance-local; selection graph must not reference their runtime or UI. |
| A-FILE | Common file/attachment consumers | Select the existing safe Storage attachment/routing/Resources reopen regression tests from actual changed read-contract consumers. Demonstrate harmless owned file content can still be read through its existing authorized path. Do not launch applications, bypass grants or require paid model calls. |
| A-DEFERRED | Deferred Workspace surfaces remain working | Passive Data Sources route and Storage Recovery open/close use real production registrations, with original captured ID/null. Existing configuration renderer/fallback tests remain green. Do not perform live database transfer or external dispatch as a smoke test. |
| A-GRAPH | No hidden dependency regression | Evaluate Core, API and catalog-admin sandbox graphs/watch inputs before and after. No new selection/Agent/Workspace implementation edge. New picker graph includes the actual neutral children and no reverse Foundation/MAF/AppComponents edge. |

## Scope-based widening, not ritual unfiltered execution

At minimum the real Agent parent save/read-back and runtime allowlist checks must run; a test that only verifies an emitted GUID array is insufficient. Reuse existing private owner fixtures and existing HTTP/runtime contract tests, selected by actual current references. Document which behavior each selection proves.

If the implementation stays in leaf UI/read projection and minimal host wiring, justify that narrow scope under current testing rules. If it changes an owning persistence protocol, canonical profile/factory, common fixture, module runtime behavior or shared generic component, widen the consumer selection and assess the current named broad-Stable trigger. Do not claim a clean broad pass from the last implementation's failed Stable receipt plus focused classification fix. [EV03, EV08]

The previous reported 658 focused cases are historical, not a mandatory repeated count. Current new/changed filters need fresh build-backed discovery. No ignored skips, zero-case filters or assertions replaced with weaker DOM-presence checks.

## Safe test environment

Use task-owned PostgreSQL 18 where required, private control-plane/vault/file roots, ephemeral ports and harmless fixture files. Never use or restart the ordinary application on port 5032, its retained database or another developer's host. Verify exact process/container identity and ownership labels before stopping anything. No real FTP/IPFS upload, native launch, bearer disclosure or external token issuance is needed for this picker slice.

Return a result per row: passed with exact evidence, failed with reproduction, not affected with source reasoning, or unavailable with explicit missing proof. “No UI error in the sandbox” is not a sufficient A-AGENT-STAGE or A-ACCESS result.
