# 05 — Modular assistant pipeline + settings

Goal: make assistant features plug-in like modules, each toggleable and optionally providing widget controls.

Tasks (C#):
1) Introduce an interface in `MusicTheory.Core/Generation/Realtime`:
   - `IHarmonicAssistantModule`
     - `string Id`
     - `bool IsEnabled`
     - `ModuleSettings Settings`
     - `ModuleContribution Evaluate(ModuleContext ctx)`
2) Extend planning engine to accept module contributions:
   - scoring modifiers / candidate boosts
   - annotations for UI (tooltips)
3) Add built-in modules:
   - `NoveltyGuardModule` (anti-loop)
   - `PatternCorpusModule` (stub for now)
4) Expose module list to UI snapshot so canvas can render a “Modules” widget.

Acceptance:
- Engine uses module contributions and can be extended without touching JS rendering logic.
- Modules can be toggled from canvas.

Self-check:
- Unit test verifies enabling/disabling novelty module changes ranking.
