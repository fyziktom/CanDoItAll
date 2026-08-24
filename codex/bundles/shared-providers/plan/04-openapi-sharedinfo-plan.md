# OpenAPI and SharedInfo plan

## Freeze prerequisite

Do not capture the final OpenAPI snapshot before:

- SB09 UI has not changed API contracts;
- SB10 docs/tooling are complete;
- route names, schemas, scopes, errors, and compatibility limits are frozen;
- focused OpenAPI integration tests pass.

## Capture

Follow the current `_candoitall-api-shared/README.md` instructions:

1. start a clean final Web host on the documented local port;
2. fetch `/openapi/v1.json`;
3. fetch `/swagger/v1/swagger.json`;
4. prove byte identity if current validator requires it;
5. compute SHA-256;
6. copy the canonical file into SharedInfo;
7. update manifest provenance and counts.

Do not capture from an old simple-chat branch or a dirty host without recording the worktree
state.

## New operation set

The SharedInfo manifest should include exact methods/routes for:

- catalog;
- models;
- Responses;
- Chat Completions;
- Images.

Do not include audio or management operations that are not implemented.

## Skill contents

`candoitall-api-shared-providers` must tell an agent:

- use live OpenAPI when target version differs;
- check `/api/access/status`;
- use catalog-read/invoke scopes;
- register source once and import selected publications;
- use catalog routing model IDs;
- compatibility subset and denied features;
- access-context header semantics;
- streaming/cancellation;
- image behavior;
- native versus OpenAI error envelopes;
- no upstream secret or endpoint discovery;
- route appendix.

## Validation

Run current SharedInfo validators and record:

- command;
- exit code;
- route parity;
- hash/provenance;
- skill front matter/name;
- stale source/branch scan;
- active-skill synchronization if applicable.
