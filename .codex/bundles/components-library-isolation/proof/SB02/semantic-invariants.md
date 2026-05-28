# SB02 Semantic Invariants

## Main Local Package Consumption

- Invariant ID: `SB02-LOCAL-PACKAGE-CONSUMPTION`
- Source raw note: Main repo must consume moved components from `ExternalPackages` and must not project-reference moved source projects.
- Expected behavior: Main repo restore/build resolves moved component libraries through `PackageReference` version `0.1.0`.
- Disallowed shallow implementation: Keeping any direct `ProjectReference` to moved component csproj files or replacing them with manual DLL references.
- Failing-first test: Pre-conversion direct project-reference matches are recorded in `bundle://proof/SB02/transcripts/failing-first-direct-project-references.txt`.
- Passing test: Post-conversion direct-reference audit and representative build are recorded in `bundle://proof/SB02/transcripts/sb02-closure-proof.txt`.
- Changed source files: `repo://NuGet.config`, `repo://ExternalPackages`, and main/test/tool project files that now use package references.
- Production assertions: `CanDoItAll.Components` and `CanDoItAll.Components.WebGlSandbox` remain main repo projects and compile against packages.
- Red-team negative case: Any reintroduced moved-component csproj reference fails the `rg` audit in SB02 proof.
- Downstream dependency check: SB03 and SB04 build against package references and do not require moved source folders in main repo.
