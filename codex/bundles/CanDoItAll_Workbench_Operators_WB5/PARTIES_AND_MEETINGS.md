# W1 — participant and meeting directory integration

## Preserve the actual meaning

A participant can remain project-local or be linked to a directory party. Quick create
creates a reusable directory entity; it does not itself complete participant sync. A party
classified AiAgent is not evidence that a new configured runtime Agent was created.
Meetings copy project defaults into the draft and save only the relevant node roles.
Do not infer grants, capabilities, workspace-tool access or Process launch permission from
party membership. [S14, S15]

Move the whole existing markup, including linked name/status, loading/errors, local-only
choice, actual ResourceCardPicker, quick-create kind/name/email/phone/summary and all meeting
selection controls. Use actual typed values, not display strings as command identity.

## Drafts, reads and preserved references

Maintain one draft/EditContext for a real participant or meeting opening. Refresh of unrelated
Structure rows, options or the same selected node must not erase its unsaved fields. A
meaningful project/node/lifetime/actor change retires it. A→B→A is not one opening.
Capture the current request generation even for two reads within the same selection revision;
late success, error, progress and finally must not publish to a newer view. Avoid fetching the
whole directory per keystroke or render; retain bounded native read behavior and lazy loading.

Distinguish successful empty options from failed/unavailable options, and an actually missing
saved party from a temporarily unavailable lookup. Do not silently drop IDs missing from the
current filtered option list during Save. Keep explicit removal and disabled/missing retained
references distinguishable; let native policy decide which operations are legal.

Sensitive directory contacts must not leak through subtitle, hidden attributes, search text,
tooltips, diagnostic JSON or a supposedly safe view model. Project only fields this consumer
may reveal. This is not authority to change stored directory records or flatten its policy.

## Canonical assignment and metadata phases

Current participant and meeting Save paths can commit assignment replacement before updating
node metadata and before option/surface read-back. They are not a guaranteed atomic transaction.
Use the exact original project/node and role set; preserve PartyId, AssignmentId, affiliation,
primary designation and unrelated roles/project defaults. Do not replace a metadata-derived
assignee hint instead of the canonical assignment store.

`ReplaceNodeAssignmentsIfCurrentAsync` already exists, with expected assignment snapshots and
an explicit unavailable fallback. Assess its supported semantics for these node kinds, use
it where suitable, and never silently fall back to unconditional replacement when it refuses.
Do not fabricate a work-item direct-assignment revision for a participant or meeting. [S15]

Native metadata changes must preserve unknown JSON, references, precise timestamps, durations,
null-versus-value distinctions and untouched fields. A full snapshot captured before a
concurrent legitimate edit must not overwrite that edit under freshly acquired authority.
Fix the smallest native comparison/merge seam required, not a new global ETag framework.

Keep these phase facts separately: accepted directory create, accepted assignment change,
accepted node update and visible read-back. Preserve exact identities before later awaits.
Read retry never recreates a party or repeats a committed role replacement. A genuinely
unknown phase needs observation, not a fabricated rollback. Compensate only through an
existing safe owner contract, not by deleting a globally reusable party.

## Native verification

Use real PostgreSQL directory/assignment/workbench owners. Exercise local-only, linking an
existing party, every currently supported quick-create kind, meeting defaults, selected-party
removal, a retained missing reference, sensitive projection and no-change Save.

Include two editors with competing assignments, a metadata-only concurrent edit, altered node
kind/parent occurrence, deleted/recreated project, and failures after directory/assignment/
metadata commit. Read back exact assignment roles and revisions, accepted IDs and unchanged
neighbor objects. A passing new fake adapter is not native evidence. The prior S0 is part of
this family, not a substitute for its complete round-trip and concurrency proof.
