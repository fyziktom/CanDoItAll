# Authorization, Handles, And Effects

## Authority Model

Every effect receives an explicit `FileAccessContext` containing typed actor/session identity, runtime/database generation, grant/authorization revision, and correlation ID. When API authorization is disabled, Composition must register an explicit local-workspace access-context policy; absence of a policy fails closed. There is no anonymous default-provider fallback.

## Scope Resolution

1. Module converts current project/node/run/resource intent to a typed semantic scope request.
2. Scope provider resolves candidate storage bindings/roots without returning raw authority to UI.
3. Authorization coordinator validates actor, runtime profile, semantic ownership, binding, requested operation, and root/locator policy.
4. Outer adapter creates provider/source descriptors only for authorized candidates.

## Handle Contract

`FileHandleId` is at least 256 bits from `RandomNumberGenerator`, URL-safe only at the endpoint edge, and looked up server-side. A record binds:

- actor/session and authorization revision;
- runtime/database generation;
- semantic scope and storage binding/source;
- canonical occurrence identity;
- allowed operation flags;
- current/expected content revision where available;
- issued/absolute expiry and last-use policy;
- revocation generation.

Registry storage is bounded with deterministic eviction. Logout, session/profile switch, binding removal, access revision change, and explicit revoke invalidate handles. Never cache handles inside shared listing entries.

## Browse To Interaction

On FileBrowser activation:

1. reject stale rendered item/session/source stamps;
2. re-resolve the current native item;
3. reauthorize `View` or requested operation;
4. mint a handle-bound `FileReference` and independent `IFileContentSource`;
5. open FileInteraction;
6. content source resolves handle and reauthorizes again before opening bytes.

## Save

- `SaveRequested` is awaited.
- Resolve handle, reauthorize `Edit`, enforce expected revision and maximum size, then persist.
- A null expected revision is an overwrite request and requires a separate typed permission.
- On success, return persisted revision and publish catalog change after persistence.
- Conflict/failure/cancellation leaves dirty state and does not bump catalog revision.

## HTTP Endpoints

New download/content endpoints accept opaque handles and attach explicit authorization. Existing `/storage/objects/preview`, `/storage/objects/download`, and `/managed-files/{**path}` must be characterized, then require the same current policy or be deprecated/migrated. Possessing `StorageJson.EncodeReferenceToken` output or a path is never sufficient.

## Required Red-Team Cases

- forge/modify unsigned storage reference;
- actor A uses actor B handle;
- reuse after runtime-profile switch, expiry, logout/revoke, source removal, or permission change;
- swap locator between listing and open;
- request Edit/Download from View-only handle;
- exploit absolute path, encoded traversal, reparse point, signed URL, or retained stale browser item;
- overwrite conflict without explicit permission;
- inspect logs/errors for secret/path/content leakage.
