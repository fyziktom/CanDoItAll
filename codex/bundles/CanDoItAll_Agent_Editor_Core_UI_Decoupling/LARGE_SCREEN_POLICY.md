# Large-desktop-only scope

Primary viewport: **1920 × 1080 CSS pixels at 100% zoom**. Optional second viewport: **1600 × 1000**,
only to diagnose a concrete supported large-desktop functional problem. Do not run a small/medium,
mobile/tablet or arbitrary responsive screenshot matrix. Do not optimize for those breakpoints in
this assignment; that work is deliberately deferred until rendering is well isolated.

Preserve shipped CSS behavior while moving it. Do not delete existing library responsiveness tests
or weaken existing functionality to satisfy this scope. Testing/review should focus on usable
large-screen forms, complete menu visibility, scroll ownership, footer access, focus/keyboard,
nested dialogs and correct assets. A 2-pixel viewer containing the expected DOM text is not usable.

The immutable shared v3 references older viewport practices. This explicit current product request
narrows NEW UI evidence for this bundle; it does not authorize falsifying old evidence or relabeling
unsupported-view tests as Passed. Avoid visual redesign beyond concrete defects introduced or
exposed by this scoped extraction. Source/build boundary and dotnet watch usability remain the goal.
