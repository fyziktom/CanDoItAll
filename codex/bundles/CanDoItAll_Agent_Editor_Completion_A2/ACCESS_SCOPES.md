# Access scopes: presentation never becomes authorization

R03/R04/R15/R18 describe the existing host and access query. Characterize each transition before
moving it. The goal is faithful isolated rendering and bounded repairs, not a new permission model.

## Project Structure Access

Preserve CanRead, CanWriteNonTaskStructure, CanWriteTasks, full CanWrite, CanCreateProjects,
CanCreateSubprojects and AllowAllProjects as distinct meanings. Existing implication/normalization
rules remain single-sourced; a disabled checkbox does not erase its stored subordinate values.
The explicit project load is lazy. Switching tabs alone must not eagerly list projects, run schema
work or fetch unrelated data. Failed/cancelled reads are not empty success.

Already saved IDs and acquired lifetime bindings remain available even when the selection list
is incomplete or an item disappeared. Removing a reference is explicit; re-adding a same-ID new
lifetime is a new authorized selection, not an automatic repair. Native save and runtime still
validate current lifetimes. Select all observed items must never silently become AllowAll future
projects. Preserve the current intentional behavior of toggling AllowAll; test it rather than
inventing generic allow-list semantics.

## Workspace Tools and Storage

Profiles are not merely labels. `ChangeWorkspaceToolProfile` preserves external aliases/bindings
and Storage choices while applying that profile's execution/file defaults. Manual overrides set
Custom as before. Read/write implications, script/environment settings and normalizers retain
meaning. Native local scripts, environment inspection, execution rights and storage writes are
not silently enabled by normalization or test setup.

The actual Storage selection field returns IDs to the parent draft; it does not Save an agent.
Pass the actual owner lifetime/revision, propagate restricted/missing records, and keep independent
edits/flags when a nested picker returns. Existing Storage picker assembly and backend stay owners.
Verify apply-then-cancel-parent makes no persisted permission change.

External-root candidate entry and host-bound registry behavior are specified in
[Memory and roots](MEMORY_AND_ROOTS.md). Risk confirmations cover both enable-scripts and enable-
environment paths, with exact permission kind and originating editor. Rejection must restore the
actual checkbox state. Late acceptance cannot broaden a new editor or a superseding permission
choice. Do not use a global bool to represent every outstanding decision.

## Secrets

Only safe ID/name/kind/purpose reference metadata enters this section. Use existing
`SecretService.ListForPickerAsync` path through the adapter. Never fetch/decrypt the secret value.
Preserve unavailable selected references rather than quietly removing them from a new Save.
An unauthorized or failed list is not permission to clear persisted IDs. Add/remove is draft state;
no secret creation/deletion, vault mutation or credential output occurs here.

## Process Access

Keep stored definition IDs, CanRead/CanWrite/AllowAll and current implications. The current
'Process definition selection is unavailable' state is intentional for this slice: preserve its
explanation and metadata. Do not implement the missing picker by calling a speculative Process
API, widen AllowAll to make tests green, or claim a new runtime capability.

## Cross-scope regression

Create a real agent with exact project/lifetime, storage and external-root constraints. Use actual
runtime queries/actions to show an allowed case and a disallowed sibling project/storage/root case.
Changing one scope may not normalize away another. Cancelled UI confirmations are not approval
for runtime calls. Fields, capabilities, approvals and domain admission remain separate checks.
