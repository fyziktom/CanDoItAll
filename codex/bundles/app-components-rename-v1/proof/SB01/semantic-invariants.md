# SB01 Semantic Invariants

## Project Identity Rename

- Invariant ID: SB01-PROJECT-IDENTITY
- Source raw note: `N001` from `bundle://inputs/02-structured-input.md`.
- Expected behavior: the app facade is built from `repo://src/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj` with assembly and root namespace `CanDoItAll.AppComponents`.
- Disallowed shallow implementation: moving the folder while leaving the project file, assembly name, root namespace, or Razor namespace as the old facade identity.
- Failing-first test: N/A - process/non-production rename with no behavior-specific failing-first test; stale-reference search is the adversarial proof.
- Passing test: `bundle://proof/SB01/transcripts/renamed-project-build.txt` exits 0.
- Changed source files: `repo://src/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj`, `repo://src/CanDoItAll.AppComponents/_Imports.razor`, and app facade source listed in `bundle://proof/SB01/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB01/source-assertions.md` cites project identity source paths and `bundle://proof/SB01/transcripts/stale-reference-search.txt` confirms the old facade project path is absent.
- Red-team negative case: `bundle://proof/SB01/transcripts/stale-reference-search.txt` rejects a path-only rename by checking for old project file and old exact namespace declarations.
- Downstream dependency check: `bundle://proof/SB01/transcripts/component-tests.txt` exercises the component test project through the renamed app facade.

## Consumer Reference Repair

- Invariant ID: SB01-CONSUMER-REFERENCES
- Source raw note: `N002` from `bundle://inputs/02-structured-input.md`.
- Expected behavior: direct web and component-test project references point to `CanDoItAll.AppComponents`, and direct app-shell imports use `CanDoItAll.AppComponents`.
- Disallowed shallow implementation: fixing the solution entry only while leaving web project references, test project references, or direct app-shell imports on the old facade namespace.
- Failing-first test: N/A - process/non-production rename with no behavior-specific failing-first test; stale-reference search is the adversarial proof.
- Passing test: `bundle://proof/SB01/transcripts/component-tests.txt` exits 0 after running AppShell component tests.
- Changed source files: `repo://src/CanDoItAll.Web/CanDoItAll.Web.csproj`, `repo://tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj`, and direct app/test import files listed in `bundle://proof/SB01/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB01/source-assertions.md` cites repaired consumer references.
- Red-team negative case: `bundle://proof/SB01/transcripts/stale-reference-search.txt` checks for old exact consumer imports and old project references.
- Downstream dependency check: `bundle://proof/SB01/transcripts/component-tests.txt` confirms test consumer resolution through the renamed project.

## Sibling Repository Boundary

- Invariant ID: SB01-SIBLING-BOUNDARY
- Source raw note: `N003` from `bundle://inputs/02-structured-input.md`.
- Expected behavior: package references and sibling-repo settings remain `CanDoItAll.Components.*`, while only the main-repo app facade becomes `CanDoItAll.AppComponents`.
- Disallowed shallow implementation: broad replacement that renames package libraries or edits sibling repository pointers.
- Failing-first test: N/A - process/non-production boundary check; stale-reference search and source assertions provide the adversarial proof.
- Passing test: `bundle://proof/SB01/transcripts/renamed-project-build.txt` and `bundle://proof/SB01/transcripts/component-tests.txt` exit 0 with package references intact.
- Changed source files: package-bearing project files are listed in `bundle://proof/SB01/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB01/source-assertions.md` states sibling package references are retained and sibling settings were not edited.
- Red-team negative case: `bundle://proof/SB01/transcripts/anti-stub-audit.txt` checks changed sources for old project-name stubs and placeholder rename logic.
- Downstream dependency check: `bundle://proof/SB01/transcripts/component-tests.txt` confirms consumers still resolve package components used by AppShell tests.
