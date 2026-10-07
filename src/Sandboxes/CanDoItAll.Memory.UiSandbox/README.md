# Memory UI sandbox

Run the actual seven-tab workspace without module/runtime DI, EF, PostgreSQL, provider
transports, a vault or external accounts:

```powershell
dotnet run --project src/Sandboxes/CanDoItAll.Memory.UiSandbox --configuration Debug --no-launch-profile -- --urls http://127.0.0.1:5088
```

The host links the repository's generated theme as content; it has no Web ProjectReference.
If that stylesheet is missing, run `npm ci --prefix Tailwind` and `npm run tailwind:build`
from the repository root. BaseLib supplies its normal fonts and assets. Publishing uses the
same linked content and static-web-assets pipeline.

Scenarios cover initial loading, empty/realistic/103-provider catalogs, unavailable/partial
reads, accepted and unknown queries, and registered/missing/blocked/iframe/external provider
UI. The only iframe endpoint is an owned local fixture. Query/status/save/read controls hold
the next actual owner-port call; Release settles owned waits. Reset retires the controller
and releases the original store. Retired store contents remain inspectable (last four stores).
Unsupported ingestion, feedback, event acknowledgement and cancellation remain refused.
Accepted results and ledger fixtures are explicitly synthetic, never production capability
proof. Operation IDs are unique per scenario action; catalog IDs and fixture timestamps are fixed.

Text edits are immediate. Refresh and tabs retain raw fields, unfinished tag input and
incomplete numeric text. Selection or Discard draft explicitly starts a new lifetime.
The real page owns vertical scrolling, including a large catalog; no nested scroll redesign.

See [the boundary and proof record](../../../docs/architecture/memory-ui-boundary.md).
