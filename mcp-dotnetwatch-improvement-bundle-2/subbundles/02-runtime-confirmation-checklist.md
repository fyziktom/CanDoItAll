# Sub-Bundle 2: Runtime Confirmation Checklist

- [x] add a runtime hot-reload generation token in the web app
- [x] expose the token from `/_dev/runtime`
- [x] teach the health probe payload to parse the token
- [x] keep `watch.pendingChange` true until runtime generation confirmation
- [x] make `RevisionConfirmed` depend on runtime confirmation for supported apps
- [x] add a log-level wait condition for reported hot reload completion
- [x] add unit and integration coverage for the new semantics

Evidence:

- `08-implementation-results.md`
- `tests/CanDoItAll.Mcp.DotNetWatch.Tests/AppSessionLifecycleTests.cs`
- `artifacts/managed-mcp-pageheader-fullflow.json`
- `artifacts/managed-mcp-projects-page-fullflow.json`

Done:

- managed wait no longer reports a confirmed hot reload before runtime generation confirmation
