# Spreadsheet Internal Agent Skill

Use this skill when an internal agent reads, validates, or produces spreadsheet-like artifacts.

Work rules:

- Inspect workbook structure before summarizing or editing.
- Preserve tab names, headers, formulas, and data types unless the step explicitly asks for changes.
- For generated spreadsheets, validate the workbook can be opened or inspected and that expected sheets and key cells exist.
- Keep reconciliation outputs explicit about matched records, unmatched records, and uncertainty.
- Do not claim spreadsheet proof from a text-only summary.

Use assigned document/spreadsheet workspace tools when available.
