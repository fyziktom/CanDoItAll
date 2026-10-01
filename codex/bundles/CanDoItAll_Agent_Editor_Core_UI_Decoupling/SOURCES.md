# Reviewed sources

Pinned review sources, not execution checkout instructions. FileTools uses its own exact ref.
Coverage is explicit: a read project file is not an evaluated graph, test source is not test execution,
and checked-in evidence is an implementation report rather than independently inspected private TRX.

## R01 — docs/architecture/projects-files-ui-decoupling.md

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `80c21a74fc2a503a4ec32b048224133dbeafff6b`.
Coverage: Complete architecture/execution record.
Role: P2 scope, owners, graph, published sandbox and bounded wider-test decision.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/docs/architecture/projects-files-ui-decoupling.md)

## R02 — docs/architecture/projects-files-ui-p2-evidence.md

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `b0a60badee75d3736a7bd98e2cfe94e085326324`.
Coverage: Evidence index including final sections; retrieved as whole text and focused tail ranges.
Role: Implementation-reported tests, mixed attempts, development loop, provenance and limitations; private artifacts not inspected.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/docs/architecture/projects-files-ui-p2-evidence.md)

## R03 — src/Modules/CanDoItAll.Modules.Projects/ProjectFilesSurfaceSession.cs

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `eb86cb2701c9524c5a0c7f4dcd352c9756c93335`.
Coverage: Complete 288-line source.
Role: Same-activation operation publication and cleanup; P2-R1 concrete source flow.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/src/Modules/CanDoItAll.Modules.Projects/ProjectFilesSurfaceSession.cs)

## R04 — src/Modules/CanDoItAll.Modules.Projects/ProjectFilesActivation.cs

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `347244769eb0e245c67369ba9dd9ea780acf504c`.
Coverage: Complete file.
Role: Activation versus per-operation identity and cancellation ownership.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/src/Modules/CanDoItAll.Modules.Projects/ProjectFilesActivation.cs)

## R05 — tests/Unit/CanDoItAll.Tests.Unit/ProjectFilesSurfaceSessionTests.cs

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `3b0cd63c5f345ac185b047104d842532806af525`.
Coverage: Complete source.
Role: Existing activation replacement, preview, action and cleanup controls; missing same-activation late failure schedule.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/tests/Unit/CanDoItAll.Tests.Unit/ProjectFilesSurfaceSessionTests.cs)

## R06 — src/UI/CanDoItAll.Projects.Files.UI/ProjectFilesDialogView.razor

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `745674c1853e20644ecbe6df2fe93f8ec7aa1f33`.
Coverage: Complete renderer.
Role: Real browser remains visible during preview; current preview error display; title bytes need local confirmation.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/src/UI/CanDoItAll.Projects.Files.UI/ProjectFilesDialogView.razor)

## R07 — src/CanDoItAll.FileTools.FileBrowser.Components/Models/FileBrowserInteractionDispatcher.cs

Repository: `fyziktom/CanDoItAll.FileTools`. Ref: `3a080ecd31068a77c1e1bd639f7a78e21c93db85`.
Observed blob: `d021ddd8fe81dbc1d21445be74ed2bc24c77af67`.
Coverage: Complete file at FileTools source revision.
Role: Rendered item/snapshot guard does not serialize accepted host preview callbacks.
[Open pinned source](https://github.com/fyziktom/CanDoItAll.FileTools/blob/3a080ecd31068a77c1e1bd639f7a78e21c93db85/src/CanDoItAll.FileTools.FileBrowser.Components/Models/FileBrowserInteractionDispatcher.cs)

## R08 — src/Modules/CanDoItAll.Modules.Projects/Pages/ProjectsPage.razor

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `a08dc4a48838a2b5292512e4a4e2f5628e46e434`.
Coverage: Lines 650–921 (Save/seed/refusal/Delete paths), not a fresh full-page audit.
Role: Verify exact P1-R1 refusal correlation and release without changing unknown outcome policy.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/src/Modules/CanDoItAll.Modules.Projects/Pages/ProjectsPage.razor)

