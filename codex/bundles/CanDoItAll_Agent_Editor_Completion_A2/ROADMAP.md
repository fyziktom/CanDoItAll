# Current rendering roadmap and direction assessment

Sources: R01, R03/R04, R21–R26 and `review-provenance.json` subtree inventories. The detailed behavior
review focuses on A1/A2; later families are a current path/representative-source census, not a
line-by-line correctness audit. Difficulty is architectural integration risk (1–5), NOT duration,
completion percentage or a fixed promise of the number of runs.

## Direction

The program is on the intended path: actual leaf renderers + independently usable scenario hosts,
without turning every screen into an HTTP client. Completed Projects P1/P2 and Workspace should not
be refactored again merely to remove legitimate routed/effect hosts. The 11/12-project A1 pair and
534 watch inputs are concrete isolation evidence, while whole-Web timings do not promise universal
speedups. Protect owner/policy boundaries rather than maximize interfaces or project counts.

The earlier 'AgentFramework 2–4 slices' estimate bundled very different families. After inspecting
actual remaining markup, track editor, provider administration, capability authoring, team and
chat/usage leftovers separately. This is more honest than declaring the entire module complete
when its ten-section editor is done. Do not enlarge this run to every one of those families.

| Order / family | Current status and concrete remainder | Difficulty / practical decomposition |
|---|---|---|
| Projects | P1 board/editor/hierarchy/package presentation + P2 dialog/portfolio Files completed. Route, context, owners and leases are retained intentionally | No new Projects extraction assigned; bounded regressions only |
| Agent technical editor | A1 shell + Identity/Runtime/Images/Voice complete; six access/Memory/capability sections and small confirmations remain | **4/5; this larger A2**, three implementation stages plus final integration |
| Agent provider administration | `AgentProviderProfilesPanel` still owns substantial tree/form/tab markup; connection/test/Thinking and source-managed sharing/history/model maintenance require independent boundaries | **4/5; about 1–2 substantial later cuts**, after its exact child census |
| Capability-definition authoring and team dialogs | `CapabilitySetupWizardDialog`, definition/configuration dialogs; team detail/member dialogs still have actual rendering | **3–4/5; about 1–2 later cuts**; assignment list itself is already extracted |
| Agent chat/usage/global voice residuals | Usage/detail/voice controls and nested runtime dialogs need a renderer/host census. `AgentChatPanel` already mostly composes ChatWorkspacePanel and real host actions | **2–3/5 audit; 1–2 cuts only where real renderers remain**; no replacement Simple Chats domain |
| Workflow authoring/management | Existing light Workflows.UI is a foundation. `WorkflowCanvasEditor` still imports Core/Prompts/Workspace and renders its native canvas/toolbox/inspectors. Catalog/run/settings/admission dialogs need closure | **4/5; roughly 2–4 cuts**; runtime/version/launch authority stays native |
| Workbench | Calendar/manager summaries, task/assignment forms, Gantt, Structure overlays/native canvas, file/runtime/attachment surfaces remain | **3/5 read/calendar entry; 5/5 family; roughly 4–6 cuts**. Smaller coherent surfaces first |
| Processes — last major family | Thin route pages still compose substantial ProcessWorkspaceShell, role/step/canvas, live dashboard, run files, launch/approval/recovery | **5/5; roughly 4–6 cuts**. Read/catalog before launch/monitoring/recovery |
| Final Web/shared sweep | Home/runtime-capability composition, leftover neutral utilities and scenario gaps | **2–3/5**; only proven reusable markup, not a universal rewrite |

Workspace, CRM/HR, Prompts, Collaboration, TestLab, Plugins, SchedulerPlanner, Memory and Resources
retain their selected completed boundaries. Their integration tests remain consumers, not a reason
to start replacement UI projects. Security has no standalone extraction assignment without an
actual remaining screen; Secrets/API administration already belong to their product surfaces.

## A2 closure is narrower than AgentFramework completion

A2 must leave the real capability-creation wizard, Avatar generation and source refresh working via
explicit host integrations and record them in the census. Full provider/capability authoring remains
on the next roadmap. Completed global capability catalog is not recreated; the technical editor's
assignment/verification section is a separate user journey using the same authoritative facts.

The final maintained map should name actual component path, renderer assembly, production host,
scenario coverage, retained owner, known limitations and suggested next slice. Do not infer remaining
work from file extension, line count or a grep for 'inject' alone. Do not update sealed historical maps.
