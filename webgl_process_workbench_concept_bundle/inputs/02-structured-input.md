# Structured input

## Problem framing

The user wants a **prepared execution bundle** for a concept branch that evaluates whether the repository's dense process diagrams should be explored through a **WebGL-backed 2.5D workbench** in Blazor.

## Core output requested

- initiative-style bundle with detailed subbundles,
- architecture direction for WebGL in Blazor,
- universal WebGL wrapper library as phase 1,
- new dedicated sandbox project as phase 2,
- template-backed concept scenes,
- interactive node move/connection changes,
- screenshot and semantic proof strategy,
- workbook with stories/features/traceability,
- corrective subbundles and architecture review gates.

## Normalized raw notes

| Raw note ID | Input note | Expected proof |
| --- | --- | --- |
| IN-01 | Prepare a detailed execution bundle and deliver it as a zip artifact. | bundle structure + zip presence |
| IN-02 | Mirror the repository's existing bundle and subbundle conventions instead of inventing a new format. | bundle layout consistency |
| IN-03 | Decide the best practical style for using WebGL in Blazor for process-diagram exploration. | explicit architecture decision |
| IN-04 | Current process diagrams are hard to read in 2D and the concept should test whether a 3D or 2.5D approach improves legibility. | readability review notes + screenshots |
| IN-05 | Phase 1 must add a component library that wraps WebGL with a basic rendering and interaction system comparable to the current canvas workbench. | new WebGL library skeleton + typed contracts |
| IN-06 | The WebGL library must stay universal and reusable outside the Processes module. | no process reference from library |
| IN-07 | Codex must then add a new sandbox project dedicated to the WebGL concept. | new sandbox project in solution |
| IN-08 | The sandbox must display a selected template process taken from the current process-template pack. | template-backed sandbox scene |
| IN-09 | The sandbox should allow switching between templates. | template selector + route/query state |
| IN-10 | The sandbox must support manipulation, including moving a node and changing a connection. | interactive in-memory editing |
| IN-11 | The work happens on a separate concept branch and should avoid destabilizing the production 2D process workspace. | production workspace left untouched |
| IN-12 | Codex must validate through screenshots because Playwright MCP cannot easily drive canvas or WebGL directly. | browser proof playbook + screenshot review |
| IN-13 | The base library should expose an interface or contract that lets Playwright MCP test semantic WebGL actions such as moving nodes. | automation bridge contract |
| IN-14 | The bundle should include an XLSX workbook covering user stories, features, and traceability. | xlsx workbook present |
| IN-15 | Subbundles must be phase-based and must force architecture reviews between phases. | review gates + stop rules |
| IN-16 | The bundle should include emergency/corrective subbundles that repair or refactor the concept before downstream work continues. | corrective playbooks |
| IN-17 | The current Process module and current 2D canvas implementation are the structural reference for IDs, semantics, and interaction patterns. | contract reuse notes |
| IN-18 | The concept should be able to compare multiple template complexities rather than a single trivial example. | simple/medium/dense template set |
| IN-19 | Codex should be able to validate actual state changes even when pixel-only validation would be weak. | semantic snapshot proof |
| IN-20 | The final result for this task is the prepared bundle itself, not implementation code in the repository. | prepared-only execution report |

## Non-goals inferred from the request

- No production replacement of the existing Processes 2D workspace in this bundle.
- No persistence of concept edits into the production process editor.
- No requirement to prove a final product decision immediately; this is a concept branch.
- No assumption that unrestricted 3D is automatically better than 2D.
