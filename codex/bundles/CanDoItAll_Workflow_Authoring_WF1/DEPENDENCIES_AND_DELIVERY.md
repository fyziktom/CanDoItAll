# Source pair and dependency delivery

Review main: `ccca2fd3a7c4239d2869e8143617fd9ab04c3723`.
CA1 executable source checkpoint: `73789c39598eb50eb06975ce6b6746e28015eb76`.
CA1 local Components proof: `b495d4c4a28f0a6588ba10bfaa7be6e8409eae18`.
Reviewed remote Components development: `4a858412d2c2a3f6123bf23d8c4584f05b47627d`.
These identify evidence, not required checkout/reset operations.

CA1 reports the existing Tooltip correction as local and unpushed. Verify the local commit, its signature,
actual loaded assembly and static asset hashes. Reuse it; do not duplicate or revert it. If a remote ref
now includes an equivalent tree, prove that equivalence and record the current ref. Local reproducibility
permits implementation to continue, while remote publication remains a separate delivery qualification.

Current CI resolves Components from the matching target/ref branch, then records its SHA for jobs.
It also has an explicit FileTools commit input. Local sibling mode and the CI pin need separate recorded
provenance; this bundle does not authorize updating all dependencies, pushing a sibling or changing the
branch resolver merely to hide a local/remote difference.

Record exact main/Components/FileTools and any deliberately changed sibling sources plus evaluated
project graph, assemblies and static assets. A matching package version alone is not proof. A Gitless
Docker build is identified by frozen inputs, immutable image ID and actual loaded binary/asset hashes,
not an invented Git suffix. Later test/docs-only changes require inspected source equivalence; actual
production/asset/build changes invalidate their affected native image proof.

Protected closures include old Workflows.UI, AgentFramework.UI/Editor, PP1/PP2, Workspace, Projects and
their independent sandboxes. Do not add a product-authoring dependency from a foundation/neutral library,
create cycles or reuse cached unresolved references. Native Web can compose all necessary leaves.
