# Negative and source guards

## Behavioral negative proof

- A bearer token containing only broad `api` receives 403 on `GET /api/llm-chats`.
- A read token cannot create conversations or admit turns; manage cannot read; execute cannot read.
- An SSE request with a read JWT in the Authorization header passes authorization and reaches the
  unknown-operation 404. The same JWT in `?access_token=` receives 401.
- `origin: application` is rejected as an unmapped JSON member before the service runs; the same
  negative case passes against PostgreSQL and creates no row.
- Same operation ID with a different user message returns a stable conflict without either message or
  `requestFingerprint` in the body and without another provider dispatch.
- Definition detail responses omit the stored system prompt and its value.

## Source assertions

Automated bundle guards:

```text
Architecture boundary check passed.
Streaming/SSE source contract check passed.
Test-policy validation passed.
```

The origin/policy search shows one title-only `CreateLlmChatConversationApiRequest`, Web's explicit
`LlmChatConversationOrigin.Api`, and policy metadata on every LLM Chat route. The shared definition
status mapper applies manage once to its three generated endpoints.

The raw-log guard used multiline matching over the complete product project:

```text
rg -U -n "logger\.Log(Trace|Debug|Information|Warning|Error|Critical)\(\s*exception" src/Modules/CanDoItAll.Modules.LlmChats -g '*.cs'
PASS: no raw exception object is logged by LLM Chat product code
```

The first audit found four additional state-machine/application raw-exception overloads beyond the
executor path. All were corrected to retain operation/conversation IDs and exception type only; the
guard above then passed.

Additional production searches returned:

```text
PASS: no production partial expansion in LLM Chat scope
PASS: no dormant deployment or participant model in production LLM Chat scope
```

`git diff --check` passes. No wildcard CORS, anonymous endpoint, public widget, query-token handler,
deployment/participant field, new table/migration, TODO/FIXME, or `NotImplementedException` was added.

Result: Pass.
