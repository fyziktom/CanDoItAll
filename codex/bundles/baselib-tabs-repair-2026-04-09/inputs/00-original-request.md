# Original Request

Use `candoitall-bundle-workflow` to prepare and execute and validate bundle for repair of tabs in our `CanDoItAll.Components.BaseLib`.

Actual tabs kind of work, but their styles not working correctly. We also keep kind of dependence on two different styles groups `zy` and `cad`. We must unify it. Customization of look must be via parameters that can help with enums to configure additional look or additional Tailwind classes in `Class` property.

I cloned Radzen library `C:\repositories\radzen-blazor`. They have good working and looking tabs. But they are using own styles. We must use Tailwind CSS only.

Add page dedicated to Tabs with different examples of tabs in components sandbox. Add also examples that shows what happen in not optimal paths, like too long title, missing title, tabs wrapping on small column size, etc. Based on those examples you will see new issues that must be solved, so always check how those examples looks and improve subbundles to execute changes and then revalidate.

Radzen tabs are without border around the tab button. I would prefer also light border around the tabs buttons. But it can be optional parameter.

Analyze all and then create bundle with subbundles to repair it and test it with Playwright MCP and screenshots. You must validate that tabs are looking.

Thread artifact note:

- Screenshot A shows Radzen server and client render mode examples with underlined active tabs and clear panel boundaries.
- Screenshot B shows prevent-change and reorder examples, including a preferred lightweight tab-border appearance option.
