# Normalized Requirements

| ID | Requirement | Source notes | Acceptance signal |
| --- | --- | --- | --- |
| R001 | Preserve the processes page as the reference pattern and upgrade it to shared compact stat/action tooltip primitives. | N001, N004, N005, N006 | `/processes` large-screen screenshot shows a compact header; tooltip checks show 2-second delayed stat/action help. |
| R002 | Add shared BaseLib primitives for compact badge stats and icon-only page-header actions with default 2-second tooltip delay. | N003, N004, N005, N006 | Build passes; migrated pages use the shared components rather than hand-rolled tooltip/status/action markup. |
| R003 | Convert production page headers with first-screen stat rows from large tiles to compact badge stats, moving them into the header where practical. | N002, N006, N008 | Representative page screenshots show header stats occupying badge height instead of a separate large tile row. |
| R004 | Convert CRM-HR tab pages from large stat tiles to compact badge stats. | N002, N007, N008 | CRM-HR tab screenshots show secondary tabs plus compact stats without large stat cards. |
| R005 | Convert tab/subpage stat rows that use large `SummaryTiles` or `MetricCard` in the identified production surfaces to compact badge stats. | N002, N007 | Build plus route/component screenshots show no large stat tiles in the targeted tab summary areas. |
| R006 | Header add/open/refresh/similar actions changed by this bundle are icon-only with accessible labels and tooltips. | N003, N004, N005 | Screenshots show icon-only header actions; DOM/tooltip checks show accessible labels and delayed tooltips. |
| R007 | Validate on large screens with screenshots and record proof paths and closure status. | N008 | Execution report includes commands, routes, viewports, screenshots, and raw-note closure. |