## R09 — src/UI/CanDoItAll.Projects.UI/ProjectModalHost.razor

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `5b6d5b390216d3863aac50c404e5c88aee62a944`.
Coverage: Lines 540–end, interop ownership and callbacks.
Role: Verify P1-R2 pending import versus acquired module teardown.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/src/UI/CanDoItAll.Projects.UI/ProjectModalHost.razor)

## R10 — docs/architecture/modules.md

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `06c07de38d39bc49b2b3b0d5252c35de7ef324c8`.
Coverage: Maintained rendering table and remaining-roadmap sections plus owner map.
Role: P2 still marked deferred; partial AgentFramework and later Workflow/Workbench/Processes scope.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/docs/architecture/modules.md)

## R11 — src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `1e86353cdb6001b4e805a1fbec9f5c5d83f5e3ae`.
Coverage: Complete markup through sequential ranges 1–210, 211–490, 491–800 and 800–end.
Role: Actual ten-section form, four core sections and six deferred integrations.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor)

## R12 — src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor.cs

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `7e8ef15d8e4e2d9678cdf8a50bb628bc2b55d5d0`.
Coverage: Lines 1–840 in three ranges; remaining helper methods require implementation census.
Role: Session, reads/commands, current-target guards, save/reconciliation, confirmations and capability effects.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor.cs)

## R13 — src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentEditorSession.cs

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `892bd36f5c44a23fdae9e7413b3359df7a4286ca`.
Coverage: Complete source.
Role: Canonical target/draft/context, pending refresh and unknown write state, ten section enum.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentEditorSession.cs)

## R14 — src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentEditorDraftPolicy.cs

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `012b66cc99d3d1f76fea535e213e23b82d90eeca`.
Coverage: Complete source.
Role: Detached request, default policies, Favorite tags and all deferred-data copy preservation.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentEditorDraftPolicy.cs)

## R15 — src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentEditorCommands.cs

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `cdb7f4694096a1c91056cdea56c6e195b0200f5c`.
Coverage: Complete source.
Role: Real native command classifications, root normalization and reconciliation.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentEditorCommands.cs)

## R16 — src/MAF/Common/CanDoItAll.AgentFramework.Components/ProviderModelSelector.razor

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `6b532238e0040d443d36dd5359958eef570d9630`.
Coverage: Complete facade.
Role: Existing neutral conversation model selector and compatible default/custom value mapping.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/src/MAF/Common/CanDoItAll.AgentFramework.Components/ProviderModelSelector.razor)

## R17 — src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentThinkingEffortSettings.razor

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `1f5a80bd3c3e5aa6b720871a26e2d085dfc1d71d`.
Coverage: Complete renderer/policy-adaptation source.
Role: Supported/unsupported/unknown effort, unavailable saved override, default versus None and boolean labels.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentThinkingEffortSettings.razor)

## R18 — src/MAF/Common/CanDoItAll.AgentFramework.Components/CanDoItAll.AgentFramework.Components.csproj

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `8ba5e893756b47bf03ab2edc230d9c30de7a0396`.
Coverage: Complete project declaration, not evaluated MSBuild.
Role: Core/Voice/Canvas graph expansion risk from importing a broad facade assembly.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/src/MAF/Common/CanDoItAll.AgentFramework.Components/CanDoItAll.AgentFramework.Components.csproj)

## R19 — tests/Components/CanDoItAll.Tests.Components/AgentEditorSessionTests.cs

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `4dc73a84836f3e8f67e17be0c0e84bb9036b7a95`.
Coverage: Complete test source within requested 1–250 range.
Role: Form context and all sections, independent editors, lazy reads, old target results and reset behavior.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/tests/Components/CanDoItAll.Tests.Components/AgentEditorSessionTests.cs)

