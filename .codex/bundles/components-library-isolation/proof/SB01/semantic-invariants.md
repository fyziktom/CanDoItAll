# SB01 Semantic Invariants

## Component Package Isolation

- Invariant ID: `SB01-COMPONENT-PACKAGE-ISOLATION`
- Source raw note: Move the eight stable component projects to the sibling components repository and build them as private packages.
- Expected behavior: The components repository owns the eight moved projects, builds a dedicated components slnx, and produces version `0.1.0` packages.
- Disallowed shallow implementation: Leaving moved project folders in main repo or producing package names without buildable source and package metadata.
- Failing-first test: N/A process/no production behavior exemption; the pre-split state had source folders in the main repo and no dedicated component package slnx.
- Passing test: `dotnet build`, `dotnet pack`, and component Tailwind build evidence in `bundle://proof/SB01/transcripts/sb01-closure-proof.txt`.
- Changed source files: Components repo `Directory.Build.props`, `CanDoItAll.Components.slnx`, package readmes, root package scripts, and Tailwind workspace.
- Production assertions: Main runtime behavior is unchanged; this phase changes source ownership and package generation only.
- Red-team negative case: A component repo project reference back to the main repo would violate isolation and fail the project-reference audit.
- Downstream dependency check: SB02 consumes only the generated packages from `repo://ExternalPackages`.
