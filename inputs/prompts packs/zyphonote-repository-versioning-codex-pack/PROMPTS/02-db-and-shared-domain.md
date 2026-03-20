Implement the repository DB/storage foundation first.

Required work:
1. Add the repository migration based on `DB/2026-03-08-repository-versioning-proposed.sql`.
2. Add config/storage roots for repository blobs/snapshots/commits.
3. Create shared libs for:
- canonical hashing
- blob persistence
- snapshot persistence
- commit persistence
- branch/ref persistence
- repository lookup
4. Add command-line verification/backfill tool skeletons.

Rules:
- all code comments must be in English
- keep existing features working
- do not remove legacy version tables
- do not make non-default branches overwrite current public/read-model fields

Update relevant checklist files after implementation.
