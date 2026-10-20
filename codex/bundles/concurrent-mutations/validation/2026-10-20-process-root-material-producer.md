# Process root material producer

Status: design accepted; implementation and behavioral proof pending.

Design: [root material preparation](../plan/P07-process-root-material-producer.md).
Predecessor: signed `0cea03fa4`; its 127 distinct passing behaviors cover capture
ownership prerequisites, not this producer.

Entry review: `snap-20261020221807-6bd2f38c`, five projects, seven project reference
edges, no project reference cycle. Existing Workbench module/type cycles and findings
are retained for the final scoped comparison.

Required closure includes real registered Workbench capture, before-claim dispatcher
wiring, competing/lost-acknowledgement and cancellation paths, finite budget accounting,
bounded original source reads, authority/lifetime failures, exact focused selections,
standard Release builds of changed production projects, full portability enforcement,
source/runtime identity verification and owned-resource reconciliation.

No implementation result, whole-suite result or E2E activation is claimed here.
