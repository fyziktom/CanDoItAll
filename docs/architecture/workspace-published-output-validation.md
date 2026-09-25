# Published-output validation tools

## Decision and evidence

The September 25 process investigation found a capability gap in the read-only QA
step: it could restore, build, test and launch a project, but could neither publish
it nor serve its published static files. Run `9fd55aa2-0618-4c61-a9fa-bf6dcb998e69`
correctly reported the required static-output proof as NotVerified. A development
server is not equivalent evidence. This decision supplies generic validation
capabilities; it does not change process outcomes or implement the example product.

## Ownership and dependencies

- A registered workspace validation tool provider in the MAF adapter owns tool
  schemas and configured access policy. It uses the exact execution's workspace
  service through a narrow contract in Runtime.Abstractions, passed in the typed
  tool-provider context. Inventory construction does not create a second workspace.
- Core owns bounded publish/static-host command planning and the existing process
  runner, path policy, receipts and execution-run leases. Shared lifecycle execution
  remains the single owner of startup, explicit stop and terminal recovery.
- A small standalone static-host executable owns HTTP serving. It depends on the
  ASP.NET shared framework, not Core, process modules or MAF. The provider supplies
  its trusted assembly location; agents cannot choose another executable. Core has
  no dependency on this concrete host or on ASP.NET.
- Tooling references Runtime.Abstractions for the narrow command contract; Core
  already references it. MAF references the host and both contracts. No reverse edge
  or process-core dependency is introduced.

## Contracts and alternatives

Publish accepts a project, configuration and bounded timeout, with output in a
fresh managed artifact directory. Build intermediates retain the existing validation
command semantics. Serving accepts an authorized directory and loopback URL, reads
files only, and emits owned startup/cleanup receipts. It has an execution-run lease
and uses the existing stop tool. The host provides explicit SPA fallback and
revalidation headers as disposable test infrastructure, without claiming to prove
any separately requested production deployment.

The design composes a registered provider with command planning and the existing
lifecycle. An arbitrary shell grant would widen QA authority; adding launch scripts
or changing the sample application would transfer responsibility to the observer.
A second process supervisor would duplicate cleanup and recovery. Putting HTTP
hosting into Core would add an unnecessary framework dependency. None is needed.

## Proof plan

Contract tests cover publish arguments/output confinement, scope/path rejection,
configured permission and governed operation classification. Host tests cover actual
published-file bytes, MIME types, cache headers, SPA fallback, missing assets,
unsupported methods and path/link escape rejection. Managed launch tests cover
startup receipt, owned stop and execution-run cleanup. Composition tests confirm
the registered provider receives the original workspace service and that inventory
exposes the same tools. Existing lifecycle tests protect shared behavior.

After focused tests and portability enforcement, a real process must perform its
own publish, browser/header proof and cleanup and reach Completed without an
escalation. A successful build alone does not close this issue.

Initial scoped CodeAnalytics evidence: snapshot
`snap-20260925113238-790bd625`, healthy Core/Models scope, 327 documents. This is
bounded discovery evidence, not a claim about the complete application's graph.

## Architecture gate

Status: Pass for the tooling change; live process acceptance remains a separate gate.

The six-project snapshot `snap-20260925115231-f56c0e9e` loaded without diagnostics or
blocking errors and has no project-level cycles. Direct project-reference inspection
also found no cycles across 122 production projects. Core and the contract projects
do not reference the static HTTP host. No partial class, service locator, nested
implementation boundary or second process supervisor was introduced.

The new provider and runtime adapter own schema/access handling; the command planner
owns bounded command construction; the executable owns HTTP responses. The current
execution's workspace command service is passed through the typed composition seam.
Saved results use the existing workspace source/path authority. Scope and permission
failures remain explicit.

The focused regression set has 201 discovered cases: 101 provider composition,
74 command/lifecycle, 15 capability-policy and 11 published-output cases. The first
run passed 198; three exact catalog expectations needed the two new tool records.
After preserving LF in their raw-string expectations, all 15 policy cases passed.
The real published-output cases passed both explicit stop and terminal cleanup,
including file bytes, response headers, WebAssembly MIME type, SPA routes, missing
assets, method rejection, traversal and symlink escape rejection. The process
catalog topic also passed all 76 cases. MAF and Web builds passed without warnings or
errors. Validation transcripts are retained in the September 25 investigation.

The optional static-validation capability is exposed by the validation workspace
profile. The generic Blazor template does not unconditionally require static hosting
for every rendering architecture; its resolved contract determines the required
proof. Existing required tool assignments and process outcome gates remain intact.

Live validation also exposed a receipt-selection defect in the browser driver: after
QA stopped both its development host and static host, the gate compared the latest
required start with the latest stop regardless of host identity. The driver now finds
the successful stop by the same startup receipt in the current execution and restricts
browser evidence to that lifecycle's time interval. This preserves the latest-host,
cleanup and current-execution requirements without adding product knowledge to Core.

The static-host executable's runtime configuration is copied through the SDK project
reference contract. No fixed `bin/<configuration>/<framework>` content path is needed.

Run `d4e15b68-d553-4344-b85d-d7eac4725b2e` exposed a Windows launch constraint after
published output moved beneath its process-run scope. The 270-character content root
was valid for file access but failed process creation as the working directory
(native error 267). Serving now launches from the workspace root, passing the same
authorized content directory as an absolute argument. The long-root real-host case
exercises this distinction without weakening path confinement or cleanup.
