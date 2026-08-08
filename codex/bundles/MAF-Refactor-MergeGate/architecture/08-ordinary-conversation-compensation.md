# Ordinary conversation durable compensation

`ActiveTurn` must contain enough bounded state to restore the exact pre-turn conversation after a crash:

- pending user entry identity;
- pre-turn provider snapshot when adoption occurred;
- pre-turn acceleration envelope when invalidated;
- admitted revision/timestamp.

Provider failure, cancellation, explicit abandonment, and crash recovery use the same compensation
constructor. Rename is rejected while a turn is active in this release. Turn admission reserves both
transcript slots before the provider call.
