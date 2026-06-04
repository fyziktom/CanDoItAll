# Remaining Weak Spots

1. `ArtifactProjection.cs` still contains repeated side-effect blocks:
   - read file bytes
   - call `storagePlacementService.PlaceAsync`
   - build `ProcessArtifactRecordRequest`
   - call `RecordArtifactAsync`
   - update candidate external reference / expectation ids

2. The write coordinator currently has a narrow success result (`Result<string>`). It should return a structured outcome with at least:
   - managed storage path
   - external reference key
   - artifact expectation id
   - artifact kind/title/trust status snapshot

3. Record-only decision artifacts do not fit the storage-backed write coordinator, but they still deserve a small helper to keep artifact record construction consistent.

4. Some helper files remain in the Dispatch folder. That is acceptable for this bundle; do not move them into Process Core.

5. Source adapters are useful, but the dispatcher still owns too much orchestration around duplicate checks, file reads, source full-path resolution, and candidate state updates.
