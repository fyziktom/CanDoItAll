# SB07 test-budget exception record

Status: `exceeded-unapproved`

Recorded: 2026-08-25

Authority: None. No explicit operator approval or governing budget amendment has been recorded for this overrun.

## Governing limits

- `bundle://test-budget.json` limits the whole bundle to 2 multi-instance runs and 2 Docker application-image builds.
- `bundle://plan/02-test-budget-and-gates.md` allocates SB07 one Docker lane and one application-image build, reserving the other planned run and build for SB12.
- This record does not amend either governing document or authorize another run or build.

## Durable recorded consumption

Seven governed lifecycle commands were invoked with `pwsh -NoProfile -File tools/SharedProviders/Run-SharedProviderE2E.ps1 -Reset`. All seven exited with code 1; none produced passing lifecycle proof. Five of the seven progressed far enough to start the isolated topology.

| Transcript | Started (UTC) | Duration | Exit | Last durable phase | Application-image build invoked | Recorded failure |
| --- | --- | ---: | ---: | --- | --- | --- |
| `bundle://subbundles/SB07-backend-checkpoint-three-instance-proof/proof/transcripts/11-governed-lifecycle.txt` | 2026-08-25T13:18:59.372135Z | 2.903 s | 1 | Verify Docker Engine and Compose | No | Required native query returned exit 1. |
| `bundle://subbundles/SB07-backend-checkpoint-three-instance-proof/proof/transcripts/15-governed-lifecycle-final.txt` | 2026-08-25T14:45:23.961034Z | 180.856 s | 1 | Prepare exact marked artifact root and credentials | Yes | Resolved-Compose validator reported an unexpected host port. |
| `bundle://subbundles/SB07-backend-checkpoint-three-instance-proof/proof/transcripts/16-governed-lifecycle-final.txt` | 2026-08-25T14:58:00.976202Z | 222.117 s | 1 | Start isolated databases and upstreams | Yes | PowerShell attempted to read a nonexistent `Sum` property. |
| `bundle://subbundles/SB07-backend-checkpoint-three-instance-proof/proof/transcripts/17-governed-lifecycle-final.txt` | 2026-08-25T15:12:09.258887Z | 228.606 s | 1 | Seed central | Yes | Failed one-off container was not removed within the cleanup deadline. |
| `bundle://subbundles/SB07-backend-checkpoint-three-instance-proof/proof/transcripts/19-governed-lifecycle-final.txt` | 2026-08-25T15:25:12.737884Z | 238.718 s | 1 | Seed central | Yes | Required native command returned exit 1. |
| `bundle://subbundles/SB07-backend-checkpoint-three-instance-proof/proof/transcripts/23-governed-lifecycle-final.txt` | 2026-08-25T15:49:00.356922Z | 435.762 s | 1 | Seed both clients | Yes | Log-checkpoint code mutated an `OrderedDictionary` during enumeration. |
| `bundle://subbundles/SB07-backend-checkpoint-three-instance-proof/proof/transcripts/24-governed-lifecycle-final.txt` | 2026-08-25T15:59:10.999541Z | 550.930 s | 1 | Prove persisted idempotent source synchronization | Yes | Required native command returned exit 1. |

One additional application-image build invocation is durably recorded in `bundle://subbundles/SB07-backend-checkpoint-three-instance-proof/proof/transcripts/sb07-docker-build-app-image.txt`. It successfully exported `docker.io/library/candoitall-shared-providers:e46f81d5`. That raw Docker transcript does not contain wrapper-captured start time, duration, command line, or process exit code, so this ledger does not invent those fields.

## Accounting

- Recorded governed lifecycle invocations: 7. Against the whole-bundle cap of 2, this is 5 over. Against the SB07 planned allocation of 1, this is 6 over.
- Recorded application-image build invocations: 7 total (6 within lifecycle attempts and 1 standalone build). Against the whole-bundle cap of 2, this is 5 over. Against the SB07 planned allocation of 1, this is 6 over.
- Even if only topology-started lifecycle attempts are counted as multi-instance runs, 5 were recorded, which is 3 over the whole-bundle cap and 4 over the SB07 planned allocation.
- Cached Docker layers do not make a build invocation free for budget purposes; each lifecycle build stage listed above is counted.
- Deterministic upstream-fixture image builds are not included in the application-image-build count.
- These are exact counts of the durable transcripts currently retained and therefore a recorded minimum. This file makes no claim about commands for which no transcript exists.
- No SB12 multi-instance-run or application-image-build allowance remains under the current governing limits.

## Closure status and required authority

The retained evidence contains zero passing governed lifecycle runs. SB07 therefore cannot close from this evidence, and the budget state remains `exceeded-unapproved`.

No further Docker lifecycle run or application-image build is authorized by the current contract. A further closure attempt requires explicit operator approval and a durable amendment of the governing budget and associated plan/status documents before execution. Any later approval must preserve this historical ledger; it must not relabel these prior attempts as approved or passing.

No broad, stable, Playwright, live-provider, or paid-provider lane is recorded as consumed by these attempts. This record contains no secret values.
