# MAF 1.20 Assessment

## Decision

Upgrade from MAF 1.18 to 1.20 as a separate foundation subbundle before the incident repair. The upgrade is maintenance and compatibility work. It does not fix the reported asset-call failure by itself, and the bundle must retain SB01–SB06.

## Current and target dependency sets

The repository currently declares MicrosoftAgentsAIStableVersion 1.18.0 and MicrosoftAgentsAIPreviewVersion 1.18.0-preview.260818.1 in repo://src/MAF/MicrosoftAgentFramework.Packages.props. Restored agent assets contain MAF 1.18, MEAI 10.8, OpenAI 2.12 and OllamaSharp 5.4.25.

Target stable MAF packages are 1.20.0. Matching A2A/Hosting preview packages are 1.20.0-preview.260831.1. MAF 1.20 requires Microsoft.Extensions.AI 10.9 and several Microsoft.Extensions foundation packages at least 10.0.11. The repository directly pins six MEAI references to 10.8.0 and MicrosoftExtensionsPackageVersion to 10.0.10.

An isolated negative restore reproduced NU1605 when Microsoft.Agents.AI 1.20.0 was combined with DependencyInjection.Abstractions 10.0.10. The upgrade therefore cannot be a one-line MAF property edit. It needs an aligned dependency change, restore graph inspection, full production build and the named broad gate.

Keep OpenAI at 2.12.x. MEAI OpenAI 10.9.0 explicitly constrains OpenAI below 2.13 because of an incompatibility. Do not combine this work with an OpenAI SDK upgrade.

## Release-content relevance

MAF 1.20 release notes include Foundry-hosted workflow response cancellation, a timeout for wait-for-first-completion, Foundry recovery test stabilization, and other unrelated integrations. They do not list a general local-function binding diagnostic or tool-failure-to-run-status fix.

MAF 1.19 is more adjacent:

- Its file-tool description fix changed only Description text that used snake_case while AIFunctionFactory exposed camelCase schema names. Upstream explicitly says schema/binding behavior did not change. This validates an additional local audit: tool descriptions and examples must use the exact generated argument paths.
- Its session-persisted RoutingChatClient is additive and experimental. It helps a client switch model routes while preserving client-side history. CanDoItAll currently creates provider-specific agents per run and deliberately reconstructs canonical context. Adopting the router is not required for this defect and would not make persisted application tool evidence authoritative.
- Its MCP Tasks extension change is breaking for MAF's long-running MCP task feature. CanDoItAll has its own ModelContextProtocol clients and no source reference to that task extension, but MCP setup/call/cleanup tests remain required upgrade regressions.
- A2A streaming artifact changes make existing A2A tests part of the upgrade surface.

MEAI 10.9 adds routing/failover APIs and fixes concurrent ExcludeFromSchema behavior. Its release notes do not list a missing-required-argument feedback change.

## Direct 1.20 probe

The isolated probe compiled and ran MAF 1.20.0, Workflows 1.20.0 and MEAI 10.9.0. Its generated tool still required projectId and request. The captured malformed project_id/flat shape still threw System.ArgumentException for missing projectId, and the delegate ran zero times. The corrected nested shape ran once.

This proves the package update does not address F01. CanDoItAll's unchanged generic catch would still convert that non-IAgentToolFailure exception into the generic SDK-visible failure. The result does not depend on direct versus shared provider transport because binding occurs in the local function boundary.

Evidence:

- bundle://analysis/maf-1.20/result.log
- bundle://analysis/maf-1.20/downgrade-result.log
- bundle://analysis/maf-1.20/dependency-closure.json
- bundle://analysis/maf-1.20/Program.cs.txt
- bundle://analysis/maf-1.20/Probe.csproj.txt
- bundle://analysis/maf-1.20/provenance.json
- bundle://analysis/maf-1.20/sources.md

## Agent completion versus workflow completion

The reported run is an ordinary agent execution. CanDoItAll receives a normal agent response plus ToolInvocationTraces, then its own AgentFrameworkWorkspaceExecutionService decides Completed/Succeeded without assessing the failed mutation. That decision remains application-owned under MAF 1.20.

The local MAF workflow adapter uses a separate path. MafWorkflowStreamingRunDriver observes WorkflowErrorEvent; MafWorkflowTurnResultMapper and the legacy driver override even an ended/idle MAF status to WorkflowRunState.Failed when normalized Error or ExecutorFailed events exist. The known workflow problem described by the user is therefore not evidence for this ordinary-run root cause.

A MAF agent may treat a tool exception as conversational input and still finish its turn. If such an agent is ever embedded in a workflow without carrying typed CanDoItAll tool outcomes, the outer workflow may see no WorkflowErrorEvent. No such integration was found in the current workflow source. SB00 must characterize hard workflow errors/cancellation after upgrade; SB02 remains responsible for ordinary interactive mutation outcomes.

## Upgrade scope

SB00 updates and validates the dependency family without claiming the incident fixed. It also adds a schema-description conformance check for the asset tool and representative catalogs, using the exact camelCase/nested paths exposed to models. This is useful for smaller models and follows the upstream 1.19 lesson, while safe binding feedback remains mandatory.

If SB00 changes generated schemas, streaming contents, session state, workflow events or result shapes, update captured baselines and reopen affected SB01–SB06 assumptions before implementation continues.
