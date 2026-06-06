# Nested Model Alias Cutline

The projection boundary still references dispatcher nested models through aliases.

For this bundle, do not attempt a full model migration. Instead:

1. Inventory all aliases from `ProcessArtifactProjectionFacets.cs` and `ProcessArtifactProjectionContext.cs`.
2. Introduce read-only view/adapters only where they reduce direct dispatcher coupling without large behavior movement.
3. Do not move EF entities or process runtime records to `Processes.Contracts`.
4. Do not expose public model types for drivers.
5. Leave deeper model extraction as a future bundle after facet implementations are split and tested.

A future bundle may target projection model snapshots, but only after this bundle completes.
