# W3 — runtime quick actions, one-launch approval and web preview

## Move real UI, retain existing execution architecture

Extract the actual quick-action dialog, its native offered actions, explicit script approval,
capability/unavailable states, start/exit/stop feedback, readiness and the whole web-preview
overlay. The existing EmbeddedBrowser and CanvasOverlayDialog remain real children.
Host/executable resolution, typed plans, path authority, environment policy and process
supervision remain at their original owners. [S17-S24, S30]

There is no new in-browser terminal to build. Terminal mode is the existing external terminal
presentation of a compiled plan. Direct, terminal and elevated modes are distinct capabilities;
headless or unsupported hosts must say so, not use an implicit shell fallback. Do not turn
Docker's absence into an unrequested native launch mode or elevate to pass a test.

## Original selection and approval

An offered action is a capability projection, not permission. Capture the exact project
admission, node occurrence and relevant configuration, origin/selection revision, mode and
approval opening before awaits. Validate the original target/configuration at admission.
An A→B→A callback or a recreated node cannot inherit the latest target just because NodeKey
matches. Existing agent execution checks are separate from operator-selected path authority.

The current host awaits a boolean script approval and then passes the captured node to the
launcher. Trace and test stale-node/configuration, navigation and actor changes across this
await. Bind approval to exactly one immutable reviewed operation; do not cache consent across
launches or reinterpret its target after approval. A trusted native plan may safely finish once
admitted, but a not-yet-admitted stale approval must not start a process. [S17, S19, S23]

## Process identity and truthful outcomes

The registry already owns an IWorkspaceProcessSession and obtains a
WorkspaceOwnedProcessIdentity on start. Reuse that identity. The high-level result currently
reduces this to success/message, and the adapter awaits an early-exit window after start.
Test cancellation/failure after process acquisition separately from refusal before start.
Do not state "nothing started" merely because an awaited observation failed. [S19-S21]

Prefer a minimal additive observed-identity/result/expected-session operation on the existing
owner if needed; do not introduce a parallel process registry, generic durable launch platform,
new persisted schema or ad-hoc PID tracking. A process identity is not only its numeric PID.

Stop must act on the original owned session presented by that opening. If another session now
occupies the same node, an old Stop cannot terminate it. The existing registry's post-terminate
reference check is valuable and must remain; it does not by itself prove a queued UI callback
captured the right session before dispatch. Never kill by executable name or arbitrary PID.
Test two nodes, two openings, already-exited sessions, launch/stop races and ownership cleanup.

The registry's current per-node keying is a native contract, not automatic permission to
redesign it across the product. If exact cross-profile/occurrence ownership cannot be achieved
with a bounded additive seam, map the full callers and block only that unsafe action pending
a dedicated repair; do not hide it or declare the family complete while it remains required.

## Readiness, preview and close

Readiness is bound to the accepted run instance and exact URL, not just current ProjectId or
navigation text. A late successful/error result from an older wait in the same page must not
open over a new preview. Retiring a view cancels only its observation; it is not proof of
runtime termination. Preserve real cancellation/exit handling and clean every owned token,
subscription and callback after it unwinds. [S17]

Closing the preview leaves its runtime running. The explicit Stop action terminates only its
owned runtime and closes the matching preview on confirmed success; failed Stop remains visible.
Ordinary web links have no Stop. Reopening, maximize/restore, external Open and the embedded frame
must remain usable at the supported desktop viewport. Preserve current URI/embedding security:
loopback cross-origin behavior must not broaden same-origin or remote sandbox permissions.
Do not replace the EmbeddedBrowser with an unrestricted iframe. [S22, S24]

## Actual native proof

Start a reviewed harmless fixture executable or explicit script in a private owned root via
the real native launcher/process host. For a loopback server, verify serving bytes and process
identity, close without stopping, reopen, then explicitly Stop and verify exit/descendant cleanup.
Preserve an unrelated process/canary. Use no installed user application or unknown command.

Verify pre-dispatch refusal, explicit approval/cancel, double-submit, early nonzero exit,
post-start cancellation, superseded readiness, delayed Stop and replaced same-node session.
Verify original environment allowlists and path/symlink/read-only rules; never inherit secrets
or replace ProcessStartInfo.ArgumentList with shell concatenation.

Use actual available host lanes. Cross-platform policy tests plus truthful unsupported results
are appropriate when a host cannot open a terminal; they are not successful native terminal or
UAC runs. Existing host test requirements remain, but no permission to launch an elevated user
process is granted merely to close this bundle. Linux process-test containers must have an init
process. Report unavailable macOS/desktop/elevation lanes separately. [S24, S27]
