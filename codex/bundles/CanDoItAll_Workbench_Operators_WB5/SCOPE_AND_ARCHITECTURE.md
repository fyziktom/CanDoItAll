# WB5 scope and architecture

## Goal

Complete the remaining *operator integration* presentation in Workbench as one substantial
run, with separate validated stages and independent state ownership. This advances the
module beyond Content without folding Workflow/Process orchestration into the same change.
The objective remains smaller representative `dotnet watch` builds, not API-only UI.

| In scope | Real presentation to move | Native responsibility to retain |
|---|---|---|
| Participant/directory | Whole inline editor, local-only/link choice, real picker, quick create, feedback and read/retry | Directory and canonical assignment owners, project/node identity, metadata and authorization |
| Meetings | Real selection, defaults, missing references, Save and feedback | Role policy, canonical membership, primary/affiliation identity, concurrency and node metadata |
| Protected references | Entire picker/search/purpose/note/edit/create-and-use form and its real SecretField | Vault, secret lifecycle, allowed metadata, original node/reference mutation and secret policy |
| Node quick actions | Actual quick-action dialog and offered actions, not a surrogate list | Current node-kind/capability policy and the existing typed action dispatch |
| Runtime | Explicit launch confirmation, availability, phases, exit/stop feedback and readiness | Existing plan compiler/path authority/process session/environment/terminal/elevation adapters |
| Web preview | Actual EmbeddedBrowser, restrictions, open-in-browser, close and exact runtime stop | Trusted URI/embedding policy, process identity, readiness and lifecycle |

Read [parties](PARTIES_AND_MEETINGS.md), [secrets](PROTECTED_REFERENCES.md) and
[runtime](RUNTIME_AND_PREVIEW.md) before choosing the seam. [S14-S24]

## Placement and graph

Prefer `src/UI/CanDoItAll.Workbench.Operators.UI` plus
`src/Sandboxes/CanDoItAll.Workbench.Operators.UiSandbox` and an independent component-test
project. Three separate sessions can live in one renderer library without sharing state.
BaseLib, CanvasLib and RecordBrowsing supply actual widgets. Reuse existing small approved
contracts where the full graph is suitable. Add an Operators.Contracts assembly only for
shared values with a real boundary benefit, not to meet a project quota.

`ProjectPartyIntegrationContracts.cs` currently lives in the Projects implementation;
its filename is not proof that referencing its assembly is lightweight. Do not move this
whole mixed file or pull the Projects/CRM/MAF runtime graph into the new leaf. Pick narrow
projections or carefully move only genuinely shared values with compatibility proof. [S15]

Production may compose WB5 through named slots. WB1 Planning, WB2 Insights, WB3 Structure,
WB4 Content and all completed Agents/Workspace/Projects/Workflow leaves and sandboxes must
not gain a reverse reference to Operators or its native adapters. Foundation, AppComponents,
MAF and Processes may not depend on product renderers. Do not introduce `IServiceProvider`,
a delegates/service bag exposing every page operation, or copied owner algorithms.

Move actual markup, real descendants, CSS, custom templates/registrations and asset usage.
A parent thin adapter is correct when it owns lifetime/effects; a leaf receiving the entire
old form as a RenderFragment is not extraction. Do not force native authority into the leaf
just to reduce the route's line count. Reuse canonical seam rules. [S25, S26, S29]

## Production composition and compatibility

Preserve the existing three Structure tabs, routes/query tokens and back/forward context.
Keep original node quick actions that target Files, Prompt Gallery, TestLab, Agents,
Workflows and Processes available through typed host intents. Only quick-action presentation
moves; those external product pages do not become part of this bundle.

Maintain public identities/type forwards and dynamic dialog activation where supported.
Check actual callers, not just import strings. A retained legacy class must be classified
honestly. There are no placeholder implementations in production; unavailable operations
are explicit native states, not synthetic success.

## Out of scope

Do not extract Workflow/Process definition linking, start/review/matching/recovery or the
Processes product module. Do not build a terminal emulator, redesign process supervision,
change a global ownership model, implement missing provider features, redesign retention,
upgrade SDK speculatively, or tune mobile/tablet layouts. Keep deferred integrations working
and map their exact remaining sources in the final census. See [roadmap](ROADMAP.md).
