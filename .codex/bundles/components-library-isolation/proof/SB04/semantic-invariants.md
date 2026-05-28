# SB04 Semantic Invariants

## Main Solution Isolation

- Invariant ID: `SB04-MAIN-SOLUTION-LIT-UP`
- Source raw note: Main slnx must exclude moved components and Space3D while a dedicated Space3D slnx remains available.
- Expected behavior: Main solution builds from local component packages and no longer schedules moved component or Space3D projects.
- Disallowed shallow implementation: Removing Space3D from the slnx while unit tests still pull Space3D into the main solution build graph.
- Failing-first test: Main slnx audit rejects moved component and Space3D names, recorded in `bundle://proof/SB04/transcripts/sb04-closure-proof.txt`.
- Passing test: Main build, Space3D build, focused tests, and browser smoke are recorded in `bundle://proof/SB04/transcripts/sb04-closure-proof.txt`.
- Changed source files: `repo://CanDoItAll.slnx`, `repo://CanDoItAll.Space3D.slnx`, and `repo://tests/CanDoItAll.Space3D.Tests/CanDoItAll.Space3D.Tests.csproj`.
- Production assertions: Main app starts with package CSS and main CSS static assets served; Space3D remains buildable through its dedicated slnx.
- Red-team negative case: Reintroducing moved component or Space3D project names into `repo://CanDoItAll.slnx` fails the final slnx audit.
- Downstream dependency check: Browser smoke verifies the main app serves package and app CSS after the solution split.
