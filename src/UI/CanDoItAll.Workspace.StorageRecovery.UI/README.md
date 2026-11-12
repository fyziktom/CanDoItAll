# Workspace Storage Recovery UI

The actual two-feed dialog and session depend only on neutral Recovery contracts and
the real BaseLib components. The host supplies an owner and controls dialog lifetime.
The session separates cancellable observations from accepted commands, retains safe
acknowledgements before optional refresh and never replays a command on read failure.

Use the independent `CanDoItAll.Workspace.StorageRecovery.UiSandbox` for local rendering
work. See [the boundary record](../../../docs/architecture/workspace-completion-ui-boundaries.md)
and the focused test project under `tests/Components` for scope and proof.
