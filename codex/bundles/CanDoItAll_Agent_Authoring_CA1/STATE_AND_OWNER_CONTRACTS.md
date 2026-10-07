# State, native effects and non-regression contracts

## Distinct identities

Keep profile/host authority, catalog record identity, editor lifetime, rendered callback origin,
submitted configuration and native result distinct. A row index, name, fresh reload or latest
selected ID is not a substitute for the origin the operator acted on. A-B-A is a new lifetime.
Opaque fingerprints/tokens are compared for equality, never numerically ordered.

Capability identity uses the native kind/key normalization and ID; update fingerprint is preserved.
Team metadata and membership are distinct operator actions even if persisted in one record. Do not
reuse a returned latest version while retaining a conflicting older whole-record payload.

## Reads and forms

Use bounded independent reads; catalog refresh should not recreate acquired dirty editors. Same
successfully acquired target can be a no-op, while a failed or incomplete acquisition must retry.
Capture input on actual input events where necessary for Enter/footer save. Preserve parse-invalid
text, field messages and stable context across section changes. Do not equate a rendered heading
with an acquired editable target. Deferred DataGrid/templates capture their actionable origin in
the template invocation, not once before asynchronous rendering.

Late success/error/finally/focus/notifications must be fenced. Dispose request resources after
owned tasks unwind. Closing a view cancels its reads, not a proven native commit. Do not expose
scoped services after their scope ends. No global close-all, singleton draft or fire-and-forget
mutation as a lifecycle fix.

## Effects and outcomes

| Effect | Required evidence |
|---|---|
| Authoring Save/Create | Actual owner rejection, accepted identity/fingerprint, or genuinely unknown result |
| Setup test | Original config/input and diagnostic/effect outcome; not saved or verified proof merely because it passed |
| Agent assignment | Existing-agent whole-draft policy versus new-agent staging; canonical post-save read-back |
| Verify | Original native verification receipt/version and separate proof publication; preserve A2 draft |
| Team membership | Native exact target/selected IDs, no permission widening, distinct subsequent refresh |

Known commit plus failure of notification/read-back remains committed. A speculative read by name
cannot manufacture a commit receipt. An unknown external effect or database acknowledgement must
not offer blind retry. Use current native contracts; if insufficient, a bounded additive seam may
be introduced in the correct owner after proving consumers. Never require a universal operation
framework as a prerequisite for extracting renderers.

## Safety boundaries

Retain native MCP/tool/skill validation, host path restrictions, binding resolution, network policy,
execution limits and approval rules. Test setup can be effectful, including real local process
execution. No startup/render/upload side effects. No raw secret in generic operation history,
URLs, snapshots, screenshots, stdout or serialized commands. Canonical config may contain secret
references and protected bindings: disclose only their intended UI projection.

Renderer locks are not authorization. Creating a capability does not assign it, assigning it does
not by itself authorize every runtime call, and team membership does not add rights. Existing
operator/context checks and native tool approval remain in force. No private/public provider
fallback, no automatic AllowAll, no new provider support or retired capability migration.

## Published asset and graph boundary

New source/published sandbox must use actual markup and children, default DI only for rendering,
and deterministic private fixtures. A forwarding wrapper around the old heavy component is not
completion. Preserve moved CSS, Tailwind scan inputs, JS, icons/fonts and actual dialog behavior.
Measure evaluation/watch paths and visible edits; a claimed count alone is not a speed guarantee.
