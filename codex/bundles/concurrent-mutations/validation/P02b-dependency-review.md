# P02b intermediate dependency review

CodeAnalytics refreshed snapshot `snap-20261016170356-f6369a37` across nine selected
projects and 787 documents: SharedKernel, Infrastructure, Projects.Contracts,
Projects, Projects.UI, Workbench, Composition, Tooling and MAF. This is an intermediate
source review while native integration is still changing, not final architecture closure.

The scoped project edges remain acyclic. Infrastructure references SharedKernel,
not Projects or Workbench. Dataset activation orchestration joins Infrastructure and
Projects in Composition. Projects.Contracts references SharedKernel; Projects.UI
references Contracts. Workbench adapts its existing execution authority into Projects;
neither Projects nor Tooling references Workbench. MAF's restoration callback remains
Tooling metadata and does not depend on the Projects receipt implementation.

The dependency query reported two internal namespace clusters (Infrastructure
Persistence/ControlPlane/Storage and Workbench's existing rendering/structure clusters)
and six type clusters in existing authority leases, package import and skill preparation.
None includes the new operation protocol, caller binding, receipt codec or activation
coordinator. This is not a claim that the preexisting module internals are fully acyclic.

The loader reported **187 WS0002 errors**, all existing NuGet vulnerability audit
messages, plus 173 other diagnostics (duplicate generated attribute types, unresolved
scoped DI facts and export/persistence information). `hasBlockingErrors` is true.
Therefore this snapshot is useful scoped dependency evidence, not a healthy full
solution analysis. The actual Release build-backed Unit/Integration discovery passed;
Infrastructure also built directly after the transfer changes. Final source builds,
focused runtime checks and portability enforcement remain required.
