# Spreadsheet Internal Agent Skill

Use this skill when an internal agent reads, validates, or produces spreadsheet-like artifacts.

Work rules:

- Inspect workbook structure before summarizing or editing.
- Preserve tab names, headers, formulas, and data types unless the step explicitly asks for changes.
- For generated spreadsheets, validate the workbook can be opened or inspected and that expected sheets and key cells exist.
- Keep reconciliation outputs explicit about matched records, unmatched records, and uncertainty.
- Do not claim spreadsheet proof from a text-only summary.

Workbook workflow:

- Use `workspace_inspect_spreadsheet` for a compact preview of incoming `.xls`, `.xlsx`, `.csv`, or `.tsv` files.
- Use `workspace_spreadsheet_summary` for `.xlsx` workbook sheet names, positions, used ranges, and dimensions before editing.
- Use `workspace_read_spreadsheet_range` for tables and `workspace_read_spreadsheet_cell` for exact key cells.
- Use `workspace_write_spreadsheet` to create or update `.xlsx` workbooks. Put values in `cellWrites` for individual cells and `rangeWrites` for rectangular tables.
- For every `rangeWrites` item, the outer `values` count must fit within the rows of `rangeAddress`, and every inner row must fit within its columns. Use empty rows with the same column count as the table, or omit unused trailing cells.
- If a spreadsheet tool returns a retryable input failure, correct the reported address, row, or column mismatch and retry. A retryable result means the tool is available; do not report spreadsheet creation as unsupported.
- Use one logical worksheet per concern: source data, assumptions, calculations, and summary. Keep sheet names short and stable, for example `Quotation`, `Assumptions`, `Margin`, and `Summary`.
- After writing, inspect the workbook and read back representative cells or ranges. Do not report success until the workbook exists and the expected sheets, headers, and formulas are visible.

Formula rules:

- Values beginning with `=` are stored as Excel-compatible A1 formulas by `workspace_write_spreadsheet`.
- Use `workspace_spreadsheet_function_catalog` when building formulas. It returns common function names, syntax, examples, and notes.
- Keep formulas readable and auditable. Prefer helper columns over deeply nested formulas.
- Use `IFERROR` around ratios that can divide by zero, such as margin formulas.
- Use absolute references like `$B$2` only when a formula will be copied and the referenced cell must not move.
- Store uncertain or user-supplied assumptions on an assumptions sheet and reference those cells from calculations.

Common formula patterns:

- Total: `=SUM(D2:D20)`
- Gross profit: `=Revenue-Cost`
- Gross margin: `=IFERROR((Revenue-Cost)/Revenue,"")`
- Conditional total: `=SUMIFS(D2:D20,A2:A20,"Hardware")`
- Lookup price: `=XLOOKUP(A2,Items!A:A,Items!D:D,"Missing")`
- Scenario label: `=IFS(E2>=0.4,"High",E2>=0.25,"Medium",TRUE,"Low")`

When converting a PDF, DOCX, or other document into a workbook:

- Use `workspace_convert_document` first and extract source facts from the returned markdown/output path.
- Keep source facts separate from calculations. Put extracted quotation rows on a source-data sheet and formulas on a calculation sheet.
- Include the source path, extraction assumptions, currency, units, and missing-data notes in the workbook.

Use assigned document/spreadsheet workspace tools when available.
