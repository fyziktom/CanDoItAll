# Agent chat execution lifetime

## Decision

An accepted agent operation belongs to the application, not to the Blazor circuit
that submitted it. A browser refresh detaches a view; it is not a cancellation.
The application retains an independently created execution scope until the run is
terminal, including time spent awaiting approval or explicit continuation.

The floating window, navigation subscriptions, selected window and unsent composer
draft remain circuit-owned. A bounded application-owned catalog retains the owner,
profile lifetime, agent, conversation, activity stream, execution state and safe
return location needed to reconnect. It does not retain a component, circuit
service provider, authentication provider or browser callback.

## Responsibility and dependencies

| Responsibility | Current owner | Target owner |
| --- | --- | --- |
| Observe the current application surface | Circuit context registry | Unchanged; freeze its strict observation at submission |
| Resolve agent authority and construct invocation | Turn context capture service | Same policy, using the frozen observation in the owned execution scope |
| Execute and retain transient continuation context | Circuit workspace factory | Independently owned execution scope |
| Observe progress | Singleton activity stream and circuit reader | Unchanged; reconnect readers to the original stream |
| Identify the submitting user | Web authentication boundary | Typed owner provider implemented by Web |
| Discover detached work | Circuit active-chat registry | Owner/profile-filtered application catalog projected into the circuit registry |
| Restore working context | No restoration | Safe module-generated return route; routed host publishes fresh context |

Core contracts remain independent of Web, DI containers, provider SDKs and database
implementations. Module composition creates and disposes execution scopes. Execution
services receive explicit ports and immutable inputs. No new runtime partial class
or project is needed: the existing Core and AgentFramework module boundaries suffice.

## Ownership, authority and navigation

Ownership uses verified issuer and subject, with a separate verified trusted-local
identity. When both API authorization and user authentication are explicitly disabled,
all browsers use a distinct shared workspace owner for that no-users deployment.
Authentication session IDs, circuit IDs and browser IDs are not ownership keys.
Authenticated users can rediscover their work from another browser. An anonymous
caller in an authenticated deployment cannot adopt shared workspace ownership.
Listing and control must resolve the current caller and profile again.

Ownership is a discovery/control boundary, not an execution permission grant.
Existing canonical agent authority, project lifetime, tool admission, approval and
profile-generation fences remain in force. A refreshed browser cannot republish a
retained attachment or binding as fresh authority. Profile changes must never retarget
an admitted operation to the replacement database.

Return locations are bounded local routes built from known module bookmark state.
They preserve the admitted source/view/selection, not the page the browser subsequently
visits. Arbitrary URL query text, credentials and free-text searches are excluded.
External URLs, network-path references and backslashes are rejected. Reopening
navigates to the location and lets the normal routed owner establish current access.

## Lifecycle

Admission freezes the request and strict context observation before asynchronous
authority lookup. The owned scope executes the existing workspace entry points and
publishes into the existing activity stream. A second simultaneous turn for the same
conversation is rejected rather than replayed or silently queued.

Disconnecting, hiding or disposing a view only unsubscribes that view. Pending
approvals and continuations retain their original runtime context. Explicit control
uses the retained executor and current owner check. Terminal execution scopes are
disposed after completion publication; bounded recent chat metadata remains available
for reconnect. Active work is never evicted to make room for another request.

This decision guarantees browser/circuit independence within a running application.
It does not replay arbitrary tool calls after a server crash. Existing persisted run,
receipt and startup recovery contracts continue to describe interrupted server work.

## Alternatives

Making the existing floating coordinator or workspace factory singleton would share
UI state and scoped authority across users. Keeping the old circuit alive would retain
components and stale authentication. Neither provides a valid lifetime boundary.
Persisting only a window ID would repair appearance while leaving execution vulnerable.
A new distributed queue would add deployment and exactly-once claims that this browser
refresh requirement does not need. The selected patterns are an owned execution
session, an explicit scope factory and a detached read projection.

## Acceptance and validation

Tests must dispose the entire submitting DI scope, not merely rerender a component
using the same provider. Independently test continued execution, reconnect to the same
stream, owner/profile isolation, duplicate admission rejection, retained approval
context, explicit cancellation, terminal cleanup and host shutdown. Composition proof
must exercise the real module registrations. Browser proof must refresh the actual
floating chat while a deterministic operation is held, then reopen its working view
without dispatching another turn.

Test execution for this change runs in GitHub CI at the user's request. Local builds
and reviewed portability/rendering static enforcement remain separate validation.
