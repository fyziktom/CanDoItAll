# SB00 — Re-anchor and Baseline

## Status

Proven

## Outcome

Establish a current, reproducible baseline for the exact checkout Codex will edit. Confirm package coordinates, project graph, MAF option construction, workflow HITL implementation, persistence/API conventions, and focused test discovery before any product change.

## Owned requirements

RQ-041 and the discovery prerequisites for every other requirement.

## Non-goals

- changing package versions;
- fixing baseline defects;
- implementing tool policy;
- implementing HITL;
- running all UI or solution tests;
- rewriting the bundle because harmless file names differ.

## Prerequisites

None.

## Reopen triggers

- IK-01 repository HEAD materially changes;
- IK-02 package coordinates change;
- IK-15 baseline build is red;
- IK-16 selected tests discover zero;
- any later subbundle discovers a missed direct MAF consumer, custom invocation client, workflow backend, persistence store, or API route.

## Exact sources and discovery

Read:

- repository root instructions;
- `CanDoItAll.slnx`;
- `Directory.Build.props`, `Directory.Build.targets`, `NuGet.config`;
- `src/MAF/MicrosoftAgentFramework.Packages.props`;
- all direct MAF consumer projects;
- current agent options factories and chat-client decorators;
- workflow abstractions/runtime/MAF adapter;
- workflow API;
- persistent workflow stores;
- existing tests listed in `inventories/TEST-MAP.md`;
- current bundle skills from `CanDoItAll.SharedInfo`.

Run and record:

```bash
git status --short --branch
git rev-parse HEAD
git log -1 --format=fuller
dotnet --info
dotnet restore CanDoItAll.slnx
python <bundle>/scripts/check_maf_upgrade.py <repo-root> --mode baseline
```

Use `rg` discovery commands from the impact map.

Run focused test listings or tests for:

- `MafApprovalSessionRoundTripTests`
- `MafWorkflowAdapterIsolationTests`
- `WorkflowRuntimeLifecycleRedGateTests`
- `WorkflowApiIntegrationTests`

Record actual discovered counts and baseline result. When a baseline test fails, distinguish a reproducible pre-existing failure from environment failure. Do not silently fix it inside SB00.

## Implementation boundary

SB00 may update only bundle execution state/evidence unless a repository-local instruction explicitly requires a harmless generated baseline artifact. No product source edit belongs here.

Produce:

- exact execution HEAD;
- clean/mixed worktree record;
- instruction hierarchy;
- resolved package graph;
- direct and transitive MAF consumer inventory;
- option/client composition inventory;
- workflow external request sequence;
- persistence migration convention;
- auth/API convention;
- focused test names/counts/results;
- broad baseline status or exception.

## Acceptance criteria

- current branch and HEAD are recorded;
- unrelated work is identified and protected;
- target versions are still available;
- stable versus preview package split is understood;
- no direct MAF consumer remains unlisted;
- every application-owned `ChatClientAgentOptions` and `FunctionInvokingChatClient` path is mapped;
- direct `ToolApprovalAgent` use is confirmed present or absent;
- old isolation provider symbols are confirmed present or absent;
- current HumanInput, approval, checkpoint, resume, API, and persistence path is documented against actual source;
- focused test filters discover more than zero tests;
- actual baseline counts become the minimum retained count;
- any baseline blocker is recorded before SB01.

## Proof tier

Standard

## Focused validation

- `python scripts/validate_bundle.py .` from bundle root;
- package/code scan script in baseline mode;
- focused test commands with `FullyQualifiedName` filters;
- affected project restore/build only as needed to establish baseline.

Expected discovery:

- each selected existing test class: greater than zero;
- exact counts captured in the closure record and propagated to later subbundles.

## Invalidation keys

IK-01, IK-02, IK-03, IK-04, IK-06, IK-07, IK-10, IK-13, IK-15, IK-16.

## Broad-gate decision

Do not run solution-wide tests. A single solution restore is allowed to establish package availability. A baseline solution build is allowed only when repository convention makes it the cheapest trustworthy way to expose package/API incompatibility; record its cost and outcome.

## Closure record

Passed on 2026-08-20.

- HEAD: `af425ac371b251447f9858b15476092531c686da` on `maf-update-and-hil`; only the
  bundle commit differs from preparation baseline `5cdf1666dbafdcea975909101c1854773f5f3556`.
- Worktree: clean at entry; no unrelated changes.
- SDK: `10.0.303`; `global.json` requests `10.0.302` with `latestPatch`.
- Restore: `dotnet restore CanDoItAll.slnx` passed after granting access to the configured
  user NuGet environment.
- Package graph: stable MAF `1.17.0`; A2A/Hosting preview
  `1.17.0-preview.260804.1`; all direct consumers remain the three projects listed in the
  impact map.
- Old symbol scan: no session-isolation provider symbols.
- Concurrency composition: the central options factory is the only production
  `ChatClientAgentOptions` construction site; no production `FunctionInvokingChatClient`,
  `UseProvidedChatClientAsIs = true`, enabled concurrency, or enabled declaration-only
  tool storage.
- Direct `ToolApprovalAgent`: absent.
- Workflow/API/persistence deviations: none; current source matches the preparation
  evidence. `CanDoItAll.slnx` contains product/tool projects but no test projects, so SB06
  must not treat `dotnet test CanDoItAll.slnx` as the broad test gate.
- CodeAnalytics: `snap-20260820203442-90bdd166`; 6 scoped projects, 364 documents,
  no blocking diagnostics and no project-reference cycle.
- Focused discovery: approval 12, adapter isolation 4, lifecycle 13, API integration 16.
- Focused results: combined unit selection 29/29 passed; API integration 16/16 passed on
  the authorized rerun.
- Environmental note: a pre-existing `CanDoItAll.Web` process locks Release web output.
  Preserve it and use isolated validation output/configuration rather than silently
  stopping it.
- Blockers: none.
- Progression: SB01 is unlocked. Later subbundles inherit the recorded discovery floors.
