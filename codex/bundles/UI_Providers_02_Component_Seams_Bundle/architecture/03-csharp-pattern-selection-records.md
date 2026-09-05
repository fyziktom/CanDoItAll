# Pattern decisions
1. Immutable submission value + target mutation session: required to keep UI draft independent of asynchronous writes and correlate later reconciliation. Rejected passing mutable editor and a component-wide busy flag; direct delayed-command Unit proof.
2. Provider command adapter: real outcome translation/owner token boundary, not a bag returning existing services. Keep read adapter separate. Rejected duplicating registry transaction guesses in Razor; database proof required.
3. Typed external change scope: source application already knows affected IDs. Rejected generic Changed and full editor reload; tests distinguish local/new/unaffected/affected/retired.
4. Per-target shared session and overlay lifetime: cancellation + generation must survive reentrant parameter/render lifecycles. Rejected loaded-ID bookkeeping and global CloseAll; direct/component stale/disposal proof.
5. Explicit first publication command: contract A avoids read-side permanent lifecycle changes. Keep existing permanent public identities/audit protection; rejected auto-deleting publication rows or inventing identity removal.
