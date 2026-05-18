# Normalized Requirements

| ID | Requirement | Observable success criteria |
| --- | --- | --- |
| R1 | Extract the two source packs into text-readable bundle artifacts. | `inputs/extracted/source-index.json` lists both projects and extracted markdown/json artifacts. |
| R2 | Normalize source truth into time-based groups. | Each project has at least five `Sxx` groups in `source-truth/*-time-sliced.md`. |
| R3 | Preserve financial and operational detail. | Source truth includes unit economics, investment ramps, CAPEX/OPEX, staffing, construction/facility, cash-flow, and scenario data. |
| R4 | Generate deep project structures from analyzed content. | API runner parses headings into stage, category, and fact nodes with parent/child depth greater than three levels including file evidence nodes. |
| R5 | Load only through APIs. | Evidence includes project create, lease, node create, link create, readback, external source upload, ingestion, consolidation, review decisions, and snapshots. |
| R6 | Perform human-like review decisions. | Pending review items are approved, rejected, or deferred using source-system and source-truth rules captured in evidence. |
| R7 | Validate recall against source truth. | Every manifest recall probe is evaluated for context, expected stage locator, and required terms. |
| R8 | Repair implementation only on evidence. | Any C# change must cite failing evidence, explain root cause, and include build/test plus rerun proof. |
