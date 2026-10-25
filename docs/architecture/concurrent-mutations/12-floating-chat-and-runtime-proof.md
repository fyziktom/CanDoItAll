# Floating chat and runtime proof repair

## Design before implementation

The floating conversation must reserve its main space for messages. Routine saved-receipt
review and thread metadata belong in a controlled conversation-details dialog; context
permissions and follow/detach controls belong in a compact context dialog. Active errors,
retained submissions and approval decisions remain in the conversation. Opening either
dialog does not read receipts, submit prompts, change context or remount the composer.
The existing native owners continue to validate typed origins and perform every effect.
The lightweight UI owns only disclosure state. Full-page chat keeps its existing density.

Use the existing shared Dialog, Button, Cluster and conversation components. Compact
presentation is opt-in; it does not add service dependencies to renderers. Keep the
conversation title editable, label icon controls, restore focus on dialog close and close
details when the selected thread changes. Validate long text, minimum supported window
size, pending approvals and active response updates.

Resize belongs to OverlayLib. Native CSS resize currently competes with ResizeObserver
and server geometry echoes. Browser geometry must remain authoritative during a pointer
gesture, with one final geometry publication after release. Delayed acknowledgements must
not overwrite a newer local size. Explicit reset/minimize/hide must still take effect,
and disposal must remove gesture listeners and pending callbacks. No per-pointer-move
Blazor round trip, new layout service or feature-specific resize implementation is needed.

The reported process reached runtime QA with passing product checks, then restarted its
preview host for a service-worker update check. That last browser evaluation did not
record the host URL, so the current-execution lifecycle gate correctly rejected the
completion claim. Keep host/startup-receipt correlation and latest-lifecycle validation.
Improve the browser driver's generic QA guidance: every restart requires fresh navigation
to the returned URL and browser evidence before the matching stop. Cached/offline proof
is additional evidence, not a substitute for the current host lifecycle. Investigate the
recorded tool evidence before retrying only the affected QA step.

## Validation contract

- Component tests: focused disclosure, preserved draft/editor, context intent identity,
  saved-receipt read guards, pending approvals and full-page compatibility.
- Overlay runtime regression: first resize, delayed echo during and after gestures,
  reset, drag, minimize/restore, container resize and disposal; real-browser resize with
  delayed acknowledgements and streaming-style content updates.
- Process tests: latest start still requires its own matching browser and cleanup proof;
  generic QA/recovery guidance; native retry and resulting durable outcome when feasible.
- Build affected production projects and discover the exact owning test filters before
  execution. Run rendering-register, portability-static and documentation validation.
  Record source identities and browser dimensions/results; no invented success claims.

Preserve the ordinary 5032/7271 instance and its data, the existing shared test database
fixture and the unmerged SPA branch. Use standard Debug/Release outputs. Store small
diagnostic proof in the ignored compact-chat-process-20261024 artifact directory; record
and clean up any owned temporary host.
