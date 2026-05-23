# SB02 Semantic Invariants

## Shallow-Pass Trap

A change that merely mentions `WASM` in a prompt but still accepts a server-hosted `Microsoft.NET.Sdk.Web` app for a static/no-backend contract must fail.

## Adversarial Negative Proof

Use the observed contract and a bad output shape: server-hosted Blazor Web App, `AddInteractiveServerComponents`, and root `MainApp` instead of `main-app`. Expected result: validation rejects it.

## Semantic Positive Proof

Use a WASM/static-hostable or plain static output in the contracted root with localStorage persistence. Expected result: contract-fidelity validation accepts the mode/root shape.

## Anti-Stub Audit

Search changed prompt/policy/runtime files for stubs and fixture-only acceptance.

## Raw Note Literal Closure

- Closes `N002`, `N003`, and `N006` only after static/no-backend and root/mode fidelity are proven.

## Production Behavior Artifact Matrix

| Artifact/Signal | Producer | Consumer | Lifecycle | Negative-Test Citation |
| --- | --- | --- | --- | --- |
| Contract mode/root constraint | Contract artifact/grounding | Implementation and validation | Generated once, carried forward, compared to output | Pending |
| Contract revision requirement | Runtime/prompt | Executor/manager | Required before changing mode/root | Pending |
