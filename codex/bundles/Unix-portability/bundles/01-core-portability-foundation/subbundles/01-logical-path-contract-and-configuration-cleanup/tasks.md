# A01 tasks

## Entry checklist

- [ ] Verify exact checkout and preserve unrelated working-tree changes.
- [ ] Verify prerequisite gate evidence.
- [ ] Reproduce focused baseline/characterization.
- [ ] Confirm every source hotspot after materialization.

## A01-T01 — Introduce an explicit path taxonomy

- [ ] Create or document narrow types/policies for logical locators, physical host paths, routes/URIs, executable identifiers, and opaque script/command text. Do not introduce a broad platform god service.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A01-T02 — Define canonical logical path serialization

- [ ] Emit '/' for logical paths, reject rooted/traversal forms, and add field-scoped legacy backslash readers. Preserve Unix filenames that legitimately contain backslash outside known logical fields.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A01-T03 — Replace Windows-only development roots

- [ ] Remove shared %LOCALAPPDATA%\... defaults from appsettings and launch profiles. Resolve platform defaults in code/configuration while retaining explicit Windows legacy input compatibility.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A01-T04 — Define portable configuration expansion

- [ ] Support '~' and a documented variable syntax with bounded expansion. Treat unset or recursive variables as diagnostics; never expand arbitrary secret or user-authored artifact content.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A01-T05 — Align path owners without violating dependencies

- [ ] Make WorkspacePathAccessGuard, FileSystemStoragePathPolicy, WorkspacePathPolicy, and MafRuntimePathResolver consume compatible pure semantics. Add a new abstractions project only if dependency analysis proves it necessary.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A01-T06 — Version external-root aliases

- [ ] Replace drive-letter-only aliases with a platform-neutral root identity and retain a reader/migration for existing aliases.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A01-T07 — Detect foreign absolute path syntax

- [ ] Recognize Windows drive/UNC paths on Unix and Unix absolute paths on Windows as host-bound/unresolved records. Never pass them through Path.GetFullPath as relative input.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A01-T08 — Add golden and actual-host path tests

- [ ] Cover separators, case, Unicode, dot segments, empty segments, environment tokens, home expansion, routes, URLs, drive paths, UNC paths, Unix roots, and round-trip serialization on all three OSes.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A01-T09 — Run focused source scan and Gate C1a

- [ ] Prove no blanket slash replacement, no shared Windows root, and no divergence among path owners before filesystem work starts.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [ ] Every owned requirement has evidence and status.
- [ ] Focused validation and required stable regression pass.
- [ ] Source references/findings/ADRs/traceability are current.
- [ ] Artifacts are redacted.
- [ ] Required independent reviewers record GO.
- [ ] Handoff identifies the next eligible subbundle or conditional stop.
