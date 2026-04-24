# Original Request

Use [$candoitall-bundle-workflow](C:\\Users\\dell\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) to prepare and execute and validate bundle for refactoring of WebGlLib. Especially the [01-webgl-workbench.js](src/CanDoItAll.Components.WebGlLib/wwwroot/js/runtime/workbench/01-webgl-workbench.js) . It must be splitted to logical smaller classes and helpers. Analyze how CanvasLib is done. It is still not best example because some files are still very large. We are also still missing tools for rightclick menu that must be drawen in webgl too, then tools for connection/reconnection of nodes in 3D.
WebGL window must have some top toolbar similar as we have in canvas (but it must be really drawen in webgl). We need tools for delete, selection, etc.
we need in settings options like display just miniature nodes info, or display detailed info for nodes, or hide info at all. Try to identify another usefull options/settings.
You must do testing with playwright mcp and screenshots to see real result in webgl.
