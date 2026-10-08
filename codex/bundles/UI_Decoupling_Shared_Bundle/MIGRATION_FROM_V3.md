# Installing the active v4 companion

Extract both archives outside the repository first. Their top-level directories are:

```text
UI_Decoupling_Shared_Bundle/
CanDoItAll_Processes_UI_Decoupling_PC1/
```

Place them side by side under `codex/bundles`. The child references the active shared directory; it deliberately contains no embedded `shared/` copy.

## Safe replacement

Inspect `git status` and the diff of the existing active shared directory before replacement. The delivered `MANIFEST.json` is the inventory of the v4 package. Replace the active directory's tracked v3 content with this reviewed v4 inventory; review removal of obsolete active v3 files, including its Czech README. Do not leave a competing old active prompt or validator in place and then claim the new manifest is authoritative. Do not run a blind recursive delete: preserve and reconcile unexpected local/untracked operator files. An unexpected file is a reason to inspect it, not permission to erase it.

Git history preserves the old active v3 revision. Do not modify, delete, recursively copy, or rerun historical child bundles and their embedded v3 companions. Their sealed evidence remains historical. This scoped upgrade does not commission a translation/cleanup of every historical archive. No Czech summary is added to either new package or maintained engineering docs.

Validate the two extracted/installed packages with the helper in the v4 directory. Text manifest hashes normalize CRLF to LF only, so a Windows text checkout is supported without weakening checks for meaningful changes. Package verification is a handoff integrity check, not a required product test at every implementation stage.

`bundle.json` in the child requires the v4 ID and its delivered normalized manifest digest. An intentional local amendment must be reviewed, recorded and re-manifested; do not silently edit the sealed input to make its evidence look executed. Record current work in a separate owned run directory and maintained product architecture documents.

Do not alter the branch or check out the review commit. A later HEAD is expected; refresh relevant source and test observations before work. The supplied source register remains provenance even when the actual implementation begins at a newer revision.
