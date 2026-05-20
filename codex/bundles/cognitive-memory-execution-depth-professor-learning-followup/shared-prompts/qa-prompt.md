# Shared QA Prompt

Review the subbundle as a skeptical QA architect. Look for:

- claims in the execution report that are not supported by tests or source behavior;
- tests that assert the shallow/template implementation instead of the intended user-visible behavior;
- happy-path-only proof;
- missing negative/adversarial cases;
- missing recheck of dependencies;
- internal diagnostic text leaking into canonical memory or user-facing recall;
- professor anchors that can be assimilated using their own direct capture record.

Fail the gate when the evidence proves only that the code ran, not that the cognitive behavior is correct.
