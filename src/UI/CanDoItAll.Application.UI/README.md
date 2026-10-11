# Application presentation

The application renderers consume immutable presentation values and explicit user intents.
They use AppComponents and BaseLib without Web, Composition, infrastructure, module stores,
readiness probes or profile activation services.

`DashboardSurface` renders the native snapshot's recent/active distinctions, usage uncertainty,
identity-bearing links and owner-selected activity. Web retains loading, per-profile caching,
the five-minute timer, clock, refresh admission and retirement. The route contract is `/`,
`/dashboard`, `/projects?projectId=…`, `/agents/workflows?workflowId=…&runId=…` and
`/processes/live?runId=…`; native route consumers validate those identities.

`RuntimeCapabilitiesSurface` renders the full mapped readiness snapshot and real
`CapabilityFact` children. The Web mapper owns native enum meanings and safe implementation
identity projection. Refresh requests a new native snapshot; the renderer performs no probes.

`DatabaseProfileDialogSurface` and `ShellUtilitiesSurface` receive safe display fields and
emit an opening identity plus the profile captured by the rendered control. MainLayout owns
activation admission, canonical readback, startup gating and pending-restart acknowledgement.
The native credential-bearing editor never enters this assembly. `ShellStatusBar` uses the
actual shared HelpPopover and retains its keyboard, hover and pinned behavior.

The [independent scenario host](../../Sandboxes/CanDoItAll.Application.UiSandbox/README.md)
uses the same production renderers. The [closure record](../../../docs/architecture/components-decoupling-closure.md)
tracks the current census, native integration, assets and evidence separately. The application
boundary tests inspect resolved assemblies, exported contracts and actual rendered children;
their negative controls include native parameters and a dynamically supplied native child.
