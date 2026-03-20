# PROMPT 07 — Multi-part model (E1) + migration

Goal: Add ScoreDocument.Parts and PartId on events, preserving backward compatibility.

Read:
- `DESIGN/VOICING_LYRICS_PAGINATION.md` (sections 1-2)

Tasks:
1) Implement `ScorePart` and `ScoreDocument.Parts`.
2) Add `PartId` to `ScoreEvent` (and derived types).
3) Update JSON serializer/deserializer to migrate:
   - if Parts missing, create default part
   - assign all events to default part
4) Add unit tests for migration.

Update checklist:
- Mark **E1** done.

STOP.
