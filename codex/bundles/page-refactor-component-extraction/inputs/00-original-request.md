# Original Request

Use [$candoitall-bundle-workflow](C:\\Users\\lucys\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) to do refactoring of each page in our app. Lots of them are very long and it will be better to split them into own smaller components.
Some pages also contains lots of logic that can be isolated to helpes. For example on project structure page there are lots of helpers for nodes. You must isolate them into some ProjectStructureNodeHelpers.
In bundle first identify all those necesary refactorings and how to isolate functions and components. It will require some connection of logic, events, functions, etc. it is best to create detailed checklist with references in xlsx.
Then you can create subbundle for each change and do them atomically. first isolations of helpers to reduce code on pages. then components isolations.
you must preserve all functionality and test it that all works as before.
