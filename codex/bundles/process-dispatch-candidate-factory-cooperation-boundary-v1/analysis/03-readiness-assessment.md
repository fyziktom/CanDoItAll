# Readiness Assessment

## Is Process Core ready?

Not yet.

The repo is closer, but the dispatch candidate boundary still mixes module runtime semantics, EF-loaded snapshots, execution-client queries, current assignment facts, technical-agent binding, cooperation metadata and route-specific `DispatchCandidate` object construction.

Before `CanDoItAll.Processes.Core` extraction, at minimum these seams should be clearer:

1. Candidate construction/factory.
2. Candidate route result vocabulary.
3. Cooperation/workspace-profile classification.
4. Technical-agent binding side-effect boundary.
5. Recovery execution id/manual directive boundary.
6. Dispatch orchestration outcome boundary.

## Is driver API ready?

Not yet as production code.

However, this bundle should prepare documentation-only driver readiness:

- map candidate route kinds to future driver needs,
- map workspace tool profile to future helper-driver families,
- identify what future drivers may inspect or provide,
- keep this as documentation/inventory only.
