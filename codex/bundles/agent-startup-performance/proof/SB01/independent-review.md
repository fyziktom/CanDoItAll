# Independent SB01 foundation gate

Decision: **Pass; SB03 entry unlocked.** Reviewed against committed baseline 3d5def561 and the candidate production.patch, source/binary hashes, discovery lists and TRX files in this directory. This is a foundation gate, not integrated bundle closure.

## Source and security review

The production change is confined to DurableFileWriter.EnsureDirectoryTree. Eligibility uses equality or a separator-terminated root prefix with StringComparison.Ordinal. Each reused-policy descendant is reconstructed from that literal root. Containment therefore does not depend on a stale sensitive/insensitive comparison. A case-variant input, Unknown case fact, or newly created directory retains fresh factory acquisition.

Both per-segment EnsureSafePath calls remain. Native-root/ancestor/link checks still execute; operation entry, coordination, external callback and precommit boundaries still reacquire facts. PhysicalFileSystemPathPolicy and its factory contracts are unchanged. No global cache, persisted trust flag, public API, project reference or DI change was introduced.

Payload WriteThrough, FlushAsync, Flush(true), atomic replacement, coordination, cancellation and cleanup code is unchanged. The test observer proves commit-stage order, not physical flush syscall counts.

## Evidence review

- Baseline: 19 Windows unit, 19 Linux unit, 31 Windows integration passed.
- Failing-first: the expanded 27-case suite failed exactly the three expected count assertions against the old writer; the remaining 24 passed.
- Candidate: 27 Windows unit, 27 Linux unit, 31 Windows integration passed; discovery and TRX counts reconcile.
- Before/after depth counts: depth0 8→8; depth6 20→8; depth12 32→8.
- Actual Windows insensitive case-variant exclusion: exact path8, variant12.
- Actual non-root Linux root symlink replacement and same-path recreation reject unsafe/stale writes. Unix private modes and symlink capability were affirmative.
- Unit and integration Infrastructure candidate DLL hashes agree.

A real Windows directory case-mode toggle was denied by the OS. This is an explicit limitation, not a claimed successful scenario. The independent review accepts the ordinal-containment argument, controlled changed-fact callback test, actual insensitive Windows case-variant behavior and actual Linux root replacement/recreation as sufficient evidence for this limited branch. No security bypass was used.

## Anti-stub audit and invalidation

Probe tests delegate to the real unchanged policy factory, write and read real payloads, and assert the existing commit stages. The old implementation demonstrably fails the performance assertions. Controlled case-fact tests do not replace the actual platform security tests. No dropped validation, mocked durable writer, always-success adapter or renamed work is accepted.

SB03 must rerun its 70-case storage integration selection, including the 31 downstream cases, after its own edits. Changed writer/policy/factory, lifetime, flush, lock, SDK/dependency or platform facts invalidate the relevant SB01 proof. Exact historical command metadata and stable invariant mapping are being added to the manifest without rerunning tests solely for bookkeeping. Real-host UI and paired performance gates remain open.
