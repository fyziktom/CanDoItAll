# Structured Input

## Core Objective

- Add a safer and more discoverable filesystem tool family for agents.

## Success Criteria

- Agents have explicit tools for non-recursive and recursive directory listing.
- Agents can hash, zip, and unzip workspace paths using existing file-service behavior.
- Folder copy/create support is clear in tool metadata.
- Tool policy, templates, and tests know the new commands.
- Filesystem behavior is extracted from `WorkspaceRuntimePlugin`.

## Hard Constraints

- Do not bypass `IWorkspaceFileService`.
- Do not weaken workspace/external-target allowed-area checks.
- Do not add `MafAgentRuntime` partials.
- Do not silently approve mutation tools.

## Allowed Side Effects

- Production changes under MAF runtime workspace tooling, Core tool policy, capability templates, and tests.

## Source Artifacts

- User prompt in `inputs/00-original-request.md`.
- CodeAnalytics snapshot `snap-20260706235051-789dd62f`.

## Input Coverage Signals

- "read list of files in folder or with subfolders" maps to non-recursive and recursive list behavior.
- "copy folder, create folder" maps to clearer directory-capable copy/create tool descriptions and existing service validation.
- "allowed area" maps to direct use of existing file service and path policy.
- "where those functions are defined as tools" maps to extraction and catalog/template updates.

## Dependency And Sequencing Signals

- Extracted filesystem plugin must exist before runtime wiring is updated.
- Tool policy/template changes must land before final composition proof.

## Validation Expectations

- Direct unit tests, catalog/template tests, focused composition tests, and affected builds.

## Evidence Contract

- Focused `dotnet test` transcript.
- Affected `dotnet build` transcript.
- Bundle execution report and C# architecture gate update.

## UI Validation Strategy

N/A.

## Browser Validation Analytics

N/A.

## Working Assumptions

- The existing file-service implementation is correct and should be reused.
- `workspace_list_files` can remain as the existing recursive/glob-capable tool while a new `workspace_list_directory` makes shallow listing explicit.

## Primary Risks

- Duplicate runtime tool registration.
- Archive mutation classified as read.
- Extracted plugin still depending on broad runtime behavior.
