# Prompt Factory Improvements Bundle

This bundle translates raw feedback into an implementation-grade UX and delivery package for Prompt Factory.

Files:
- `01-structured-inputs.md`: rewritten and normalized product inputs.
- `02-ux-spec-and-user-stories.md`: improved UX specification, user stories, and behavioral expectations.
- `03-flows-and-wireframes.md`: flow diagrams, ASCII layouts, and rationale.
- `04-implementation-plan.md`: execution plan, checklists, validation criteria, and implementation prompts.
- `05-qa-ux-review.md`: critical review of the plan and acceptance gate from a QA and senior UX perspective.

Outcome target:
- reduce scroll debt
- keep the canvas dominant without hiding critical setup
- make prompt-component selection more precise
- make prompt inputs richer and easier to reason about
- prevent destructive or accidental mass changes
- keep the experience advanced but guided, like a serious mass-market productivity tool

Latest refinement:
- Prompt Factory must use true page tabs, not a button strip that only swaps content under the canvas.
- `Canvas` is tab 1 and contains the canvas plus the contextual floating inspector only.
- `Setup`, `Governance`, `Assembly`, and `Review` are separate tabs that replace the canvas surface when selected.
- The inspector should no longer mirror those whole workspaces. Its job is to show details and actions for the selected canvas item.

Current slice:
- The tab strip must read like standard connected tabs, with the active tab visually attached to the active workspace panel.
- The canvas inspector must become a real component editor when a prompt component is selected.
- Component edits must update the session override used by prompt build.
- The inspector must expose a selection preview action that can open a copy-ready modal for the selected item or prompt-step subtree.
- The inspector must live inside the canvas as a floating panel, default docked to the right, draggable, and minimizable so maximized-canvas work stays first-class.
