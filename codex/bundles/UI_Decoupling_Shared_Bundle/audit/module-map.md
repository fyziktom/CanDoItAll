# Current decoupling map — PC1 reviewed, PC2 assigned

Review source: `146067ed133f624878dfe5756c441a43c0f21b4a` on 2026-10-09. This is not an execution pin or a claim of an exhaustive new whole-repository audit.

| Area | Current reading | Next action |
| --- | --- | --- |
| Processes workspace, Live Processes, canvas, roles, steps, templates, files, cancellation | PC1 supplies all eight renderer families in `CanDoItAll.Processes.UI`, with native hosts and an independent sandbox. The maintained record qualifies native authoring durability. | Preserve extraction. Execute PC2 correctness fixes and proof/census closure, not PC1 again. |
| Processes authoring owners | Native client creates short-lived scopes; definition/role stores shown in reviewed source are instance dictionaries; catalog remains template-derived. | Demonstrate the actual lifetime/read-back problem and deliver the bounded owner prerequisite specified by PC2. No singleton/UI-cache workaround. |
| Workbench WB1–WB6 | Earlier maintained records classify active renderers extracted. PC1 also touches the workflow launch path. | Protect native workflow/agent mapping and launch/source lifetime. Do not re-extract Workbench. |
| Agents/chat/activity | Existing UI seam reused; PC1 adds typed child binding and headless activity in the native adapter. | Test both default and typed-child consumers when affected; preserve attachments, approvals, streaming, context and voice. |
| Workspace, Projects, CRM/HR, Resources, Memory, Scheduler, Plugins, TestLab, Collaboration, provider/capability/workflow families | Existing prior extraction history remains relevant; this review did not re-certify every family. | Touch only proven consumers or a specifically demonstrated remaining renderer boundary. |
| Whole-repository closure | No fresh semantic exhaustive census was executed by this reviewer. | PC2 must inventory active route/render/deferred/asset closures and classify actual residuals. A native host is not itself an unfinished extraction. |

See the child's `REVIEW_PC1.md` for verified code paths, new versus inherited defects, and hypotheses needing controlled tests. The earlier map and survey are [historical](module-map-v4-historical.md), not an active list of tasks.

Do not invent another module task to avoid closing the current one. If the final census finds no active forbidden rendering edge, report structural decoupling closed, while keeping native authoring and any missing validation dimension visibly qualified.
