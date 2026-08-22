# Follow-on Request — Workflow HITL API Sample

Received 2026-08-21.

Create a standalone Blazor SSR sample at
`C:\programovani\dotnet\candoitall-sample-hitl-api` to exercise a real
CanDoItAll workflow through its HTTP API. The sample is intentionally small and uses plain
HTML, CSS, and JavaScript.

The sample must:

- collect a user name, start a workflow, and display an LLM-personalized greeting that asks
  for the user's main hobby;
- accept the hobby through the existing external-response API;
- observe the run-specific SSE stream and react to the durable human-attention signal;
- have the workflow propose three search topics and query a local simulated Wikipedia;
- retry at most three searches, produce a personalized answer when an article is found, and
  produce an explicit not-found answer otherwise;
- host the simulated Wikipedia API and approximately twenty file-backed hobby articles;
- keep the CanDoItAll API token server-side;
- repair a missing or broken workflow API command only after an end-to-end failure proves
  the gap;
- be validated with Playwright against several successful and unsuccessful conversations.

The sample may use the local Ollama `gpt-oss:20b` model to produce the article corpus.

