# Capabilities-02 entry adjudication

Entry: components-decoupling at d066d367d6abc147e046bee1308ea5f702e65cfd, independently confirmed locally and remotely. Clean working tree. Components c3e6aa03a878994c0ba8aed6af017d0be75f3796 and FileTools 7c7453c6583365ae5bd63f8fc6efc4a776e15818 remain live, unchanged.

1. Confirmed: existing controlled surface and cancellable read session remain accepted.

2. Confirmed: toggle mutates the live Draft.SelectedCapabilityIds before Save.

3. Confirmed: the unsafe characterization explicitly asserts a rejected attachment remains visible.

4. Confirmed: current-profile Save awaits Core save before CRM/HR and invalidation; the typed projection exception proves commit.

5. Confirmed: Core compares ExpectedUpdatedAtUtc inside the locked catalog update callback.

6. Confirmed, with qualification: catalog replacement precedes workspace-index write. CatalogDataRevision is embedded in the canonical document; the workspace index is a separate revision and does not make the catalog transactional.

7. Confirmed: cancellation after dispatch, including index/cleanup work, cannot establish rollback.

8. Confirmed: the host currently catches generic exceptions and displays their raw message.

9. Confirmed: busyGeneration is compared only to session.Generation.

10. Confirmed: reentry to A advances generation and hides the unresolved A operation.

11. Confirmed as required product decision: before set remains authoritative until commit/read evidence.

12. Confirmed: Verify reads inputs, runs proof, then updates current catalog without input comparison.

13. Confirmed: missing attachment still advances agent timestamp and updates global proof.

14. Confirmed, refined: proof rules observe files and HTTP; the proof service does not launch arbitrary MCP processes, but setup diagnostics can. No recovery may replay either.

15. Confirmed: legacy Task API exposes no typed stage or proof receipt.

16. Confirmed: preview contract has a token; host omits it.

17. Confirmed: both dialogs save global capability catalog; selection is not their owner. Their current rendered subtrees contain no nested DialogService.OpenAsync calls, so nested ownership is an audited empty set, not invented dialogs.

18. Confirmed: Curator busy is selected-generation owned although its identity is fixed.

19. Confirmed: launcher reserves a visible handle, creates durable chat session, attaches it, transfers context, then focuses shell. Failures after session creation do not prove absence; no blind replay.

20. Confirmed: broad Components already references lightweight UI. No movement in this child.
