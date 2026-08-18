# SharedInfo standards used by this bundle

## Impacted-test selection

The current `candoitall-codeanalytics-mcp` skill requires:

- only actual diff files under `changes`;
- one-based inclusive line ranges;
- inspected but unchanged files under `contextOnlyPaths`;
- every relevant runnable test workspace supplied;
- `behaviorIntent=Unknown` first;
- `BehaviorPreservingImplementation` only after a justified conservative result;
- healthy workspaces and nonzero source/test discovery;
- every required selector executed;
- conditional selectors promoted when a returned trigger occurs;
- zero or unexpected discovery treated as invalid proof;
- `AllSuppliedSuites` treated as an executable requirement, not an empty selector set.

## Bundle execution

The current bundle execution skill requires:

- one coherent subbundle outcome at a time;
- risk-proportionate proof;
- targeted validation by default;
- unfiltered gates only after a named invalidation trigger and at one frozen checkpoint;
- Components MCP and browser truth for Blazor UI;
- a named large-screen desktop viewport for CanDoItAll application proof;
- architecture evidence before and after dependency changes;
- no unplanned project references or partial-class expansion;
- architecture review before closure.

## Architecture

The current architecture governor rejects fake modularity:

- interfaces without independent owners;
- abstractions implemented only by the original monolith;
- partial-file growth;
- service location;
- dependency cycles;
- projects used as dumping grounds;
- tests that still require the full old runtime.

This bundle's neutral project and compatibility adapters are designed around those constraints.
