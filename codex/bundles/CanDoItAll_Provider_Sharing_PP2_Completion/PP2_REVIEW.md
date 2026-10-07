# Review of the pushed PP2 checkpoint

## Actual scope

The branch HEAD is `9052b2a443a4fdce1ac24dd0ccfce236fd35ab74` (signed product/test commit).
Its parent `df9b7c8e1b563d56fef762c38aad2a14089bd332` is the historical package addition.
The two-commit comparison from PP1 contains S0 repairs, fixture/tooling changes and tests, but
no new SharedProviders UI/sandbox project. The maintained record explicitly says structural
Sharing/source extraction and the final consumer campaign are pending [S01, S02, S03].

This is not an accusation of a hidden failure: the record is explicit and useful. It is a
partially completed assignment that must continue at PP2 rather than advance to History.

## Preserve

1. `ProviderProfilesReads` obtains native configuration revisions alongside catalog profiles.
   `ProviderProfilesSession` checks a changed imported editor against the same accepted revision
   before installing catalog and editor together, with read/selection/context checks. Same-revision
   imported views and dirty local PP1 editors retain their existing behavior [S04, S10].
2. The tree description uses display-name resolution; opaque model identities remain internal.
   Actual component tests exercise tooltips, snapshot changes and local invalid inputs [S08].
3. Sharing is no longer destroyed solely by replacement of the PP1 editor context. This preserves
   its independent draft across remote metadata refresh; however its local concurrency baseline
   is not yet sufficient. Repair the bounded finding instead of restoring unconditional resets
   [S05–S08].
4. Relay target creation now uses `ProviderMetadata.BuildConfigurationJson` rather than raw persisted
   connector JSON. The native regression goes through real save/publish/relay and checks the model,
   credential delivery and timeout. Do not return to invalid raw timeout representation [S12, S13].
5. The runner has explicit fixture/port/configuration inputs and guarded ownership. Reuse it rather
   than creating a parallel container harness. Its complete implementation still requires review
   before destructive/resume actions [S15, S01].

## Added finding

PP2C-R1 is described in [R1_IMPORTED_LOCAL_SETTINGS.md](R1_IMPORTED_LOCAL_SETTINGS.md). It is a
source-derived clean/dirty local-settings stale-value/latest-token combination, not a runtime
reproduction performed by this reviewer. The existing regression verifies retention under a
remote-only change but never saves after a concurrent operator changes local alias/enabled [S08].

## Limits

No local .NET SDK or Docker executable was available to the reviewer. C#, browser, PostgreSQL,
container and watch tests were not rerun. Private raw TRX, complete image manifests and logs were
not supplied. GitHub signature metadata was read; it is not a local `git verify-commit` run.
The source record separates what was read in full from partial inspection. Historical reports
are credited as implementer evidence, not promoted to independent execution.
