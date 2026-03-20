# DB / storage / hashing checklist

- [x] Canonical JSON is identical between PHP and C# for the same logical input.
- [x] Blob files are deduped by SHA-256.
- [x] Snapshot files are deduped by snapshot hash.
- [x] Commit payload files are stored and verify against commit hash.
- [x] CID v1 raw is computed where configured.
- [x] Repository storage paths are safe against traversal.
- [x] Verification tool detects missing/mismatched blob/snapshot/commit files.
