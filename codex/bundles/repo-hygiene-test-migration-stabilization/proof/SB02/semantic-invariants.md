# SB02 Semantic Invariants

- Invariant ID: `SB02-RUNTIME-LAUNCH-WATCH`
- Source raw note: RH-003 and RH-004 required current runtime launch paths and realistic watch restore behavior.
- Expected behavior: launch tests target `src/App/CanDoItAll.Web/CanDoItAll.Web.csproj`, and stale referenced project assets prevent `--no-restore`.
- Disallowed shallow implementation: forcing restore for every watch run or hardcoding a developer-only path outside test fixtures.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first.txt`
- Passing test: `bundle://proof/SB02/transcripts/passing.txt`
- Changed source files: `repo://tests/Unit/CanDoItAll.Tests.Unit/ProjectStructureRuntimeLauncherTests.cs`, `repo://tests/Unit/CanDoItAll.Tests.Unit/WorkspaceRuntimeProcessToolsTests.cs`
- Production assertions: no runtime launcher production semantics were broadened beyond the current project layout expectation.
- Red-team negative case: `bundle://proof/SB02/transcripts/anti-stub.txt` verifies the stale-reference fixture points at an existing project.
- Downstream dependency check: SB05 startup proof uses the rebuilt web project and is recorded at `bundle://proof/SB05/5032-startup-log.txt`.

