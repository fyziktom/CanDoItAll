# Architecture gate

Before snapshot: `snap-20260815072303-363bd134`  
After snapshot: `snap-20260815080824-3b5bd776`

## CodeAnalytics result

- scoped projects: Web, Workspace, LLM Chats product, and LLM Chats Persistence;
- documents/types/members: 191 / 609 / 4,140;
- DI registrations: 53;
- direction: Web -> product and Workspace; Persistence -> product; product and Workspace have no
  project references in the scoped graph;
- cycles/blocking errors: 0 / 0;
- findings/open questions: 182 existing heuristic findings / one existing Workspace DI collector
  ambiguity;
- diagnostics: two existing duplicate embedded-type warnings plus informational diagram truncation and
  the existing DI collector note.

## Manual review

Status: Pass.

- `git diff fd33ab9..4ec4d269 -- src` is empty: no production owner, reference, schema, or protocol changed;
- architecture and SSE source guards pass;
- no production LLM Chat partial declaration exists;
- the real-host heartbeat assertion strengthens the consumer proof at the existing Web transport owner;
- the repository-root correction is test infrastructure only and fails explicitly for a configured
  path that does not contain `CanDoItAll.slnx`.
