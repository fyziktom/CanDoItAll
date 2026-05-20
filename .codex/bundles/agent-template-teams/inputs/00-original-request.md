# Original Request

Use `C:\Users\dell\.codex\skills\candoitall-bundle-workflow\SKILL.md` to solve this:

Main goal:
Having agents templates in Templates folder similar as we have workflows or processes.

Notes from architect:
Each agent must have own folder with instructions, specific skills, some json with specific settings (like provider, etc that are more related to our data structure). The generic informations about agent must stay in easy editable form, so we can tune those templates in simple files.
You must split our default agents to the teams. It will be good to have some folder structure for team definitions and in folder of team there will be team info/settings and then folders with members.
When you will be doing it you must do revision of our each agent instructions and improve them.

You must avoid to have hardcoded those default agents in source code. If it is somewhere you must remove it when it is proven that our new system works.
You must do validation with playwright mcp and assure that agents work as before.
