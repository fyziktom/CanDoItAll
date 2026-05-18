# Normalized Requirements

| Id | Requirement | Priority | Acceptance Signal |
| --- | --- | --- | --- |
| R001 | All shell menu item tooltips that still exist must wait for the shared few-second delay before appearing. | Must | Playwright hover check shows no tooltip before the delay and a tooltip after the delay; component markup carries the delay parameter. |
| R002 | `More`, `Opened`, and `Switch Database` trigger tooltips must remain absent. | Must | Browser DOM checks find no trigger tooltip for those popup menu controls. |
| R003 | The shell navigation system must allow modules to contribute additional main menu items tied to a parent route. | Must | A module contribution contract exists outside the Web shell and `ShellNavigation.GetItems` merges contributions after their parent item. |
| R004 | AgentFramework must contribute `Workflows` at `/agents/workflows` immediately after `Agents`. | Must | Desktop browser menu order is `CRM / HR`, `Agents`, `Workflows`, `Resources` when space allows. |
| R005 | Subpage contributions must record that they are subitems while rendering as normal items for this bundle. | Must | Code contains explicit metadata and a short design note explaining future subitem rendering. |
