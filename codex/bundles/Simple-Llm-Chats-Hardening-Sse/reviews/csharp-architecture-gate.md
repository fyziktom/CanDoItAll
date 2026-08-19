# C# architecture gate

Run at CP1, CP2 and FINAL.

## Ownership

- Product truth remains in Modules.LlmChats.
- EF/runtime adapter behavior remains in Persistence.
- Provider-neutral contracts remain generic.
- Concrete protocol parsing remains in provider drivers.
- Web owns transport only.

## Required proof

- before/after project graph and cycles;
- exact new references;
- no forbidden dependency tokens;
- no service-location in product behavior;
- no new production partial expansion of large services;
- direct tests for reducer, coalescer, retry policy and profile scope;
- failure-injection tests target new command owners;
- old callback/independent-transaction paths removed or compatibility-only with removal evidence.

## Blocking anti-patterns

- one “manager” owns commands, queries, dispatcher, stream and cleanup;
- repository creates hidden context during a caller transaction;
- Web event service becomes canonical journal;
- provider driver references Simple Chat operation;
- an interface exists but only the old monolith implements all behavior;
- a new partial file is the final architecture;
- transaction/fence ownership is implied by method names rather than verified.
