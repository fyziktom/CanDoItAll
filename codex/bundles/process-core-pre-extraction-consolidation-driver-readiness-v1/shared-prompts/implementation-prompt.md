# Implementation Prompt

You are implementing a behavior-preserving architecture hardening bundle.

Rules:
- Do not create Process Core.
- Do not add production driver APIs.
- Do not remove behavior.
- Do not touch UI/browser/mobile surfaces.
- Keep execution report rows separate.
- Every phase gate must include build/test/source-scan proof.
- If a change would require behavior decisions, stop and record a red-team note rather than guessing.
