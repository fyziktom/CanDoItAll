# Assets and desktop composition

Target: the same production catalog at 1600×1000. Catalog/cards are the primary surface; team tree is secondary. Preserve the compact toolbar/search, team actions, card action placement, empty/loading states and existing responsive behavior. No application mobile redesign. Card results keep the existing bounded vertical scroll; team panel retains its desktop scroll/sticky behavior. Avoid adding multiple competing page scroll owners.

Real children: AgentSelectionCard -> ConversationParticipantCard -> BaseLib Avatar, icons and TooltipTarget. Team navigation uses the actual BaseLib TreeView, not a hand-written list. Register AddCanDoItAllBaseLib and render the required tooltip/overlay host discovered from the current sibling APIs. Use local representative avatars and fallback initials; include long names/tags, managed action identities, private-provider badges, favorite/status/card-detail tooltip states.

Asset sources:
- Tailwind/input.css uses Tailwind 4 and @source ../src plus existing application theme/surface imports.
- Tailwind/package.json build/watch commands generate src/App/CanDoItAll.Web/wwwroot/css/output.css.
- Web Components/App.razor links BaseLib material-symbols.css and output.css, application css/output.css and app.css, module isolated bundle and Web.styles.css.
- ConversationParticipantCard.razor.css remains owned by Conversations.Components.
- AgentCatalogPanel.razor.css moves with the panel; verify generated RCL bundle imports in both actual hosts.

Reuse the locked Tailwind dependency/CLI version and the same input/theme/source scan for both hosts in the primary comparison. Give the sandbox access to that generated stylesheet through a documented static asset link/copy target; do not make it reference/build Web to get CSS. A task-owned Tailwind watch process must run for each measurement session and be recorded. No second hand-written approximated stylesheet or omission of Tailwind from the sandbox.

Use SDK static web assets for RCL CSS, BaseLib fonts/JS and local avatar assets. Audit App.razor's manual module CSS link when the panel moves; remaining module CSS still belongs to the full app. Preserve both isolated style scopes and load order; avoid duplicate imports. The sandbox host may include only the needed application theme stylesheet surface, with equal declarations/hashes for the catalog, not an import of the full production runtime.

Inspect screenshots and DOM for normal, expanded tree, long-card tooltip near every viewport edge, empty/search/loading and action states. Verify tooltips are not clipped by card/tree scroll containers, action buttons remain outside selection buttons, CSS/scoped assets and fonts return 200, and selection styling actually changes. Compare computed styles and production screenshots before claiming representative parity.
