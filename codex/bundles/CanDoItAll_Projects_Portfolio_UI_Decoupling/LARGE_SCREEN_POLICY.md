# Large-desktop-only policy for this slice

The user explicitly prioritizes isolation and fast UI development over responsive tuning. This scoped policy supersedes historical small/medium viewport examples in archived bundles; do not edit the sealed shared v3 files to accomplish that.

Use **1920×1080 CSS pixels at 100% zoom** for new production and sandbox journeys and captured review images. Use **1600×1000** only when a second genuinely large desktop window adds evidence of a functional layout regression. There is no required viewport matrix.

Do not create, repeatedly execute or tune 390/768/900-width layouts, mobile menus, breakpoints, touch-specific controls or medium-screen visual refinements for this task. Retain existing CSS media rules unless the extraction itself requires a minimal correction. Do not delete established independent BaseLib responsive tests, suppress failures in a required owning suite, or globally disable assertions to save time. If the ordinary required suite runs them automatically, report its real result; do not turn their execution into an extra tuning project.

At the supported viewport still verify functional visibility, clipping, scroll access to the full form/footer, keyboard focus, Escape/backdrop ownership, correct stacking, no accidental horizontal overflow hiding actions, real assets and hydration. These are correctness checks, not optional polish. Use existing components and desktop structure; do not redesign the visual theme.

This bundle's performance deliverable is the isolated sandbox and actual edit-to-visible proof with `dotnet watch`, not a mobile redesign and not a promise that the complete Web graph becomes small.
