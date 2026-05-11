# Documents wrapper and spreadsheet executor

## Status

- `Completed`

## Objective

- Add `CanDoItAll.Tools.Documents` as the app-owned document wrapper and implement the first spreadsheet operations needed by workflows.

## Success Criteria

- ClosedXML is referenced only by `CanDoItAll.Tools.Documents` and wrapper tests.
- Wrapper can inspect workbook sheets, read cells/ranges, apply batched writes, save workbooks, and render range data as Markdown.
- Spreadsheet executor uses the wrapper through app-owned models and emits JSON payloads suitable for workflow edges.

## Covered Inputs

- R04, R05, R15, R16.

## Prerequisites

- Subbundle 01 contracts compile.
- Reference repo ClosedXML version is confirmed as `0.105.0`.

## Exact Source References

- `C:\programovani\Aqualectra\pve-invoicing-connector\PVEInvoicing\PVEInvoicing\Import\ExcelImportService.cs`
- `C:\programovani\Aqualectra\pve-invoicing-connector\PVEInvoicing\PVEInvoicing\Export\InvoiceExportService.cs`
- `C:\repositories\CanDoItAll\CanDoItAll.slnx`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`

## Deliverables

- New `CanDoItAll.Tools.Documents` project with ClosedXML package reference.
- Spreadsheet wrapper models and service implementation.
- Spreadsheet workflow executor settings and result models.
- Unit tests creating, reading, writing, and Markdown-rendering a real `.xlsx`.

## Dependency Impact

- Subbundle 04 depends on the spreadsheet executor to prove artifact/output behavior.
- Subbundle 06 depends on real workbook scenarios.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add the new project to `src` and the solution.
2. Add ClosedXML only to the wrapper project.
3. Implement spreadsheet service methods for workbook summary, cell/range reads, batched cell/range writes, and Markdown rendering.
4. Add spreadsheet executor settings/result types and implementation.
5. Register wrapper and executor in DI.
6. Add unit tests with a real temporary workbook.
7. Scan for ClosedXML leakage.

## Scope Exceptions

- PDF and DOCX wrapper capabilities are explicitly deferred.
- Complex Excel formulas, pivots, charts, and macros are out of scope for this first executor.

## Do Not Do

- Do not expose ClosedXML types from public CanDoItAll APIs.
- Do not use ad hoc CSV/string parsing as a substitute for workbook operations.

## Acceptance Checklist

- Wrapper can read a known cell and range.
- Wrapper can write multiple cells and persist the workbook.
- Markdown output escapes table characters and represents blank cells predictably.
- Spreadsheet executor validates required path/sheet/range settings.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter Spreadsheet`
- `Select-String` scan for `ClosedXML` references outside the wrapper and tests.
- Sample workbook under bundle artifacts if useful for scenario proof.

## Browser Validation Logging

- `N/A`

## Progression Gate

- Subbundle 04 may invoke spreadsheet executor only after wrapper tests and ClosedXML boundary scan pass.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
