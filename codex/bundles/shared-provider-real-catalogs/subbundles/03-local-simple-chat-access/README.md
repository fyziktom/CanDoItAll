# SB03: local Simple Chats access

## Status

- Completed
- Entry gate: Passed. Clean baseline 3b998b363, reproduced in an ordinary browser.
- Closure gate: Passed. Four failing-first cases, 38 component/9 HTTP/3 real UI passes,
  exact source usage and healthy rebuilt hosts. Final proof is indexed in SB03.

## Covered Inputs

- N005 / R5: a local browser on a headless Docker deployment can create and use Simple
Chats. Keep remote, HTTP API and authorized-file permission boundaries intact.

## Objective

Repair normal local browser access without changing the API authentication boundary.

## Prerequisites

- Baseline source/tests read, ordinary-browser denial reproduced and scoped architecture
  snapshot built successfully (snap-20260827172158-9a0e08df; no diagnostics).

## Dependency Impact

- SB01/SB02 catalog and real-runtime proof remains valid. SB02's normal-browser handoff
is reopened until this phase passes. Preserve the two app volumes, providers and 5032.

## Exact Source References

- repo://src/App/CanDoItAll.Web/Infrastructure/LocalOperatorAuthenticationStateProvider.cs
- repo://src/App/CanDoItAll.Web/Infrastructure/InteractiveServerServiceCollectionExtensions.cs
- repo://src/App/CanDoItAll.Web/Composition/LlmChatsUiComposition.cs
- repo://src/App/CanDoItAll.Web/Infrastructure/HttpFileAccessContextProvider.cs

## Root Cause

The identity provider couples browser privilege to OS desktop capabilities and literal
loopback transport. Docker is headless and its local browser transport is the NAT gateway.
The scoped provider owns identity; HTTP authentication remains a separate boundary.

## Deliverables

- Scoped browser access repair, exact ingress options, regression tests and live proof.

## Implementation Steps

1. Record failing headless/explicit-gateway circuit tests before changing production.
2. Remove the OS-interactive gate. Add validated exact UI ingress addresses, empty by
   default; require trusted original AND effective addresses, including mapped IPv6.
3. Preserve authenticated principals, HTTP users, scope restrictions and dev endpoints.
4. Deploy only to loopback-published test containers with explicit inspected gateway trust.
5. Create/save/reload and execute new chats through a browser without injected JWTs.

## C# Architecture Entry Review

Keep identity decisions in the existing Web infrastructure owner. One typed options
class is justified by a real deployment configuration boundary. No new interface,
project, package, inheritance layer or UI/business-policy bypass. No changes to shared
components or general forwarded-header/dev-endpoint trust. DI remains scoped.

## Validation Depth

- Proof tier: Governed.
- Invariants: LOCAL-UI-ACCESS and API-BOUNDARY.
- Focused component identity/configuration/composition and API authorization tests only.
- Freeze each expected count from discovery before execution; zero selected tests fails.
- Negative cases: untrusted transport, spoofed forwarding, missing addresses/context,
  malformed/wildcard trust, authenticated read-only user, circuit isolation, HTTP 401/403.
- Rebuild exactly 5210 and 5212. Plain-browser creation and real shared OpenAI/Ollama
  replies; source usage records, healthy hosts and anonymous API rejection.
- Invalidation keys: identity provider, options/DI, deployment config or runtime access.
- Broad-gate decision: no full suite; this does not change catalog/routing/domain code.

## UI Composition Contract and Browser Validation Logging

1920x1080 existing Simple Chats Definitions/Conversations and editor dialog. No markup
redesign. Verify visible create/save/send actions, readable feedback, dialog scrolling,
reload persistence and actual replies. Capture before/after screenshots and DOM.

## Acceptance Checklist

- Plain local browser can read, create, save, reload and execute shared-provider chats.
- Scoped identity is circuit-local; remote/API anonymous calls remain denied.
- All security negative cases and real two-host proof pass.

## Progression Gate

- All acceptance checks must pass before normal-browser handoff is restored.

## Proof Required

- bundle://proof/SB03/manifest.md and bundle://proof/SB03/semantic-invariants.md own red/green,
source hashes, inspected UI, deployment and API-negative proof. Do not close until all
normal-browser cases pass; a token-injected browser is not acceptance evidence here.

## Do Not Do and Reopen Triggers

Never disable API auth, grant all API scopes, trust all private networks, or infer trust
from Host/X-Forwarded-For alone. Do not expose a trusted anonymous ingress publicly.
Reopen on ordinary browser denial, remote privilege elevation or missing real replies.
