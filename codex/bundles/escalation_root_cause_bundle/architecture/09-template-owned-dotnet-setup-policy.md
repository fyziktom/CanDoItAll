# .NET setup driver executes operations; templates own their choreography (2026-07-12)

## Trigger

The .NET setup driver had correctly become the owner of deterministic solution
creation and readback, but it also contained maps keyed by concrete process step
names such as `create-dotnet-project` and `add-test-project`. That makes a
reusable driver depend on one process template's choreography.

## Decision

The `dotnet.launch-contract` activation supplies the per-step completion,
readback, receipt, and launch-variable-scope policy as typed template
configuration. The .NET driver validates that configuration, resolves its
declared variables, and executes the deterministic .NET operation. It does not
choose which process step invokes the operation.

The generic Processes Runtime continues to treat the resulting launch variables
as opaque process contracts. It contains no .NET, test-project, application,
or template-step knowledge.

## Responsibility split

| Responsibility | Owner |
| --- | --- |
| Process step names, sequence, branch shape, required evidence and variable visibility | Process template |
| Parse and validate template-owned .NET setup policy | Isolated .NET driver |
| Generate and execute deterministic .NET setup scripts | Isolated .NET driver |
| Resolve generic per-step launch-variable maps | Generic launch preparation/runtime |
| Decide product topology and initialize versus verify-existing | Architecture artifact and template/agent |

## Constraints

- The driver must reject absent or malformed policy configuration; it must not
  silently invent a default workflow sequence.
- A renamed template step must work when the template policy is updated, with no
  C# change.
- Runtime/dispatcher projects must not receive .NET-specific references.
- This is containment, not the eventual topology graph redesign. The follow-up
  replaces single application/test assumptions with template-bound project and
  reference operations.

## Acceptance criteria

- No `Drivers/DotNet` source contains the current `dotnet-solution-setup` step
  keys used solely to map workflow policy.
- Unit coverage proves arbitrary template-owned step keys materialize the
  correct isolated launch variables.
- Existing scoped-variable behavior still prevents an add-project operation
  from receiving unrelated initialization script variables.
