# Exit criteria

Bundle12 exits green only when:

1. the current repo passes phase10 gate,
2. the current repo passes phase11 gate,
3. the current repo passes phase12 gate,
4. required tests are present for every subbundle,
5. EF migrations/snapshots include the new durable runtime records,
6. runtime work no longer depends on manual admin invocation or inline execution hacks.
