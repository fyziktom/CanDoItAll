# Phase plan

## Subbundle Dependency Map

```mermaid
flowchart TD
    SB01["SB01 Validation evidence and scope cleanup"]
    SB02["SB02 Runtime vs pending activation contract"]
    SB03["SB03 Remove dead hot-switch/drain state"]
    SB04["SB04 Maintenance profile context factory boundaries"]
    SB05["SB05 Parallel processing after PostgreSQL claims"]
    SB06["SB06 Claim-token canonicality for process dispatch"]
    SB07["SB07 Claim-first candidate loading"]
    SB08["SB08 Final validation, benchmark, merge gate"]

    SB01 --> SB02
    SB02 --> SB03
    SB03 --> SB04
    SB04 --> SB05
    SB04 --> SB06
    SB06 --> SB07
    SB05 --> SB08
    SB07 --> SB08
```

## Critical Subbundles

- SB02: canonical UI/API state split.
- SB03: removes dead switch/drain semantics.
- SB06: process mutation ownership.
- SB07: process candidate loading and scheduler semantics.

## Phase Gates

- Do not start SB05 before SB04 proves profile-specific context creation is maintenance-only.
- Do not start SB07 before SB06 proves stale dispatch claims cannot commit.
- Do not close SB08 until concurrency stress tests prove no duplicate processing and no stale claim commits.
