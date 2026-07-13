# .NET solution context is not an initialization plan (2026-07-11)

## Trigger

The first typed .NET driver contract correctly removed prose inference, but its
`bootstrap` document still required `dotnet new` templates and a conventional
new-project topology for every run. That made an existing, deliberately
non-conventional application look like a malformed scaffold request.

## Responsibility split

| Responsibility | Owner | Boundary | Test seam |
| --- | --- | --- | --- |
| Describe the selected solution, application, test target, and product-root-relative paths | Architecture/slice template and agent | Typed solution-context artifact | Parser and template tests |
| Decide whether a missing product must be initialized or an existing product must be verified | Architecture/slice template and agent | Explicit provisioning mode | Factory tests |
| Declare `dotnet new` templates and switches | Initialization plan only | Isolated .NET driver contract | Initialization-plan tests |
| Ground paths and reject root escape | .NET driver | Contract factory | Factory path-safety tests |
| Execute declared initialization or read-only verification | .NET setup driver | Existing runtime-owned tool boundary | Executor/script tests |
| Route child runs, manage retries, and materialize artifacts | Generic processes runtime | Opaque subprocess contracts | Generic runtime tests |

## Decision

The driver distinguishes an explicit `initialize` mode from `verify-existing`.
Both modes carry the same explicit solution context. Only `initialize` may carry
or consume a .NET template plan, call `dotnet new`, or repair solution wiring.
`verify-existing` performs no implicit generation: it verifies the declared
solution membership and project reference and fails with grounded evidence when
the declared context is not already valid.

The process core receives only opaque artifact binding metadata and does not
learn about .NET, templates, Blazor, test frameworks, or product layout. The
solution setup driver remains an isolated .NET extension, while the template and
agent instructions own the product-specific architecture decision.

## Rejected alternatives

- Loosening the existing new-project topology checks for every run would make a
  partially specified initialization request silently mutate an arbitrary tree.
- Inferring existing topology by scanning files would reintroduce hidden product
  design into a runtime driver.
- Adding a .NET conditional branch to the generic dispatcher would invert the
  dependency boundary and make an enterprise-generic runtime domain aware.

## Acceptance criteria

- A declared `verify-existing` context with non-conventional paths is accepted
  without templates or naming/layout assumptions.
- A declared `initialize` context still requires a complete initialization plan
  and preserves current path-safety checks.
- Verification-mode scripts do not contain project creation, solution-add, or
  project-reference mutation commands.
- No generic Process Runtime, Application, or Contracts type references .NET,
  Blazor, a template, or a specific product.
