# Trust and integration safeguards

## Native owners do not migrate into renderers

Workflow catalog, version/head persistence, component/Prompt storage, runtime admission, actual
providers, secret value resolution, project files and external-response authorization retain their
current owners. UI may consume a narrow safe read projection or typed host effect, not a provider
registry/DbContext/service locator disguised as a view model. Reusing a model assembly is acceptable
only after checking its transitive graph and exposed data.

## Configuration rendering

Inventory SettingsRendererHost, WorkflowSettingsRendererSource and its actual registrants at entry.
Application and bundled-plugin custom renderer identities remain checked against key, owner, trust
and schema. Invalid explicit selection must not fall back to an untrusted generic renderer. Do not
load arbitrary types/assemblies named by manifests. Move the image settings markup and pure state
presentation; keep provider reads and trusted registration in native host composition. Real generic
configuration UI and appropriate metadata-only Secret references must be demonstrated in the sandbox.

## Prompt and components

The native Prompt Gallery remains authoritative for compatibility and immutable prompt versions.
A caller opens a picker for one document/node or new-component operation. After every relevant await,
verify that the original binding target still exists and is current. A committed new component is
retained even if its old node disappeared; present the result without attaching it elsewhere. Do not
make the next click recreate an already accepted component because its parent callback failed.
A preview or component binding must not unexpectedly save the entire workflow or user Prompt.

## Shared providers

Use published DisplayName for presentation and the exact opaque route ID for persistence/inference.
Do not normalize route IDs using local provider heuristics or replace them by display strings. Keep
default, suggested, saved unavailable, price and Thinking data coherent from one accepted snapshot.
Native source-managed restrictions remain enforced; failed imports do not route to a personal default.
A component snapshot pinned to one model/version must not silently follow an updated current catalog.

## Project Structure, runtime and files

Only the native authority factory grants project access for UI preview. It is not an editable field.
Use current trusted actor and original project lifetime. Actual node/asset/file tools retain required
approvals, operation receipts, root restrictions and safe content grants. Test-only deterministic
upstreams can produce requests, but tests must inspect exact project/node/path/content before approving.
Deny unexpected requests. A preview simulation must remain explicitly selected, labeled and limited to
the admitted simulation plan; it is not evidence that its replaced executor actually ran.

## History and external responses

Run/event detail presentation is separate from Provider Request History and canonical content access.
Preserve existing safe-formatting and bounded payload policies. Metadata permission does not imply
content permission. No secret-bearing exceptions, automatic transcript prefetch or authorization tokens
in URLs, DTO dumps, screenshots or public bundle evidence. Human/external replies and retries remain
bound to the exact native pending request and actor; a stale dialog must not respond to its successor.
