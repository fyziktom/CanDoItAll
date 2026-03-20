# Data model for graph nodes/edges and layout coordinates

This upgrade switches from “C# precomputed pixel positions” to a **semantic snapshot** that JS can lay out responsively.

## 1) Why change the snapshot model
Current model:
- C# computes absolute x/y and JS just draws them.
Limitations:
- hard to auto-zoom, adapt to resize, or enforce single-line flow
- hard to introduce mood-axis mapping and lane assignment in a reusable way
- hard to make interactive canvas controls (needs renderer state)

## 2) Proposed snapshot v2 (semantic)
Create a new DTO (either replace existing or add v2).

### 2.1 Node
Fields (recommended):
- `id: string`
- `label: string`
- `kind: 'history' | 'current' | 'future'`
- `isCurrent: boolean`
- `xIndex: number` (integer; timeline index)
- `pathId: string | null` (null for history/current; set for future path)
- `stepIndex: number | null` (1..H for future)
- `probability: number` (0..1)
- `worldY: number` (0..1 world mood position)
- `color: string` (hex color computed in C#)
- `meta?: object` (optional: scale hint, chord symbol, root pc)

### 2.2 Edge
Fields:
- `fromId: string`
- `toId: string`
- `kind: 'history' | 'prediction'`
- `probability: number`
- `color?: string` (optional; JS can compute gradient from nodes)

### 2.3 Snapshot
Fields:
- `nodes: Node[]`
- `edges: Edge[]`
- `caption?: string`
- `layout?: { historySteps:number, horizonSteps:number }` (optional)
- `renderHints?: { currentWorldY:number }` (optional)

## 3) Layout computed in JS
JS responsibilities:
- compute viewY from worldY and currentWorldY
- lane assignment for future paths
- compute zoom-to-fit and spacing
- draw background with current chord color band
- draw in-canvas controls, handle pointer events
- optional: hover tooltips

## 4) Testing strategy
Unit-test the C# snapshot builder:
- xIndex monotonicity
- worldY mapping stability (given chord symbols)
- consistent assignment of pathIds and step indices

Canvas pixel rendering can be validated via:
- snapshot -> layout computed positions -> deterministic numeric asserts (not pixel-perfect)
