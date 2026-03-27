# QA Prompt

Review `canvas-feedback-bundle-3` against the bundle requirements.

Check:

- launch buttons appear only for nodes that have enough typed metadata to run predictably
- normal and elevated launches are both wired through the same runtime-launch service
- dotnet watch, other dotnet runtime nodes, and supported python/script nodes resolve the correct working directory and command
- failures are explicit and do not silently fall back to some other command
- existing inspector actions and local attachment open behavior still work
