# Invariants and non-goals

## Compatibility and data integrity

Retain current recognized query serialization and obsolete/unknown-tab fallback to Overview. A requested ID must never cause editing a different entity. Catalog selection does not itself open an editor or become a new URL policy.

Identity remains the default details section. Preserve order: Identity, Runtime, Memory, Images, Project Structure Access, Workspace Tools, Secrets, Process Access, Capabilities, Voice. Typed identities map to these indices internally.

Preserve each current confirmation point and result channel; do not invent a blanket confirmation requirement (team deletion currently differs from agent deletion). Ordinary save keeps the editor open. Successful deletion completes through DialogReference when hosted there, otherwise Saved; do not deliver both. Preserve catalog refresh/selection reconciliation after results.

The Clear action currently creates a blank model even when editing an existing agent; it is not discard-and-reload. Define the editor target/session transition without changing this visible action or silently clearing catalog selection.

Retain mutable draft values and expected-version token throughout a session. Same-target section changes do not replace its draft/edit context. Provider/secret/reference refresh must not erase unsaved fields. Do not turn capability operations that currently save the whole existing draft into staged-only operations.

Use the [behavior matrix](02-behavior-preservation-matrix.md) for preserved, newly necessary, and unresolved cases. Baseline uncertainty is not permission to infer a safer-looking behavior and implement it incidentally.

## Architecture

Keep authoritative state explicit at the correct lifetime. Application use cases do not store component instances, RenderFragment, URLs, DialogReference, or a circuit-wide mutable editor. UI-local state and presentation remain in UI.

Parent isolation and full subtree isolation are separate claims. Existing children may have justified technical dependencies; name and exercise them. No persistence or service location in the three target parent components after their owning phases.

Preserve existing project references and sibling source mode in this child. No feature dependency may be added to AppComponents. No new partial file. Interface/type counts and wrapper prohibitions are replaced by responsibility and evidence review.

## Non-goals

No new canonical routes, detail page, routed overlay framework, physical project extraction, sandbox host, global dialog change, provider-workspace refactor, unrelated module/test cleanup, CSS redesign, or unsupported watch-performance improvement claim.

New project/contract ownership or sibling changes require a separately concrete scope decision. Routine internal naming, focused pure helpers, and genuine same-module ports are normal implementation choices, not repeated permission checkpoints.
