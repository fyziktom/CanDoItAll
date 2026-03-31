# Original Request

```text
Use [$candoitall-bundle-workflow](C:\Users\lucys\.codex\skills\candoitall-bundle-workflow\SKILL.md) skill to prepare and execute bundle that will do the refactoring of the styles across whole solution.

Main Goal:
- unifiy styles across the solution
- maximise reusability of styles
- use tailwind css for absolute most of styling where possible/reasonable
- reduce duplicities, and reduce code lenght without loosing any functionalities

DO NOT CHANGE:
- CanvasLib and things related to drawing to or around canvas. We will do it in separated refactoring wave.

Mandatory steps:
0) you must store this original input and at the end of your whole work you must truly and critially anwer questions: "Did I do everything that was requested in original prompt? Is it truly the best work I can do? Did I covered all and truly validated all? Is codebase now better maintanable and easier to read?" if some of them are not ok or raising concerns you must repair/improve it.
1) identify all elements like div/button/span/...etc with tailwind css classes. make them as excel so you can group them based on the number of occasions. If some element is very similar, but not the same, you must unified styles (example: some button has rounded corners with 3 and some to 4, better to use same 3 for both).
2) Based on that list create well structured system around input.css of tailwind. tailwind allows imports to input.css so you must create own css file for every element (for example button.css for all prepared styles for different types of buttons across the solution) and structured them into folders (for example Controls/buttons.css, then Forms/.. etc). then connect them as import in input.css.
3) then it must be implemented and truly validated with playwright mcp and screenshots, that it works and looks same. If something is not working/looking properly it must be fixed and revalidated until the basic styles library is working perfectly.
4) Then check every component in BaseLib if it already uses properly tailwind classes. If it is possible/reasonable use the new tailwind css styles to unify the styles. You must validate the changes with playwright mcp and screenshots.If something is not working/looking properly it must be fixed and revalidated until the basic styles library is working perfectly.
5) When those basic libraries are working well you must identify other elements like div/button/span/...etc where we use totally custom css. Analyze for each if it is safe to use our prepared libraries without affecting functionalities. If it is safe, replace it with own components or at least shared tailwind class. For those that must use some very specific styles if it is safe use at least tailwind css instead of pure css. In this step you must create detailed information for step 6)
6) All inputs from 5) must be implemented and truly validated with playwright mcp and screenshots, that it works and looks same. If something is not working/looking properly it must be fixed and revalidated until the basic styles library is working perfectly. Assure that all texts are wrapped properly, components are not overlaying each other, etc. All important rules that you also have in [$frontend-skill](C:\Users\lucys\.codex\skills\frontend-skill\SKILL.md).
7) After all those changes try to analyze again if you missed some. If yes, you must find proper and safe way to switch them to our library. Try to trully answer questions from 0). You must not fake answers. You must answer based on facts.

IMPORTANT RULES:
- If you see that we are missing some usefull component in our BaseLib and you need it to solve some replacement of our own css style with some div/button/span/...etc. then create component in our BaseLib. Main point is to have complex components library that we will share across all projects not just in this solution. It must be perfect!
- It is huge refactoring. You must do it in logical phases. Split phases informations-plans into own subfolders with another subbundles for each specific step/component/etc. You must be sure you are continue correctly.
- Measure for your progress. It means calc how many div/button/span/...etc. you already replaced. How many of them you unified from slight differencies. How much code does it saved (you can easilly compare length of the file before after. Based on those data you can validate if you are going in good direction.
```
