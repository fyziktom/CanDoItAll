# Technical-agent teams: complete small family after capability authoring

This is AgentFramework grouping, not CRM/HR organization design. The actual sources [S16-S17,
S21,S25] include metadata editor, icon picker, member picker, catalog host and native team owner.
Complete all those render paths in this run while retaining their native ownership.

## Existing behavior to preserve

The metadata form edits name, description and icon. Its model also contains member IDs.
The member dialog searches the real agent cards, retains selected IDs across search and returns
team identity plus IDs. The catalog host then calls its existing operation owner and reloads.
Deleting a team removes the group, not the agent definitions. The native owner normalizes names,
rejects duplicates and rejects membership IDs absent from the catalog [S21]. Private-provider
badges are metadata, not authority; membership grants no project or tool permissions.

## TA-R1: immutable metadata request and correct membership ownership

Metadata Save currently sends the live editor model. The owner derives name/id before an awaited
catalog update but reads description, icon and AgentIds within the later mutation. A held store
operation can expose mixed-time fields [S16,S21]. Capture a deep request before dispatch and test
later field edits, icon completion, Enter/footer duplication and closing/replacing the editor.

A metadata editor loaded before another operator changes membership can later send stale AgentIds.
Do not call this a PP3 regression; it is a native/editor contract risk of this old surface. Required
outcome: changing only team metadata must not silently revert another operator's membership.
Choose a narrow editor-specific operation preserving current members, or an exact coordinated
precondition with explicit conflict. The check belongs at the owner update, not a prior unlocked
read. Preserve old full-team upsert semantics for callers that intentionally use that contract.
Likewise, an exact existing-editor update must not silently recreate a deleted team under an old
ID; distinguish this from legitimate legacy upsert/create. No database schema or global identity
protocol is authorized. A broader owner problem needs a concrete map, not a speculative rewrite.

## TA-R2: return to the original team, not the current parameter

The old member dialog initializes once, but Confirm reads the current Team.Id. The current catalog
host normally opens a new component; test its actual lifetime and the new proposed seam before
calling parameter substitution a production incident. Bind every rendered selection/result to the
opening team and accepted agent/profile snapshot. Reject old callbacks after A-B-A, close/reopen,
profile change and replacement. Preserve missing selected IDs as explicit unavailable references
rather than silently dropping them; native refusal remains authoritative.

The parent performs the membership write. Return no new authority in the result. Capture intended
IDs and the original target/version context before its await. A late catalog refresh cannot move
navigation or clear a new dialog. Distinguish native commit from subsequent UI refresh failure;
retry read-back without repeating a confirmed membership operation. Keep independent groups and
all affected agent configuration intact.

## Icon and catalog integration

Use the actual Material icon picker/catalog, scoped CSS and shared cards. Preserve keyboard,
focus and parent/child lifetime. A late picker result cannot change a replacement editor. Catalog
team counts, filter, selected team, private badges and requested route selection continue to work.
Where display card contracts are too broad, project safe display values rather than adding a
Core/runtime reference or copying its renderer.

## Native proof

Create a team through UI, choose a real icon, reopen, change membership, reopen again, and compare
native IDs. Open two editors: change membership in B, metadata in A, verify no silent reversal.
Test duplicate names, missing agent, deleted team, changed profile and failed read-back. Deleting
one team preserves both member agents, their capabilities/provider/access configuration and another
group. Use current catalog-owner tests and add deterministic coordination where needed.
