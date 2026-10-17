# Resource stage2 installed EF API correction

The first parent-owned canonical migration Release build failed with CS1061 at
`ProjectDeletionTransactionEntry.cs`: `DatabaseFacade.GetEnlistedTransaction`
was not in scope. No migration had been generated. This is a compile correction,
not a change to admission or cleanup semantics.

The installed EF Core10.0.4 XML declares the exact public method as
`System.Transactions.TransactionsDatabaseFacadeExtensions.GetEnlistedTransaction(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade)`.
`Microsoft.EntityFrameworkCore.Storage` was already imported. Adding
`System.Transactions` would also import its `IsolationLevel`, conflicting with
the existing `System.Data.IsolationLevel` uses. The successor calls the documented
method by its fully qualified static name instead.

The source retains the same left-to-right checks for Npgsql, current EF transaction,
ambient transaction, enlisted transaction, tracked entries, actual Npgsql connection
and closed state. It adds no fallback, exception catch, service lookup, transaction
operation or SQL. The handoff and cleanup body is unchanged.

Exact source predecessor01401B70 and stage2 inventoryA0A82B58 are preserved under
`P05-R1-deletion-entry-ef-api-predecessors`. The separate overlay pins the installed
assembly/XML and successor26D53890. Original inventories and runner5062/selectionB67
remain historical. The new ordinary runner derivative must replace only this selected
source identity and bind this overlay; all346 ordinary cases, stage1 fourteen first,
and four separate browser cases remain unchanged. The browser derivative must bind
the final ordinary prerequisite inventory explicitly.

Validation here is source-only: installed API documentation inspected, exact inverse
replacement equals the predecessor, and MAIN remains at the original hash. The parent
owns transfer, standard Release rebuild, canonical migration generation, exact runtime
discovery/execution and final portability enforcement. No build or runtime success is
claimed by this correction.
