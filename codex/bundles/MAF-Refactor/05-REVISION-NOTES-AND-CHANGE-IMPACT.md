# Revision 2 notes and change impact

## Why this revision exists

The first bundle established the correct architectural direction. This revision adapts execution to Claude Code and closes the largest planning gap: how to switch high-risk production paths safely, find every affected caller, diagnose cross-boundary regressions, and hand work between models when credits or availability change.

## Major changes from version 1

1. Replaced Claude Code-specific prompts with Claude Code execution prompts.
2. Added a Fable 5 primary profile with model-independent fallback and durable session handoffs.
3. Expanded the code impact map from central types to complete call chains, DI roots, mocks, test hosts, public APIs, persistence, provider runtime, process completion, and Blazor lifecycle.
4. Added per-subbundle high-risk adaptation notes, safe cutover sequences, and bugfix procedures.
5. Expanded SB16 from a workflow-only port into a provider-backed lightweight LLM foundation suitable for future ordinary chat.
6. Added SB17 as a dedicated cross-cutting stabilization and bugfix phase before final deletion.
7. Moved the final release gate to SB18.
8. Added cutover, rollback, observability, fault-injection, caller-scan, and bugfix artifacts.
9. Strengthened scripts to detect dual paths, broad-runtime callers, process leakage, service location, mixed scope construction, and agent construction in lightweight LLM paths.

## Review conclusion

The most dangerous changes are not type renames. They are cutovers where several currently coupled concerns must remain behaviorally identical:

- execution coordination -> narrow runtime ports;
- root DI/manual graph -> one typed composition root;
- UI context scope -> independently resolved execution authority;
- generic/MAF process behavior -> Processes-owned policies;
- unversioned session state -> explicit compatibility envelope;
- bool approval -> stable per-proposal decisions;
- full agent runtime -> provider-backed lightweight LLM invocation.

Each now has a staged migration, one-path rule, rollback boundary, telemetry requirement, and post-cutover fault test.
