# Forbidden patterns

- invoking plugin materializer code before a persisted in-progress claim exists
- rerunning successful materialization on repeated reads
