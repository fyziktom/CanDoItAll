# Assumptions And Risks

## Assumptions

- The previous artifact validation and write-coordinator behavior is correct unless current focused tests fail.
- The next seam should be tool validation, not Process Core.
- `ToolValidation.cs` is still part of application/runtime orchestration, not a pure domain core.
- Future process helper drivers will need stable semantic families, but not direct production driver APIs yet.

## Critical Path Risks

- Moving too much of `ResolveCompletionStatusWithCarryForward` at once can silently alter step closure behavior.
- Required-tool names are contract-like strings; renaming or normalizing them differently can break existing process prompts and receipt validation.
- Process mock satisfaction and carried implementation proof are special cases that can easily be dropped by a generic helper.
- Stack-specific .NET suppression must not become a generic rule that hides real failures for Rust/JS/business processes.
- Driver-readiness work can accidentally become premature driver-pack implementation if not explicitly constrained.

## Validation Risks

- Build-only proof is insufficient; focused tests must cover negative and positive cases for missing required tools and critical failures.
- Full process integration filters can be slow; use focused slices for iteration and a clean confirmation pass at gates.
- Browser proof should remain N/A; accidental UI proof can waste time and produce mobile screenshots again.
- Helper extraction can increase total code size initially; require dispatcher hotspot line-count tracking, not only total code shrink.

## Reopen Triggers

- Any helper references EF, storage, MAF, Tooling, Workbench, Projects, file-system APIs, or provider APIs without an explicit exception.
- `ToolValidation.cs` no longer compiles without direct access to helper internals.
- Missing required tool behavior changes for dotnet scaffold, browser proof, process mock, carried implementation proof, or workspace-write satisfaction.
- Critical failure behavior changes for superseded failures or stack-inapplicable dotnet failures.
- Any driver-pack or Process Core file appears.
- Any prohibited viewport proof artifact path appears.
