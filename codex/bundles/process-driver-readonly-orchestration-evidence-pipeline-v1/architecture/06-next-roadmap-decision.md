# Next Roadmap Decision

## Decision
Next candidate decision: `Continue read-only domain-driver expansion and manager-visible projection planning`.

Controlled read-only runtime integration: `Blocked`.

Runtime host status: `Not approved`.

Prerequisite status: `Not satisfied`.

## Why
The bundle now has an explicit typed gateway, supplied-evidence process orchestration, multi-domain integration proof, runtime-host denial tests, source-backed docs, release-candidate smoke proof, and red-team validator preflight. That is enough for the next bundle to expose or project read-only verification results more ergonomically, but it is not enough to run a production runtime host.

Controlled runtime integration still lacks durable audit persistence, lifecycle ownership, authorization and approval semantics, sandbox and allow-list policy, failure semantics, and compatibility governance. Until those are source-backed and tested, runtime integration remains blocked.

## Approved Next Candidate
- Add more read-only domain drivers that accept caller-supplied in-memory evidence only.
- Add manager-visible read-only projection planning over existing verification observations.
- Reduce direct process-module driver coupling only where the explicit gateway can preserve typed request construction.
- Harden audit/redaction/no-mutation tests for new read-only lanes.

## Blocked Candidates
- Generic runtime host.
- Driver registry, runtime selector, provider, pack, or service registration.
- Manager command, scheduler hook, or workflow hook that invokes drivers.
- File/network/storage/workspace access from verification paths.
- Execution-capable drivers.

## Gate To Reconsider Runtime Integration
Runtime integration may be reconsidered only after a new bundle supplies source-backed proof for every prerequisite in `architecture/04-runtime-host-decision.md` and preserves the explicit typed gateway contract.
