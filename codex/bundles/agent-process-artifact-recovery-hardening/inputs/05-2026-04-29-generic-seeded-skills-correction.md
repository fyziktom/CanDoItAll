# 2026-04-29 Generic Seeded Skills Correction

## Raw Feedback

The previous direction is better, but seeded skills must be generic too. The AI agent could be asked to write any type of application, not only a calculator app.

## Implication

- Do not leave sample-application instructions in globally seeded skills.
- Do not keep one-off task workflows as built-in seed skills unless they are generalized into reusable task-capability guidance.
- Technology skills may stay specialized when explicitly scoped, but their examples and rules must not encode a single sample app as the default behavior.
- Agents should receive task-specific details from the process input, per-run artifacts, generated bundles, or explicitly selected task skills, not from universal seeded defaults.
