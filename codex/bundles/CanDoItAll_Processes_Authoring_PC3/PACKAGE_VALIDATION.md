# Package-only validation — single-download revision

This revision repackages the existing PC3 task; it is not a fresh repository review. Its
manifest covers only this task directory's LF-normalized UTF-8 bytes, excluding the manifest
itself. The existing shared bundle is not distributed, version-pinned, hash-pinned or
modified by the verifier. Integrity checks prove consistency, not authenticity or semantics.

## Verification performed for this distribution

- All 16 tests of the local read-only verifier pass, including rejection of corruption,
  unsafe paths, symlinks, an embedded shared directory and a shared pin/download requirement.
  A deliberately invalid adjacent shared manifest does not affect task-only integrity
  checking and is not read or modified; this is not a claim about that shared file's validity.
- Every task JSON document parses; the manifest's file set and digests match. Relative
  Markdown file links and Python syntax are checked. No runtime cache is included.
- The 50 PC3 acceptance groups and all 42 PC1 + 31 PC2 carry-forward rows are preserved.
  Their corresponding evidence rows remain aligned and start NOT_RUN.
- The original technical analysis/design, source register, browser journeys, acceptance plan
  and carry-forward matrix are byte-identical to the original PC3 archive. Only handoff,
  shared-maintenance policy and related execution/evidence/packaging metadata are revised.
- The archive contains one PC3 top-level directory, no shared replacement subtree and no
  shared installation/migration script. Verification is repeated after fresh ZIP extraction.
- The existing v4.1 shared helper can also verify the revised task with its unchanged
  `verify <task-directory>` command. That compatibility check requires no shared update;
  this task's own verifier remains the primary package-only command.

These checks do not execute .NET/C# tests, CodeAnalytics, PostgreSQL, Playwright, GPG signing
or runtime benchmarks. They do not re-certify the prior source review or historical product
results. All PC3 product evidence intentionally remains NOT_RUN.

## Reproduce locally

From the repository root, run:

```text
python -B codex/bundles/CanDoItAll_Processes_Authoring_PC3/tools/verify_package.py codex/bundles/CanDoItAll_Processes_Authoring_PC3
python -B -m unittest discover -s codex/bundles/CanDoItAll_Processes_Authoring_PC3/tools -p "test_*.py" -v
```

No `--shared` argument, shared version or companion ZIP is needed. Read and apply the
installed shared guidance under [SHARED_BUNDLE_POLICY](SHARED_BUNDLE_POLICY.md) independently.
Do not interpret this integrity check as a semantic approval of an unexamined shared baseline.

Keep execution evidence and working progress outside sealed PC3 inputs. Do not rewrite
hashes to hide local edits. Any genuinely required Codex-owned shared correction follows
the explicit policy, with an inspected diff and its own validation/signature evidence;
it is not part of installing this archive.
