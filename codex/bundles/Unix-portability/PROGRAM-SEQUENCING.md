# Program sequencing

## Dependency graph

```mermaid
flowchart TD
    A00["A00 Anchor + full current inventory"] --> A01["A01 Logical path + config cleanup"]
    A01 --> A02["A02 Filesystem semantics + security"]
    A02 --> C1{"Core Gate C1"}
    C1 -- NO-GO --> A90["A90 Architecture correction"]
    C1 -- path/data incident --> A92["A92 Path/storage recovery"]
    A90 --> C1
    A92 --> C1
    C1 -- GO --> A03["A03 Storage + control-plane migration"]
    A03 --> A04["A04 Secrets + Data Protection + migration"]
    A04 --> C2{"Security Gate C2"}
    C2 -- NO-GO --> A90
    C2 -- secret/key incident --> A91["A91 Secret/key recovery"]
    A91 --> C2
    C2 -- GO --> A05["A05 Composition + capabilities"]
    A05 --> A06["A06 Headless hosting + operations"]
    A06 --> A07["A07 Three-platform CI + closure"]
    A07 --> C4{"Core Gate C4"}
    C4 -- NO-GO --> A90
    C4 -- GO --> B00["B00 Re-anchor runtime bundle"]

    B00 --> R0{"Runtime Gate R0"}
    R0 -- NO-GO --> B90["B90 Runtime architecture correction"]
    B90 --> R0
    R0 -- GO --> B01["B01 Execution primitives"]
    B01 --> B02["B02 Workbench runtime nodes"]
    B02 --> B03["B03 Manager supervision"]
    B03 --> R2{"Runtime Gate R2"}
    R2 -- NO-GO --> B90
    R2 -- GO --> B04["B04 MCP + external tools"]
    B04 --> B05["B05 Plugins + FileTools"]
    B05 --> B06["B06 Process-domain capability adaptation"]
    B06 --> R3{"Process architecture Gate R3"}
    R3 -- external dependency incident --> B91["B91 Dependency quarantine"]
    B91 --> R3
    R3 -- NO-GO --> B90
    R3 -- GO --> B07["B07 Runtime CI + E2E"]
    B07 --> R4{"Final Gate R4"}
```

## Why filesystem precedes storage and secrets

The requested order begins with basic slash/path work. The bundle then inserts filesystem semantics before storage and secret implementation because storage and key safety depend on:

- the correct logical/physical path distinction;
- root-specific case behavior;
- symlink/reparse containment;
- atomic write and cross-process locking;
- Unix modes and ownership.

Storage migration comes before secret migration because control-plane roots, Data Protection key-ring locations, database-profile files, and vault roots must already have stable path/atomicity semantics. This is a dependency decision, not a reduction in the priority of secrets.

## Core gates

| Gate | After | Required evidence | Reviewers | Unlocks |
|---|---|---|---|---|
| `C0` | A00 | Exact anchor, baseline, complete classified inventory, persistence map | Architect + runtime validator | A01 |
| `C1a` | A01 | Canonical logical paths, portable config, compatible path owners | Architect + security | A02 |
| `C1` | A02 | Case/determinism, link containment, atomicity, locking, modes, watcher convergence | Architect + security + runtime | A03 |
| `C2a` | A03 | Transactional storage/control-plane migration and rollback | Architect + data/runtime | A04 |
| `C2` | A04 | Secure providers, key-ring protection, legacy migration, restart, redaction | Architect + security + runtime | A05 |
| `C3a` | A05 | Narrow adapters, truthful composition/readiness, architecture guards | Architect | A06 |
| `C4` | A07 | Active Windows/Ubuntu/macOS CI, publish/start/restart/migration, rollback, exact handoff commit | Architect + security + QA/runtime + operator | B00 |

## Runtime gates

| Gate | After | Required evidence | Reviewers | Unlocks |
|---|---|---|---|---|
| `R0` | B00 | Core C4 anchor, full runtime inventory, ownership map, split decision | Architect + MAF/process owners | B01 |
| `R1a` | B01 | One execution primitive/lifecycle owner, executable/env/kill/redaction actual-host proof | Runtime + security | B02 |
| `R2` | B03 | Safe Manager ownership/discovery/termination and watcher convergence | Runtime + security + operator | B04 |
| `R3a` | B04 | MCP/external tool executable, secret, output, lifecycle proof | Runtime + security | B05 |
| `R3b` | B05 | Docker/FileTools/native dependency compatibility and truthful degradation | Integration + security | B06 |
| `R3` | B06 | Processes semantic ownership, capability-based strategies, authority/evidence proof | Process + MAF + security | B07 |
| `R4` | B07 | Full actual-host runtime E2E, failure injection, Windows regression, support matrix | All review roles | Program complete |

## Runtime split triggers

`B00` must create smaller child execution bundles before implementation when any of these are true:

- more than 60 production files are expected to change;
- more than eight project ownership boundaries require coordinated changes;
- source changes are required in an external NuGet/package repository;
- Manager process discovery, Workbench runtime nodes, and MCP cannot retain independent merge/review gates;
- the MAF/process architecture has materially changed since Core C4;
- an ordinary subbundle would contain unrelated migration, UI, native, and process-domain changes.

The current package remains the umbrella even when child bundles are created.

## Mandatory execution loop

For every mandatory or invoked conditional subbundle:

1. Read the root contract, current bundle README, subbundle prompt, findings, requirements, and source manifest.
2. Verify prerequisite gate evidence and exact source anchor.
3. Record git status and preserve unrelated work.
4. Reproduce baseline/characterization.
5. Add a failing-first test or named characterization artifact.
6. Implement only the subbundle scope.
7. Run focused actual-host validation.
8. Run the stable Windows regression gate.
9. Update requirements, source references, findings, ADRs, and evidence.
10. Obtain the required independent review.
11. Record GO/NO-GO and stop on NO-GO.
