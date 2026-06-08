# SB021 Semantic Invariants

## SB021_INV_001
- Invariant ID: `SB021_INV_001`
- Source raw note: `Prepare broader phases toward stable Core and domain drivers`.
- Expected behavior: Gate G can close only when the concrete verification gateway denies side-effect operations through both implemented lanes, excludes unimplemented lanes, and has no dynamic discovery, generic dispatch, runtime host, DI, manager, scheduler, workflow, file, HTTP, workspace, storage, or mutation surface.
- Disallowed shallow implementation: report-only gateway claims, a gateway that can call one verifier but accepts side-effect operations, generic `object` payload dispatch, unimplemented lanes exposed as callable, or source scans that ignore runtime host and integration surfaces.
- Failing-first test: `bundle://proof/SB021/transcripts/red-team-gateway-runtime-host-rejection.txt` rejects closure without build, focused side-effect denial tests, absent-lane proof, no-runtime-host scan, and upstream manifests.
- Passing test: `bundle://proof/SB021/transcripts/gate-g-proof-index.txt` verifies SB019/SB020 manifests, clean build, 3/3 focused gateway tests, no-runtime-host scan, and red-team rejection.
- Changed source files: `repo://src/CanDoItAll.Processes.Drivers.VerificationGateway/CanDoItAll.Processes.Drivers.VerificationGateway.csproj`; `repo://src/CanDoItAll.Processes.Drivers.VerificationGateway/ProcessDriverVerificationGateway.cs`; `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverVerificationGatewayTests.cs`.
- Production assertions: gateway has explicit `VerifyTranscript` and `VerifyRuntimeEvidence` methods only, implemented lanes are transcript and runtime evidence only, and no generic lane dispatch or `object` payload exists.
- Security assertions: source scan proves no runtime host, registry, selector, provider, DI, process, HTTP, file, directory, DbContext, manager command, scheduler, workflow, UI/media, or secret-like drift in Gate G targets.
- Adversarial negative case: runtime-host or generic-gateway closure without side-effect denial and absent-lane proof is rejected with simulated verifier exit code 1.
- Downstream dependency check: SB022 and later evidence-boundary phases may proceed only from a gateway baseline that remains read-only, explicit, and limited to implemented lanes; if side-effect denial, absent-lane proof, or no-runtime-host scans fail, downstream phases must reopen.

## Artifact Matrix
| Artifact | Role | Required signal |
| --- | --- | --- |
| `gate-g-solution-build-no-restore.txt` | Build proof | Solution build succeeds. |
| `gate-g-focused-verification-gateway-tests.txt` | Behavioral proof | Explicit gateway focused tests pass. |
| `gate-g-gateway-no-runtime-host-scan.txt` | Source proof | Gateway has no generic dispatch or runtime host. |
| `red-team-gateway-runtime-host-rejection.txt` | Adversarial proof | Runtime-host/generic-gateway closure is rejected. |
| `gate-g-proof-index.txt` | Positive proof index | Gate G proof artifacts and upstream manifests are verified. |
