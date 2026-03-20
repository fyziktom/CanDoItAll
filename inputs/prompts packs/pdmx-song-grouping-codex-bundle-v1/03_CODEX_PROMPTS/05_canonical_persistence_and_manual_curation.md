# Prompt 05 — Canonical Persistence And Manual Curation

## Objective

Apply reviewed grouping results into canonical group tables and add manual administration operations.

## Tasks

1. Implement apply-from-run flow:
   - create/update `SongGroup`
   - create/update `SongGroupMembership`
   - update cached primary `SongGroupId`
2. Implement canonical display-title/composer selection.
3. Respect manual overrides and lock modes.
4. Implement admin operations:
   - create group manually
   - add member
   - remove member
   - merge groups
   - split groups
   - set primary membership
   - set canonical display values
5. Implement derived tag sync if compatibility tags are used.

## Boundaries

- manual edits must survive reruns
- no destructive global delete/recreate
- do not make the apply flow depend on the review UI only; keep service APIs testable

## Required tests

- apply-run creates memberships
- primary cache sync works
- manual lock survives rerun
- merge/split service behavior
- derived tag sync
- canonical title selection

## Review checklist

- [ ] canonical truth is membership-based
- [ ] manual decisions are sticky
- [ ] existing summary UI can still resolve primary group quickly
- [ ] apply is retry-safe
