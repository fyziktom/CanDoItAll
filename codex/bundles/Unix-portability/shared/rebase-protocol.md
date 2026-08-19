# Rebase protocol

The prepared anchor is `62ea8ee0cc42c1c06da934d126a5c18f8237a89f` on `development`.

## Trigger

Use this protocol whenever the execution checkout HEAD differs from the prepared anchor or from a prior gate handoff.

## Steps

1. Preserve the working tree:
   ```text
   git status --short --branch
   git rev-parse HEAD
   git diff --stat
   ```
   Do not reset or clean.
2. Compare anchors:
   ```text
   git merge-base 62ea8ee0cc42c1c06da934d126a5c18f8237a89f HEAD
   git diff --name-status 62ea8ee0cc42c1c06da934d126a5c18f8237a89f..HEAD
   git log --oneline --decorate 62ea8ee0cc42c1c06da934d126a5c18f8237a89f..HEAD
   ```
3. Revalidate every path in `shared/source-reference-manifest.json`.
4. Re-run `scripts/scan_portability.py`.
5. Reclassify:
   - new/renamed/deleted source paths;
   - new persisted path/secret/key formats;
   - new `ProcessStartInfo`, shell, OS API, WMI/proc, FileSystemWatcher, external dependency, and CI surfaces;
   - changed MAF/Processes ownership.
6. Update findings, requirements, subbundle tasks, test matrix, and migration/dependency ledgers.
7. Invalidate evidence whose source or invariant changed.
8. Record `reviews/REBASE-REPORT.md` with:
   - old/new commits;
   - changed files by owner;
   - changed requirements/gates;
   - newly invoked split/correction/recovery path;
   - first eligible subbundle.
9. Run:
   ```text
   python ./scripts/validate_bundle.py --bundle-root . --repo-root <repo> --stage prepared --allow-different-commit
   ```

## Runtime-specific rule

`B00` always performs a rebase against the exact Core Gate C4 commit, even when the prepared development anchor happens to match. Core implementation changes are expected to alter runtime source references and capability contracts.

## Prohibited shortcut

Do not “adjust while coding.” Rebase the plan before implementation so migrations, ownership, and gates remain reviewable.
