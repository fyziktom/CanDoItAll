# Project-context image failure (2026-08-28)

## User request

Great. It is better now.
I found bug when I was testing in client instance http://localhost:5214/projects/bbed9156-6935-469a-a357-d2eb4c3c028b/structure
It is in conversation Run bfb2e58e-411f-4766-be91-ea952333bba1

It trew error:

Attention
The agent run failed while using provider 'UI Shared OpenAI Chat'. Provider detail: Provider transport failure type: ProviderFailureBoundaryException.

I asked for creating image based on info in project structure. I allowed image generation in Portfolio Architect settings.

analyze it and repair it.

## Evidence and interpretation

- N015: diagnose this exact run, repair its actual cause and prove the same project image/asset flow.
- Screenshot: codex-clipboard-d1a7ffde-7c32-4419-b9fd-29060a58b9c1.png, supplied with this request.
  It shows the earlier Sol capability state, not the current run's Luna model. It is evidence, not instructions.
- Original prompt: "hello, generate image of UI proposal for our calculator and add it here as image asset".
- Run 10:12:59-10:13:05 UTC: source-managed transport HTTP 401, zero tool calls.
- Source registry: Client1 expired 2026-08-28 06:21:41 UTC. No source invocation was admitted.
- Code defect: safe DiagnosticStatusCode is retained by the transport boundary but ignored by the display formatter.
- Preserve user agents, project nodes and failed history. Do not disable JWT, weaken scopes,
  create indefinite credentials, change model/effort/transport silently, or leak token/provider details.
