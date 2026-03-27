# Original User Prompt

Saved at bundle creation time so later implementation agents can trace the exact intent.

```text
You are senior C# Blazor and HTML/JS/CSS Tailwind css architect.
You are preparing bundle with information and plan for implementation agents.
Do not do any implementation!
You will do changes only in folder "components-migration-merge-bundle-1"!

We have two main projects with set of own components:
- Zyphonote (C:\repositories\Zyphonote)
- CanDoItAll (C:\repositories\CanDoItAll)

Both are prepared in dedicated branches for this work set. Bundle must be in CanDoItAll.

Zyphonote started first, then I added CanDoItAll. CanDoItAll should be main base for future. But now just part of the components are used directly from the CanDoItAll.
CanDoItAll has now great system of Canvas related components and drawing. It must be taken as main source of truth for canvas related things, but not updated versions of Razor based components.
Meanwhile we improved a lot components in Zyphonote, but mostly the Razor based components. Those are usually wrappers for some div/span/p, etc.

Now we need to consolidate all components in the CanDoItAll. It does not mean totally all components from zyphonote to move into CanDoItAll. Both Zyphonote and CanDoItAll has also own specific components that should not be shared. It means we must improve projects with components to something like this:


# CanDoItAll.Components.Common
- basic models for UI and helpers. No strong dependencies to heavy libs.
# CanDoItAll.Components.BaseLib
- Using CanDoItAll.Components.Common
- basic components like buttons, forms, layout, modals, notification, tooltip, dialog, etc services, js/css helpers, etc.
- donwloaded google icons and if necessary other external sources to have them as part of the lib.
-  No strong dependencies to heavy libs.
# CanDoItAll.Components.CanvasLib
- using CanDoItAll.Components.Common and can CanDoItAll.Components.BaseLib if needed
- canvas related components and drawing mechanism
-  No strong dependencies to heavy libs.

# CanDoItAll.Mcp.Components
- MCP server for documentation and explanation of Common, BaseLib and CanvasLib libraries
- codex can ask how to use them, etc. Similar as RadzenMCP for Radzen Component Library works.

# CanDoItAll.Components.Sandbox
- Blazor Server Render app that serve as sandbox for tuning the components and as components catalogue
- You must split components to logical groups and do page for each group, where each component will be used in some basic situations (Based on their possibilities/props). For more complex components like some Cards you must fake data.
- This apps is only for CanDoItAll.Components.Common, CanDoItAll.Components.BaseLib CanDoItAll.Components.CanvasLib

# CanDoItAll.Components
 - Using CanDoItAll.Components.Common and CanDoItAll.Components.CanvasLib
 - Specific components for CanDoItAll Web App
 - can have dependencies on some CanDoItAll heavy dependencies because it will be used only in CanDoItAll apps.


In zyphonote, then we will have

# Zyphonote.Components
 - Using CanDoItAll.Components.BaseLib, CanDoItAll.Components.Canvas
 - Specific components for Zyphonote Server and WASM App
 - can have dependencies on some Zyphonote heavy dependencies because it will be used only in Zyphonote apps.

There is lots of components, lots of them still rely on some custom css styles. You must identify all, and complete for each component subbundle folder with instructions and exact references, so implementation agent will transfer/merge all components and move them to proper libraries.
IF there are some custom css we should try to convert them into tailwind css if possible. CanDoItAll must have some common tailwind input.css and then projects can have also own for their own specific styles. This is not solved now. It must be also as subbundle.

If some agent will work in future of those apps and they are both cloned on same drive, sometimes agent is trying to update some components in basic libraries to solve some dev task in upper app. We should add some instructions, that will prevent this can happen and allow changing of basic components libs only from CanDoItAll side. If some other project (for example we work in zyphonote on something) needs to update of some components it will must place it in some specific todo/requests folder in CanDoItAll componets common/baselib/canvaslib as subbundle with request specification. Then CanDoItAll agent can implement it when possible due to another development in CanDoItAll that might be running. We might have also skill for this.

I feel I forgot to mention some details, so during creating the bundle think it through. It is complex task, with lots of UI related steps. UI is not your strong side, so you must be carefull and do precise instructions to do not break working apps. During work use [$frontend-skill](C:\\Users\\lucys\\.codex\\skills\\frontend-skill\\SKILL.md). I think it might be good to phase the bundle to create basic structure of new projects, then create sandbox project for components then start transfering them there one by one and inspecting they work and look properly in sandbox app. Then MCP server for docs and its setup so it can be used in next phases, some special new skills might also help to achieve better results of using those new libraries. After that phase it can be possible to create Components libs for specific projects and then connect them as replacement of original ones. Then complete validation of UI of both apps that all works as before. For all of those phases codex must have subbundles to be able to step by step without loosing or skipping anything.

When agent will be doing any validations of UI. She must take screenshots (large screen) of those pages analyze them, she must ask at least those questions as senior QA/UX/UI inspector:
- can I read all texts properly?
- will I like and understand this UI/Layout as new user?
- is there any too large components, gaps or something that visually disturb the visual aspect of the layout?
- do we use proper components from shared libraries instead of creating some div/spans, etc with some own css classes?
- do we using properly all available space on the page?

add other validation questions with use of your [$frontend-skill](C:\\Users\\lucys\\.codex\\skills\\frontend-skill\\SKILL.md)

If some of the answers are not ok or raising concerns, agent, as senior C# Blazor tailwind css implementation specialist, must tune the layout composition, position of problematic elements/components or using different approach how to display/edit something. This is especially important in first stage during doing compoents demos in sandbox. Already there will be possible to see lots of unsolved details around components.

Bundle must be very complex, well structured, including all architectures, plans, checklists, prompts, tests coverage, validations criterias and system of their prooving, etc.


Start with saving and then structuring my original prompt into "inputs" folder in bundle. Then start whole execution of my instructions until it is fully done.

When you have the pack ready, do detailed validation of whole bundle as senior QA inspector, senior C# Blazor wasm architect and senior Manager. With all must accept that bundle is complete prepared for implementation. if not, it must be improved.
```
