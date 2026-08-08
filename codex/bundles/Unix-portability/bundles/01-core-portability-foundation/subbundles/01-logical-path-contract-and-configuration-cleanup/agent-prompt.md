# Agent prompt — A01 Logical path contract and portable configuration cleanup

You are the senior C# architect and implementation agent for **CanDoItAll Core Portability Foundation**.

## Objective

Fix the lowest-level slash, root, and path-category semantics before storage, secrets, or runtime changes.

## Required reading

1. `../../../../CODEX-EXECUTION-CONTRACT.md`
2. `../../README.md`
3. this subbundle `README.md`, `tasks.md`, `validation.md`, and `exit-criteria.md`
4. `../../requirements/requirements.json`
5. `../../analysis/01-prepared-findings.md`
6. `../../inventories/source-reference-manifest.json`
7. relevant ADRs and prior gate/session handoff

## Execution instructions

- Work only on `A01`.
- Verify HEAD and dirty state before edits.
- Use CodeAnalytics/solution analysis where available before broad changes.
- Add failing-first tests or named characterization evidence.
- Prefer existing owners and narrow ports; do not create a parallel framework.
- Preserve Windows behavior and existing data.
- Run focused and stable gates; use actual Windows/Linux/macOS hosts when required.
- Update bundle evidence and stop on every NO-GO.
- Keep all source-code comments in English.
- Do not commit, push, or open a PR unless explicitly instructed.

## Source hotspots

- `{{REPO_ROOT}}/src/App/CanDoItAll.Web/appsettings.Development.json`
- `{{REPO_ROOT}}/src/App/CanDoItAll.Web/Properties/launchSettings.json`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/ControlPlane/ControlPlanePaths.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/FileSystemStoragePathPolicy.cs`
- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Paths/WorkspacePathPolicy.cs`
- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimePathResolver.cs`

## Tasks

- **A01-T01 — Introduce an explicit path taxonomy:** Create or document narrow types/policies for logical locators, physical host paths, routes/URIs, executable identifiers, and opaque script/command text. Do not introduce a broad platform god service.
- **A01-T02 — Define canonical logical path serialization:** Emit '/' for logical paths, reject rooted/traversal forms, and add field-scoped legacy backslash readers. Preserve Unix filenames that legitimately contain backslash outside known logical fields.
- **A01-T03 — Replace Windows-only development roots:** Remove shared %LOCALAPPDATA%\... defaults from appsettings and launch profiles. Resolve platform defaults in code/configuration while retaining explicit Windows legacy input compatibility.
- **A01-T04 — Define portable configuration expansion:** Support '~' and a documented variable syntax with bounded expansion. Treat unset or recursive variables as diagnostics; never expand arbitrary secret or user-authored artifact content.
- **A01-T05 — Align path owners without violating dependencies:** Make WorkspacePathAccessGuard, FileSystemStoragePathPolicy, WorkspacePathPolicy, and MafRuntimePathResolver consume compatible pure semantics. Add a new abstractions project only if dependency analysis proves it necessary.
- **A01-T06 — Version external-root aliases:** Replace drive-letter-only aliases with a platform-neutral root identity and retain a reader/migration for existing aliases.
- **A01-T07 — Detect foreign absolute path syntax:** Recognize Windows drive/UNC paths on Unix and Unix absolute paths on Windows as host-bound/unresolved records. Never pass them through Path.GetFullPath as relative input.
- **A01-T08 — Add golden and actual-host path tests:** Cover separators, case, Unicode, dot segments, empty segments, environment tokens, home expansion, routes, URLs, drive paths, UNC paths, Unix roots, and round-trip serialization on all three OSes.
- **A01-T09 — Run focused source scan and Gate C1a:** Prove no blanket slash replacement, no shared Windows root, and no divergence among path owners before filesystem work starts.

## Exit

- All path categories and ownership boundaries are documented and tested.
- New logical path writers are host-independent and legacy readers are field-scoped.
- Linux/macOS development roots no longer depend on %LOCALAPPDATA% or backslashes.
- Gate C1a is GO; A02 is the only next mandatory subbundle.
