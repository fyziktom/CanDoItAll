# Current census and enforceable boundaries

## Complete the census once; maintain a delta afterwards

Start from existing family inventories and maintained boundary documents. Reconcile them with
current tracked and newly added production source. Do not reread all historical bundle
instructions as though they were active tasks. Record the main and sibling source identities,
configuration, dependency/asset mode, actual projects indexed by CodeAnalytics and unresolved
areas. PC4's one-project index cannot certify the rest of the repository.

Inventory all production roots, including App, UI, Modules, Integration, MAF, Memory, Processes,
Foundation and plugins where applicable. Account for linked/generated source, evaluated Compile/
Razor items, custom MSBuild imports and external component packages. Include Razor files and
C# `BuildRenderTree`/component classes; then reconcile route discovery, dynamic component types,
dialog opens, deferred content, plugin/conversation contributors, registry/reflection entries,
JS-driven content and their real visual descendants. Examples/tests/sandboxes are separate roles,
not proof that a product entry is active or inactive.

`tools/collect_inventory.py` is an optional local read-only seed collector. It emits lexical
signals, file hashes and unevaluated project references from Git-indexed/current untracked
`src` files. It does not resolve MSBuild, semantics, supported plugins, runtime activation or
transitive package contents. Unsupported/unreadable/missing entries are explicit issues.
Do not call its output a successful architecture gate. Reuse a better existing repo tool
instead when available, and preserve the coverage description.

## Required row fields

Each actual renderer/host/activation row records a stable identity, current path/type/project,
source digest, route or real caller, classification, native state/effect owner, visual owner,
actual descendants/dynamic activations, assets, independent scenario, relevant test/evidence
references, and disposition rationale. Avoid copying entire historical test manifests into it.

Allowed final classifications:

- `EXTRACTED_RENDERER`: reusable visual logic has its intended light owner and proven child/asset closure.
- `NATIVE_HOST`: route, orchestration, effect, context or lifecycle owner; retained markup is legitimate composition rather than an unextracted reusable subtree.
- `FRAMEWORK_HOST`: framework App/Routes/reconnect/error/not-found glue, with precise justification.
- `RETAINED_COMPATIBILITY`: no current product activation found, with complete activation search and retained public compatibility rationale.
- `NON_RENDERING`: import, assembly marker or backend-only source; concrete evidence required.

`REMAINING` and `UNCLASSIFIED` are valid work-in-progress states, not successful closure states.
A path is not complete merely because it moved under `src/UI`. A class is not unfinished
merely because its native host injects services. The current Components.Git marker-only
project illustrates why raw project-reference alarms need semantic classification.

## Evaluate the actual dependency boundary

For light renderers and independent scenario hosts, inspect evaluated direct/transitive project
and package references, resolved runtime assemblies, public parameter/member types and actual
rendered children. Compare source dependency mode with supported package mode where relevant.
Unresolved references are unknown, never absence of a forbidden dependency. Identify property-
conditional or imported edges instead of trusting raw csproj XML alone.

The real application remains a native composition host. It is expected to reference modules;
that does not exempt a reusable leaf from isolation, nor make the root an invalid project.
Generic contributor contracts are allowed; an old unextracted product renderer hidden behind
a delegate is not independent rendering. Preserve proper existing contracts without quotas.

## Bounded guards rather than another architecture framework

Extend current boundary tests/scanners where suitable. Add focused guards for the completed
application leaf/contract and its sandbox, actual descendant types and evaluated forbidden
edges. Include negative controls: direct and transitive heavy edges, unresolved reference,
native public parameter, and a hidden/dynamically supplied forbidden visual descendant where
the existing guard supports that analysis. Do not claim a lexical guard checks such cases.

Track new/changed production rendering entries so the maintained register cannot silently
miss them. A changed hash requires review/disposition, not automatic approval by updating a
baseline. Guards must not rely on an exact file count, partial-class count, interface quota,
namespace label alone, or permitting every Foundation/contracts-looking assembly.

The final current register lives in maintained product docs, for example
`docs/architecture/components-decoupling-closure.md` plus its structured companion. Link it
from the architecture/UI indexes. Shared remains the stable technique, not a live task board.
