# SB05 Semantic Invariants

- Invariant ID: `SB05-FULL-RUNTIME-SMOKE`
- Source raw note: RH-009 and RH-010 required rebuild, full unit-suite proof, and a live `5032` smoke.
- Expected behavior: solution build succeeds, the full unit suite passes, and the freshly started web app responds on `localhost:5032`.
- Disallowed shallow implementation: reusing a stale listener as proof or omitting the full unit-suite outcome after targeted fixes.
- Failing-first test: `bundle://proof/SB05/transcripts/failing-first.txt`
- Passing test: `bundle://proof/SB05/transcripts/passing.txt`
- Changed source files: `repo://src/App/CanDoItAll.Composition/ModuleAssemblies.cs`, `repo://src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`
- Production assertions: the rebuilt web host starts from `repo://src/App/CanDoItAll.Web/CanDoItAll.Web.csproj` and listens on port `5032`.
- Red-team negative case: `bundle://proof/SB05/transcripts/anti-stub.txt` verifies the listener belongs to the rebuilt web executable.
- Downstream dependency check: final bundle closure depends on `bundle://proof/SB05/build.txt`, `bundle://proof/SB05/full-unit-suite.txt`, and `bundle://proof/SB05/5032-smoke.txt`.

