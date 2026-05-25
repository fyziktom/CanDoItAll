# Current state

Before execution, the branch had already removed some provider packages and migration projects, but retained retired provider/source values in the runtime model, UI/runtime branches, snapshot runtime stubs, and startup paths that could fail on legacy catalog JSON. The follow-up pass removes that surface and replaces legacy compatibility with explicit quarantine.
