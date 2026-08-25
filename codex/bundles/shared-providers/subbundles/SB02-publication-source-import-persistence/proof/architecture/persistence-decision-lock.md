# SB02 persistence decision lock

## Canonical ownership

Workspace owns publication, source, import, service identity, invocation metadata, state
transitions, reconciliation, and provider-reference policy. `AppDbContextModelRegistry` discovers
the module's cohesive EF configurations; Foundation and Migrations gain no reference to Workspace.

## Effective-profile materialization

SB02 selects the architecture-approved derived-cache option because the current effective runtime
loader maps `ProviderProfile` synchronously and a source/import join there belongs to SB06.

- `SharedProviderSource` is canonical for the source root URI and one secret-record reference.
- The linked imported `ProviderProfile.BaseUrl` caches
  `SharedProviderRoutes.ResolveOpenAiBase(source.BaseUri)`.
- `ProviderProfile.ApiKeySecretId` caches the same secret-record identity, never a secret value.
- A source edit updates the source and every linked profile cache in one atomic EF transaction;
  application-managed concurrency tokens provide optimistic conflict detection.
- Local profile `Name`, `Id`, and `IsEnabled` remain local intent and are not overwritten.
- Commit observers run for every affected provider only after the transaction commits.

Any non-atomic propagation or second stored secret value fails the checkpoint.

## Downstream generic-edit guard

SB02 does not register `provider.candoitall-shared`, so the existing generic provider editors
cannot resolve and save an imported profile. SB06 must preserve that fail-closed behavior when it
adds runtime projection, and SB08 must add the owned server-side update policy before enabling an
editor surface: only local alias and enabled intent may be changed through the ordinary editor;
source-owned endpoint, credential reference, model, purpose, capabilities, connector identity,
schema, and pricing remain read-only. Violating this sequencing reopens the owning checkpoint.

## Relational and state rules

- Separate entities and unique indexes own public publication, source, remote publication, local
  profile, stable service identity, invocation identity, and optimistic concurrency.
- Provider/source/secret relationships use explicit restrictive deletion.
- Transient catalog failure never implies authoritative absence.
- Only a successful authoritative catalog can mark an import missing; reappearance reuses the
  import and local profile identity.
- Source-instance mismatch is a persisted blocking state and never silently rebinds.
- Invocation storage is metadata-only, idempotently finalized, retention indexed, and cannot
  represent missing usage as zero.

## Existing destructive transfer path

`AiProvidersDatabaseTransferHandler` currently deletes target provider rows in bulk. SB02 must
either transfer the complete shared-provider graph in dependency order or explicitly reject a
transfer involving shared-provider references before mutation. Allowing a raw FK exception or
partial copy is not acceptable.
