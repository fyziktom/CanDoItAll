# SB07 Semantic Invariants

Date: 2026-07-12.

| Invariant | Required behavior | Positive evidence | Adversarial evidence |
| --- | --- | --- | --- |
| `SB07_INV_CURRENT_AUTHORITY` | Every effect is authorized against the current actor/session/runtime/profile/source/policy revision and exact operation. | `passing-unit-redteam.txt`; the valid view and save paths pass. | Forged, cross-actor, cross-runtime, cross-session, wrong-operation, source-removal, and authorization-revision changes fail before storage. |
| `SB07_INV_OPAQUE_BOUNDED_HANDLE` | Handles are unguessable, expiring, revocable, process-local, and retained within a configured bound. | A 32-byte RNG value is emitted as 43-character base64url; capacity and revoke-all tests pass. | Oldest deterministic eviction, expiry, revocation, and unknown handle cases are rejected. |
| `SB07_INV_RERESOLUTION` | Browser activation treats keys as locators only and resolves the current occurrence within its semantic root before grant. | Current occurrence is re-browsed and granted in the positive test. | Forged container/item keys and filesystem/FTP locators outside the semantic root are rejected. |
| `SB07_INV_ZERO_BROWSER_CONTENT` | A known authorized file can open after browser disposal and without construction or invocation of browser state. | Direct known-file factory/content test passes. | The production factory accepts only authorization/content/save collaborators; no FileBrowser catalog/session/provider member exists. |
| `SB07_INV_ENDPOINT_AUTHORITY` | Unsigned reference tokens and paths are never content authority; authorized handles are not placed in URLs. | Authorized fixed content/download route succeeds with the handle header. | Unsigned legacy `ref` requests return 401; `/managed-files/{**path}` and traversal requests return 410; auth-enabled host without a principal returns 401. |
| `SB07_INV_SAVE_REVISION` | Save is awaited, reauthorized, expected-revision guarded, and overwrite requires explicit permission. | Successful save returns the persisted new revision; native filesystem replacement persists only successful bytes. | Conflict, write failure, and cancellation keep dirty state/revision; stale revision and overwrite-without-permission perform no unauthorized commit. |
| `SB07_INV_COMMIT_RACE` | Expected revision is checked at entry and immediately before replacing the destination. | Production filesystem test exercises stale rejection and successful atomic temp-file replacement. | A revision mismatch throws `StorageContentConflictException`; the temporary file is removed without replacing the current object. |
| `SB07_INV_REDACTION` | Handles, raw paths, actor identifiers, content, secrets, and provider exception details do not reach public logs/errors. | Captured-log masking test and source audit pass. | Injected sensitive markers are absent; HTTP handle is supplied only through the named header. |

These invariants apply to the production registry, coordinator, authorizer, content/save adapters, HTTP routes/context adapter, and revisioned filesystem effect. Display keys, capabilities, URLs, unsigned tokens, and client state are explicitly non-authoritative.
