# SB10 semantic invariants

- SB10-I1: actual 5214 stale metadata becomes correct by explicit UI refresh without
  saving or overwriting the user's agent. Sol, Mini, Terra and unsupported GPT-4.1
  have model-specific option sets. Final image repeats Sol/Mini/GPT-4.1 checks.
- SB10-I2: source automatic/manual/reset UI is usable. Source manual Sol and custom
  Ollama settings persist, survive health discovery, and mirror read-only to clients.
  Temporary restrictions and the dedicated source-default test agent are restored.
- SB10-I3: real agents independently execute Sol Low-default/High-override, Mini
  Low/High and Ollama Low/High. Every request has HTTP 200 and Succeeded/Complete
  source usage. Final image has real Sol High and explicit Medium smokes. Output differences
  are not used to infer effort; outgoing source metadata is the evidence.

## Proof consumers

MCP configuration/execution/final results, inspected screenshots, source dispatch
joined to persisted usage by RequestId, Docker build/deploy transcripts and final
health records. No mock upstream, API-based setup, credential disclosure, approval
bypass or database reset counts as acceptance.

## Re-entry

Any source/client divergence, lost saved override, rejected valid effort or image
change invalidates the relevant matrix. Six runs used image1; image2 differs only
in desktop grid/wrapping and was checked again through UI and real inference.
