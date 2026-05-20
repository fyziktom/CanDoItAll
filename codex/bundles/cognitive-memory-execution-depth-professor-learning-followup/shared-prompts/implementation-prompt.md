# Shared Implementation Prompt

Implement the current subbundle only. Before editing code, read the updated bundle execution skill and validator skill if SB01 has completed. Treat the raw user request as the contract, not the previous execution report.

For every subbundle:

- identify the shallow implementation that would be tempting;
- add or update tests so that shallow implementation fails;
- implement the smallest correct behavior that passes realistic positive and adversarial negative cases;
- update the execution report with semantic proof, not only command success;
- stop if a prerequisite gate is weak or contradicted by source-code observations.

Do not mark a subbundle complete when the shipped behavior only proves plumbing, record creation, or template output.
