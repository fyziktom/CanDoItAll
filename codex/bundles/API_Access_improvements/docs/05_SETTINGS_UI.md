# 5. Blazor Settings integration

## 5.1 Keep the existing interaction model

Extend the current `/settings` page and its `api-access` tab. Do not wrap the page/application/router in a new global authorization requirement, add a general login redirect, or replace direct server calls with HTTP calls to the same instance. The existing trusted local operator is an explicit administrative capability, not an instruction to make every anonymous HTTP caller an administrator. [D08, D09, D13]

Use the current shared CanDoItAll components. Repository guidance says the application no longer uses Radzen. Consult the Components MCP for existing contracts where needed; do not copy component library implementations or introduce a new form framework. [D16]

Keep feature state, mutation admission and authoritative read-back in the owning host/service. Renderers should not query persistence, invent an admin principal, manage JWT cryptography or write configuration. Follow the current UI-component-seams guidance without launching a separate unrelated extraction program.

## 5.2 Proposed content of API access

### Effective host configuration

Show business API state, JWT authorization, user login, HTTP access administration, safe issuer/audience/lifetime information and whether required credentials are configured. Show the configured admin as configuration-owned only on this trusted/administrative surface. Do not display its hash or the signing key.

Use explicit wording: “HTTP access administration is disabled. Trusted local administration is still available.” Distinguish this from “Current caller has no administration permission.” A disabled remote surface is not a broken local UI.

When the business API is intentionally open, prominently show: “API authorization is disabled. User permissions do not isolate anonymously accessible business operations.” Do not present account checkboxes as an active protection layer in that mode. New user/session controls can be unavailable with a clear configuration reason; do not activate a mock administrator automatically.

Master switches are read-only effective configuration in v1. Provide copyable **option names** and nonsecret instructions, not an HTTP setter for signing keys or self-exposure. Configuration changes require restart; do not show an optimistic toggle that did not change endpoint mapping.

### Machine tokens

Retain the existing `ApiTokenAdministrationPanel`, scope picker and metadata dialog. Improve shared scope validation/labels and distinguish broad compatibility grants from explicit sections. Preserve one-time token display, list/revoke/delete, expiry and pagination.

Label `api` as broad ordinary API access, not as “administrator.” Explain that `api.tokens.issue` on a legacy machine credential no longer independently grants remote access administration. If that reserved scope remains visible for compatibility, do not imply it can replace an administrative login.

### API users

Add a small `ApiUserAdministrationPanel` or equivalent module-owned feature. Reuse a list/detail layout with username/display name, enabled/disabled badge and an effective scope summary. The editor contains bounded username/display-name fields, an enabled switch and the same catalog-driven capability selector used by tokens, filtered to allowed ordinary-user grants.

New-user creation includes a password field. Editing an existing account does **not** load its password, hash or a placeholder that could be submitted as a new password. Password reset is a separate deliberate action with confirmation and an empty write-only password field. No self-registration link or email field is required.

Clearly state the consequence before save/reset/disable/delete: existing sessions for that user will stop working. Removing permissions is not deferred until token expiry. A reset must clear the entered password from component state after success and when cancelling/closing the editor.

Use explicit loading/busy/validation states and prevent duplicate submission while a mutation is in progress. Keep error messages safe. Re-read canonical safe account state after a successful write and keep dirty-edit/concurrency handling consistent with existing seams. A read-back failure after a committed mutation must not encourage a blind second create or claim the account was not created.

Do not allow the configured administrator to be deleted, renamed into an ordinary account, or reset using ordinary user controls. Show how the deployment operator changes it, without surfacing secrets. A normal account cannot turn itself into an administrator through form state or raw HTTP.

## 5.3 API-only UI contract

The alternative client only needs login, `me`, logout and the usual protected APIs for normal work. It can use `me.scopes` to avoid showing unusable sections, but server enforcement is authoritative. It does not need to fetch user inventories or token administration to initialize normal navigation.

For administrative screens it logs in as the configured admin and uses the independently enabled management routes. Same-origin proxy deployment avoids an unnecessary CORS burden. A cross-origin developer UI requires an explicit allowed origin, not an application-wide permissive response-header hack.

On 401, clear the expired/revoked bearer and show login; on 403, show insufficient permission rather than repeatedly asking for the same password. On a disabled administrative API's 404, use status discovery to show “not enabled” rather than guessing credentials are wrong. Do not save the password in local/session storage to simulate refresh tokens.

## 5.4 Required browser proof

Use the **shipped Settings page** at the repository's supported large-desktop viewport, not only a bUnit renderer or a new sandbox. Exercise local trusted administration with secure JWT mode enabled and HTTP access administration disabled, then independently exercise an enabled HTTP-admin scenario.

Create an account from Settings, select two concrete capabilities, log in through the real API, verify an allowed operation and a forbidden unrelated operation, edit/remove a capability, verify the old JWT fails, log in again and confirm the new grant set. Reset the password, check old-password rejection and new-password success; disable/re-enable and finally delete the account. Ensure re-enable did not revive an old token.

Also verify the existing machine-token issuance/list/revoke flow and ordinary `/settings` navigation did not acquire a global login redirect. Browser visibility is not enough: assert the persisted safe state and the corresponding real HTTP results after each mutation.

Use stable `data-testid` hooks for user controls, analogous to existing token hooks, without coupling tests to CSS layout or component internals. Evidence screenshots must not include entered passwords, hashes, complete tokens, vault contents or environment variables. Mask/omit the one-time token panel before screenshots rather than treating screenshots as a secret store.
