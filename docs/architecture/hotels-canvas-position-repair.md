# Hotel canvas position integration

Hotel preparation plans use deliberate phase columns with vertical task cards.
Their saved API positions were correct, but CanvasLib forced horizontal parent-child
clearance even when their vertical separation already provided adequate space. This
shifted the displayed columns and crowded otherwise separate branches.

The repair belongs to CanvasLib's shared layout geometry. Components commit
`137d0db5c462c2fe93177744152e140c3107caa7` keeps sufficient vertical separation
and retains horizontal clearance for genuinely close parent-child cards. It adds no
public parameter, application override, dependency edge or API change.

Four of seven geometry regressions failed before the fix; all seven pass after it.
They cover both vertical directions, both horizontal directions, actual overlap,
manual positions, phase columns and reversed node order. The regression command is
part of Components `assets:verify`; all generated-asset, readonly-drag and composer
checks pass. CanvasLib Release builds with zero warnings/errors. The hotel image must
include this exact Components commit, followed by native canvas review at the same
saved positions. Main portability enforcement remains unchanged and passes.