## R20 — tests/Integration/CanDoItAll.Tests.Integration/AgentEditorAdapterIntegrationTests.cs

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `b3d08a5c006f7426f04b36400723c7d5b919c9d0`.
Coverage: Lines 1–180; later cache-invalidation tests not fully read.
Role: Native managed delete denial, full settings round-trip, optimistic version and postcommit warnings.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/tests/Integration/CanDoItAll.Tests.Integration/AgentEditorAdapterIntegrationTests.cs)

## R21 — tests/Components/CanDoItAll.Tests.Components/AgentEditorAdversarialTests.cs

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `cfcee7b7e4ffb8d1264500b20ce8c9a33457f6bf`.
Coverage: Complete source within requested 1–185 range.
Role: Safe error presentation, failed read-back no replay and request-token lifetime controls.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/tests/Components/CanDoItAll.Tests.Components/AgentEditorAdversarialTests.cs)

## R22 — src/UI/CanDoItAll.AgentFramework.UI/CanDoItAll.AgentFramework.UI.csproj

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `3922611e7b630fb741ed151925a3f7d6b06a27c4`.
Coverage: Complete declared project references.
Role: Existing Agent catalog UI already consumes Models/Usage/Conversations; protect it from new editor edge.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/src/UI/CanDoItAll.AgentFramework.UI/CanDoItAll.AgentFramework.UI.csproj)

## R23 — src/MAF/Common/CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `fa7a01bf6b4613df1f8fdd0a3d9255393d1b9d08`.
Coverage: Complete project declaration, not evaluated full closure.
Role: Avoid blanket ban of neutral MAF model contracts; inspect actual graph and exposed values.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/src/MAF/Common/CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj)

## R24 — src/UI/CanDoItAll.Projects.Files.UI/CanDoItAll.Projects.Files.UI.csproj

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `b44097a6ffd92a740134fbcd086a86536e99bd1f`.
Coverage: Complete project file.
Role: P2 real neutral package reference boundary.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/src/UI/CanDoItAll.Projects.Files.UI/CanDoItAll.Projects.Files.UI.csproj)

## R25 — .github/copilot-instructions.md

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `dde5c2fdbcb56b7b5f48dfa17ed74fda238c2e1a`.
Coverage: Complete current instructions.
Role: Module ownership, real components/MCP, desktop UI and mandatory gates.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/.github/copilot-instructions.md)

## R26 — docs/testing.md

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `f89ab60d99213f827a1fddeebac7bbc3b88f50fb`.
Coverage: Lines 1–132, current prerequisite/local loop guidance; reread complete current gate/CI policy at execution.
Role: Isolated PostgreSQL 18, discovery counts, bUnit event/dispose discipline and targeted loop.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/docs/testing.md)

## R27 — src/Modules/CanDoItAll.Modules.Projects/Pages/Components/ProjectFilesDialog.razor

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `35150021668f8fc5b987940d64313a534c619d0c`.
Coverage: Complete retained host.
Role: Actual coordinator/session/context composition and captured project/close target.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/src/Modules/CanDoItAll.Modules.Projects/Pages/Components/ProjectFilesDialog.razor)

## R28 — docs/architecture/ui-component-seams.md

Repository: `fyziktom/CanDoItAll`. Ref: `92a3c373c742537608fded3c483b685291339853`.
Observed blob: `0b1d045b818ab05b623d94bf4796d1ab9e9b5f18`.
Coverage: Lines 1–198 of canonical guidance; full current guidance remains required at execution.
Role: State/owner separation, genuine independent scenarios, mutations and dependency direction.
[Open pinned source](https://github.com/fyziktom/CanDoItAll/blob/92a3c373c742537608fded3c483b685291339853/docs/architecture/ui-component-seams.md)

## Primary reference for asynchronous UI reasoning

[Microsoft Blazor synchronization context](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/synchronization-context?view=aspnetcore-10.0)
is background; it does not establish a product-specific defect by itself. Concrete finding P2-R1 is based on R03–R07.

Private prior artifact directories, unpublished execution helpers and raw screenshots were not inspected in this review.
