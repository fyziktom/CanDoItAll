# Planning presentation tests

Independent bUnit and pure-data tests for Workbench planning renderers. The project
references the planning UI library and neutral Components only; native admission,
database transactions and coordinator proof live in `CanDoItAll.Tests.Components`.

Run with `dotnet test tests/Components/CanDoItAll.Workbench.Planning.UI.Tests`.
Calendar cases cover queued callbacks, independent instances, read states,
normalized state, timezone transitions and accepted-snapshot CSV/XLSX export.
