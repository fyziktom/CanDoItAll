# Collaboration UI sandbox

A backend-free interactive host for the same `CollaborationWorkspaceSurface` used by the
production route. No production composition, database, provider credentials or worker
registrations are used. Scenario state is local to the rendered page.

From the repository root, with the sibling Components repository available
under the existing source-mode conventions:

```powershell
npm ci --prefix Tailwind
npm run tailwind:build
dotnet build src/Sandboxes/CanDoItAll.Collaboration.UiSandbox/CanDoItAll.Collaboration.UiSandbox.csproj --configuration Release /m:1
dotnet watch --project src/Sandboxes/CanDoItAll.Collaboration.UiSandbox --no-launch-profile --urls http://127.0.0.1:5187
```

Use a free owned port. Do not reuse the application on 5032. Parity is the only asset mode:
the sandbox links production CSS as content and loads actual BaseLib assets. The CSS
content link creates no compile reference to Web. Missing CSS fails the build with the
generation command. Source/package dependency mode is independent of this asset choice.
For CSS changes run `npm run tailwind:watch` in another terminal.

The selector exposes loading, empty, missing thread, representative data, long subjects
and transcripts, invalid and dirty drafts, admitted saves, owner refusal, failed load,
stale refresh, saved-with-refresh-warning, delayed target and delayed reconciliation scenarios.
Forms validate and mutate deterministic in-memory scenario data. **Complete next delayed
action** releases one admitted save or read in admission order, so older and successor
operations can finish independently. Refresh reconciles accepted writes without replaying them. Linked
context and scheduler actions report their intent in the scenario toolbar.

An admitted reply commits to its captured thread even after navigation. Its completion
cannot clear or unlock the current editor. Effective section/filter changes retire an old
create's navigation; repeated no-op setters do not. The saved identity remains available,
and Refresh reveals the stored result. Changing the scenario disposes its entire isolated
store, releases every pending wait, and suppresses subsequent writes and callbacks.

In **DelayedReconciliation**, enable Unread only, submit a reply, then type the next reply
while the refresh is pending. Complete the delayed read: the unsent reply and validation
context stay attached to the original thread, with a visible explanation even though the
thread has left the list. The production host uses the same retention rule. Explicitly
selecting another thread discards the draft. Clear followed by Refresh permits automatic
selection again; this scenario requires completing that refresh too.

The viewport-owned BaseLib layout keeps the scenario toolbar visible while the real
workspace scaffold owns scrolling. The primary proof viewport is
1600 × 1000. The sandbox does not invent permissions, optimistic concurrency or a server
paging protocol absent from the Collaboration owner.

Lightweight renderer tests are in
[CanDoItAll.Collaboration.UI.Tests](../../../tests/Components/CanDoItAll.Collaboration.UI.Tests/README.md).
See [the boundary and evidence record](../../../docs/architecture/collaboration-ui-boundary.md)
for production proof, graph/watch results and measurement limitations.
