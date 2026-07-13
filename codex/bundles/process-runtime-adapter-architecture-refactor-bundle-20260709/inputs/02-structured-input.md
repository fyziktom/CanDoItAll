# Structured Input

## Intent

Prepare an implementation-ready architecture bundle.

## Main Change Area

`AgentFrameworkProcessExecutionAdapter` and adjacent process runtime/driver/MAF receipt integration.

## Must Solve

- Partial-class adapter growth.
- Mixed responsibilities in adapter.
- Domain leaks in generic process runtime/dispatcher/MAF core.
- .NET lifecycle/tool-plan behavior not isolated behind process drivers.
- Branch/receipt/repair root causes from GPTPro analysis.
- Template/artifact coverage beyond the observed blocked process.

## Must Not Do

- Do not implement production changes during preparation.
- Do not add production source edits outside the bundle.
- Do not weaken gates or required receipts.

