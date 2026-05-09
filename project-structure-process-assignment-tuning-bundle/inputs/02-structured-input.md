# Structured Input

| Id | Request | Acceptance Signal |
|---|---|---|
| IN-001 | Fullscreen modal content uses the available width. | Browser screenshot and DOM measurements show the assignment shell fills the fullscreen dialog without right-side dead space or horizontal clipping. |
| IN-002 | First rail item is `All`. | Component test and screenshot show `All` as the first rail entry; selecting it renders summary cards for all roles. |
| IN-003 | Selecting a specific role renders role-specific assignment. | Component test and screenshot show selected candidate first, remaining candidates by score, and a plus card at the end. |
| IN-004 | Plus card opens all-agent picker. | Component test proves callback; browser screenshot shows reused `AgentSwitchDialog` with search, tags, and favorites. |
| IN-005 | Agent cards expose model/tools/skills tooltips and details. | Component test asserts badges; browser proof opens tooltip/details states; details dialog is readonly. |
