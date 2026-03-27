# Normalized Requirements

- `R001` The selected-node inspector must stop repeating the same primary node identity information in the lead card.
- `R002` Secondary metadata such as Artifact, Kind, Location, and typed fact rows must move into an advanced expandable section instead of occupying the primary inspector space by default.
- `R003` Progress, Priority, and Marker must be presented together on a single deliberate row in the selected-node inspector.
- `R004` The node-action area must use a consistent shared layout with icon affordances, more even button sizing, and Delete rendered last.
- `R005` Supported node types must expose an Edit action that opens the shared canvas composer prefilled with the node's current title, subtitle, notes, and typed metadata values.
- `R006` Saving the edit modal must persist typed changes through an explicit workbench update path without falling back to stringly-typed generic blobs.
- `R007` Existing selection-panel behaviors, including previews, transcript actions, runtime launch buttons, and create-next-to-source flow, must keep working after the inspector refactor.
- `R008` The change must be proven with focused automated coverage and an updated execution report.
