---
name: csharp-architecture-review-gate
description: "Review C# architecture work before a subbundle or merge closes. Blocks fake separation, partial-class expansion, cyclic references, service-locator shortcuts, untestable extractions, and pattern cargo culting."
---

# C# Architecture Review Gate

Use this gate before closing critical architecture subbundles, before moving to dependent phases, and before merge.

## Review posture

Be strict and evidence-based. A shorter list of concrete blockers is better than generic architecture advice.

## Required review inputs

- changed C# files
- changed `.csproj` files
- CodeAnalytics snapshot id, dashboard health, dependency/cycle result, and relevant findings when the MCP is available
- responsibility inventory
- target boundary map
- pattern selection records
- test plan and transcripts
- build transcript
- source assertions
- proof manifest if this is a bundle subbundle

## Gate checks

### 1. Responsibility check

Passes only if each new type has one clear reason to change.

Block if:

- new type is a broad manager/helper
- old large class still owns the moved responsibility
- behavior was duplicated instead of moved
- nested classes were used as architecture boundaries

### 2. Partial-class check

Passes only if no new partial class was added, or the partial use is allowed by policy and has a removal plan when temporary.

Block if:

- a partial file was added to hide growth
- partial files group unrelated runtime capabilities
- tests still require the partial-class monolith

### 3. Project-boundary check

Passes only if references point in the intended direction.
Use CodeAnalytics `dependencies_get` and direct `.csproj` inspection when project references, providers, tools, memory protocols, process drivers, or runtime composition changed.

Block if:

- contracts reference implementations
- core references infrastructure
- runtime directly references every provider implementation
- a cycle was solved by dumping code into Common

### 4. Construction check

Passes only if builders, factories, and composition root have clean roles.

Block if:

- builder calls external providers
- factory owns unrelated business logic
- domain code injects `IServiceProvider` without a narrow factory reason
- `BuildServiceProvider` is called during registration

### 5. Testability check

Passes only if extracted behavior is independently testable.

Block if:

- unit tests instantiate the original runtime
- tests rely on filesystem/database/network for pure behavior
- tests assert only non-null outputs or counts
- no negative test exists for critical behavior

### 6. Extension seam check

Passes only if future additions use the new seam.

Block if:

- adding the next tool/provider/driver still requires editing the original runtime partial class
- catalog/factory exists but production path bypasses it
- implementation project has no registration path

## Review output format

```markdown
## C# Architecture Gate Result

Status: Pass | Blocked | Pass with follow-up

### Findings

| Severity | Finding | Evidence | Required action |
|---|---|---|---|

### Dependency direction

<brief result>

### Partial-class policy

<brief result>

### Testability proof

<brief result>

### Closure decision

<what may proceed and what must be reopened>
```

## Exit condition

The gate passes only when the code, project references, tests, and proof all support the same architecture claim.
