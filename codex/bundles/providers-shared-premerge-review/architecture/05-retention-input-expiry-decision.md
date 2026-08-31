# Retention execution refinement

The existing late-retry regression exposed a necessary dependency of SB04: deleting the final input tombstone loses the original capture deadline. Retaining tombstones forever would defeat the repair.

Freeze each logical request/input revision's detail expiry in the existing bounded HistoryAttemptCollection; carry the deadline in HistoryAttemptStart (defaulted from the original attempt so record copies preserve it). The recorder is the producer; persistence enforces the frozen input deadline before encryption and attachment. Later attempts keep independent response deadlines. Retry orchestration must preserve its existing typed invocation context; request IDs are not accepted from untrusted command JSON.

No new project, interface, process-global cache, persistence table or migration. Abstractions owns typed capture evidence; Persistence owns enforcement. SB03/SB04 and SB09 shared-contract proof are reopened for this additive contract change. Include producer-level late-retry proof after deletion, shared-input preservation and bounded concurrent cleanup.
