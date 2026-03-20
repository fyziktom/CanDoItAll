Add the PHP UI and repository graph canvas.

Required work:
1. Create reusable repository graph JS/CSS/PHP helpers.
2. Integrate the graph into:
- `account-score-detail.php`
- score management surface in dashboard/my-scores
- `account-playlists.php`
- `account-events.php`
- `account-learning-builder.php`
3. Reuse the current workbench/canvas visual language.
4. Add commit/branch/compare/fork/MR entry points where permissions allow.

Rules:
- the graph should feel like a real product surface, not debug UI
- provide an inspector/text fallback
- show ahead/behind/diverged state clearly

Update checklists after completion.
