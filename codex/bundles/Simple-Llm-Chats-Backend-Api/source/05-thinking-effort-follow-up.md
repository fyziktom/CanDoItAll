# Thinking-effort follow-up — preserved input

```text
One important note: I am not sure if bundle covers also thinking efforts of models that chats will use.
We need it similar as in agents. A provider can have multiple models, each with different thinking
effort. Simple chats also need availability of effort setup.
```

Normalized interpretation:

- a definition revision has a strongly typed optional thinking-effort override;
- `null` means provider default and `None` explicitly disables thinking when the selected model permits it;
- allowed effort values are resolved per provider and model from the existing canonical provider
  capability policy, never from a second LLM-Chat-specific catalog;
- safe provider/model option projections expose the available effort choices for API clients;
- create/update rejects an explicit effort that the selected provider/model does not support;
- the immutable revision, settings fingerprint, provider request, and invocation audit preserve the
  requested/effective effort semantics.

