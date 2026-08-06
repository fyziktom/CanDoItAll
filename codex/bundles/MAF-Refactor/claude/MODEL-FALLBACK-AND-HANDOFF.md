# Model fallback and handoff protocol

## Preferred fallback selection

When Fable 5 cannot continue, prefer Claude Opus 5 only when that model is configured and available in the current Claude Code environment. Otherwise use the best available high-capability Claude model. Do not invent an unavailable model ID or reduce the bundle gates to fit a cheaper model.

## Trigger

Use this protocol when Fable 5 is unavailable, credits are exhausted, a context window is nearing its limit, or a fresh review session is safer.

## Required durable state

- repository HEAD, branch, and working-tree status;
- active subbundle and checklist position;
- changed files and intended ownership;
- exact build/test/guard commands with results;
- CodeAnalytics snapshot and dependency/cycle evidence;
- selected production path and compatibility flag;
- runtime/context/authority/scope/provider correlation IDs for failures;
- bug records and failing tests;
- next smallest safe action;
- prohibited shortcuts and assumptions that were rejected.

## Handoff rule

The new model begins by verifying the durable state against the repository. It does not redo completed work unless evidence contradicts it. It does not continue across a checkpoint without the recorded unlock decision.

## Safe stopping point

Prefer a passing focused build. When that is impossible, leave one deterministic failing test with a documented expected failure and no unexplained partial production cutover.
