# Original Request

Use `candoitall-bundle-workflow` to solve this with new bundle:

Main goal:
Remove unused modules "Validation", "Activity" and "Automation"

Notes:
- we have our calendar-scheduler that with workflows and processes covers automation tasks.
- validation and activity I never use. It was wrong way. We can manage those things in project structure, processes and workflows better way.
- you must remove also tests related to those parts.
- be surgial precise when you will remove some connections of those old modules. They are in multiple places like in project structure right click menu, etc. you must first map all their references (better as xlsx) and then go one by one to remove them.
- you must assure that app is working again without trouble.
- rebuild our running 5032 instance for testing. stop it before you start doing changes.
