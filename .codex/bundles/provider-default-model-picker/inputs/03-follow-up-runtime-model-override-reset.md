# Follow-Up Runtime Model Override Reset

Use `candoitall-bundle-workflow` to solve this:

> we still have trouble with changing the model in the agent details dialog in runtime tab. I clicked to override model and I saved it and it wrote it was saved, but then the dialog get back to unselected (without override). It is like something override my change. Analyze it. It might mean we have some trouble with canonicity of the agents and their settings.

## Preserved Signals

- The runtime tab reports a successful save but reloads with the override unchecked.
- The follow-up is about canonical ownership of agent settings, not only a visual checkbox state.
- The user expects an explicit model override to persist through the save/reload flow.
