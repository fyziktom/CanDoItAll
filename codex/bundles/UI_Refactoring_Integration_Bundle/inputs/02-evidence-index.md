# Discovery Evidence Index

This index records the source locations inspected while preparing the bundle. Execution
must refresh them against current tips.

| ID | Repository / path or API surface | Finding |
|---|---|---|
| E-001 | `CanDoItAll/development` branch | Recorded at `7e2a300...`; green CI |
| E-002 | `CanDoItAll/ui-refactoring` branch | Recorded at `a2903c4...`; five unique commits |
| E-003 | `CanDoItAll/ui-refactoring-v2` branch | Recorded at `7b7d363...`; 27 unique commits; forbidden |
| E-004 | `CanDoItAll/Directory.Build.targets` | Source references are the default |
| E-005 | `CanDoItAll/Directory.Build.props` | Package fallback values are `0.1.18` |
| E-006 | `CanDoItAll/.github/workflows/ci.yml` | Sibling repositories are checked out at exact SHAs |
| E-007 | `CanDoItAll/src/App/CanDoItAll.Web/Components/App.razor` | Still loads `material-icons.css` |
| E-008 | original UI `App.razor` | Loads `material-symbols.css` |
| E-009 | original UI `global.json` | Old `10.0.204` pin; must not override current development |
| E-010 | original UI `package.json` | Adds root `watch` command |
| E-011 | original UI `.gitignore` | Adds `.idea/` |
| E-012 | original UI `PODMAN.md` | Useful instructions with stale source-mode assumptions |
| E-013 | `Components/main` | Recorded at `38c3072...`; merged UI work |
| E-014 | Components CI run `33571242020` | Build/assets passed; unit-test job failed |
| E-015 | Components unit logs | Three failures: API snapshot, source snapshot, Canvas asset manifest |
| E-016 | `Components/.gitignore` | Ignores all `output.css` files |
| E-017 | `Components/Tailwind/package.json` | Generates BaseLib and sandbox CSS |
| E-018 | `Components/BaseLib.csproj` | No automatic Tailwind generation target |
| E-019 | `Components/TODO.md` | Build enforcement for output CSS was still unresolved |
| E-020 | `FileTools/main` | Recorded at `cc398d4...`; green CI |
| E-021 | `FileTools/Test-NuGetPackages.ps1` | Forbids Components/main-app dependencies |
| E-022 | FileTools component projects | Reference FileTools layers and ASP.NET Core only |
| E-023 | FileTools project files | Local package versions range up to `0.2.1` |
| E-024 | `CanDoItAll/docs/ui-support-scope.md` | Large desktop is the supported UI profile |
| E-025 | current CanDoItAll source/tests | Eleven known old icon asset/class consumers |
| E-026 | current CanDoItAll CSS/Playwright | `.cda-material-icon` is already a stable semantic hook |
