# Projects P1 sandbox

This host uses the actual portfolio, hierarchy, overview, five-step editor, package controls
and deletion presentation from `CanDoItAll.Projects.UI`. Scenario state is synthetic.
Files and package effects stay with their production owners; the labeled slot is not file proof.

Use a task-owned port and 1920×1080 at default zoom:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/Sandboxes/CanDoItAll.Projects.UiSandbox --configuration ProjectsUiProof --no-launch-profile --urls http://127.0.0.1:5449
```

The host needs no database or production services. Generate the existing parity CSS before
building (`npm ci --prefix Tailwind`, then `npm run tailwind:build`). Publish to an owned
output directory to verify its independent static assets. The scenarios cover empty and
populated portfolios, failed acquisition, rejected save, unknown seed acknowledgement and
partial deletion. Holding a save permits continued typing; close the modal to reach the
release control and observe that its result cannot reopen the retired editor.
