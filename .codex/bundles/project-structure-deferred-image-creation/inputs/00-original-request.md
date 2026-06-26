# Original Request

```text
great. now it works fine.
First thing to check is if form trully transfer all necessary informations to provider. I asked to do calc app image and it returned just blue lighted image. So it looks like it did not get the prompt instructions even I wrote them in prompt textarea.

next step is that we must add the node immediatelly after save even in bacground is still waiting for the image. when image will arrive it will feed into node the data. Meanwhile it can show some dummy image with "Waiting for Image creation by AI..."

This migh be generic function in project structure. It might happen more offten that we create node, but it must wait for some data loading or storing, etc.
Use [$candoitall-bundle-workflow](C:\Users\lucys\.codex\skills\candoitall-bundle-workflow\SKILL.md) to solve this with new bundle.
Do proper analysis of architecture and consider all possible userstories where we can use something like this.
We still must be careful to do not break canonicity or performance. project structure is already complicated part.
Based on detail analysis you must proper architecture changes and assure we will not break something else with it. So part of architecture must be also validation of related parts and assure that we will not break existing project structure or we will not slow it down.
```

## Context From Earlier Same Flow

- The project structure generated-image provider dropdown now works in the right-click node context flow.
- The selected local provider is expected to be `Local ComfyUI Flux`.
- Local validation must use the real project structure create path, not only direct service calls.
