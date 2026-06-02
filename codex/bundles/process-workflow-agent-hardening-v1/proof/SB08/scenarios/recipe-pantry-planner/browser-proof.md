# Recipe Pantry Planner Browser Proof

- Scenario key: recipe-pantry-planner
- Process run id: d323bea1-7347-42d1-943b-8aef123c9722
- Runtime URL: http://127.0.0.1:5205
- App root: C:\repositories\CanDoItAll\.artifacts\sb08-multidomain-e2e\20260602-013426\apps\recipe-pantry-planner
- Desktop screenshot: screenshots/recipe-pantry-planner-desktop.png
- Mobile screenshot: screenshots/recipe-pantry-planner-mobile.png
- Console error count: 0
- Browser assertions: passed

## Checklist
- [x] Add pantry ingredient.
- [x] Recipe matches update.
- [x] Add missing ingredient to shopping list.
- [x] Toggle shopping item done.
- [x] Reload preserves pantry/shopping list.

## Captured State
```json
{
  "scenario": "recipe-pantry-planner",
  "url": "http://127.0.0.1:5205",
  "finalState": {
    "title": "Recipe Pantry Planner",
    "url": "http://127.0.0.1:5205/",
    "bodyText": "Recipe Pantry Planner\n\nMaintain pantry ingredients, rank built-in recipes, and build a shopping list from missing ingredients.\n\nBuilt-in recipes\nLocal pantry\nIngredient\nAdd ingredient\nPantry\npasta\ntomato\nShopping list\ngarlic\nTomato Pasta\n\n2/3 ingredients available\n\nMissing: garlic\n\nAdd missing\nVeggie Omelet\n\n0/3 ingredients available\n\nMissing: eggs, spinach, cheese\n\nAdd missing\nBean Tacos\n\n0/4 ingredients available\n\nMissing: tortilla, beans, cheese, salsa\n\nAdd missing\nApple Oats\n\n0/3 ingredients available\n\nMissing: oats, apple, milk\n\nAdd missing",
    "ready": "recipe-pantry-planner",
    "appState": {
      "pantry": [
        "pasta",
        "tomato"
      ],
      "shopping": [
        "garlic"
      ],
      "recipeText": "\n                Tomato Pasta\n                2/3 ingredients available\n                Missing: garlic\n                Add missing\n            \n                Veggie Omelet\n                0/3 ingredients available\n                Missing: eggs, spinach, cheese\n                Add missing\n            \n                Bean Tacos\n                0/4 ingredients available\n                Missing: tortilla, beans, cheese, salsa\n                Add missing\n            \n                Apple Oats\n                0/3 ingredients available\n                Missing: oats, apple, milk\n                Add missing\n            "
    }
  },
  "snapshot": {
    "heading": "Recipe Pantry Planner",
    "text": "Recipe Pantry Planner\n\nMaintain pantry ingredients, rank built-in recipes, and build a shopping list from missing ingredients.\n\nBuilt-in recipes\nLocal pantry\nIngredient\nAdd ingredient\nPantry\npasta\ntomato\nShopping list\ngarlic\nTomato Pasta\n\n2/3 ingredients available\n\nMissing: garlic\n\nAdd missing\nVeggie Omelet\n\n0/3 ingredients available\n\nMissing: eggs, spinach, cheese\n\nAdd missing\nBean Tacos\n\n0/4 ingredients available\n\nMissing: tortilla, beans, cheese, salsa\n\nAdd missing\nApple Oats\n\n0/3 ingredients available\n\nMissing: oats, apple, milk\n\nAdd missing",
    "activeElement": "H1",
    "storageKeys": [
      "sb08-recipe-pantry-planner:shopping",
      "sb08-recipe-pantry-planner:pantry"
    ]
  },
  "assertions": "passed",
  "screenshots": [
    "C:\\repositories\\CanDoItAll\\codex\\bundles\\process-workflow-agent-hardening-v1\\proof\\SB08\\scenarios\\recipe-pantry-planner\\screenshots\\recipe-pantry-planner-desktop.png",
    "C:\\repositories\\CanDoItAll\\codex\\bundles\\process-workflow-agent-hardening-v1\\proof\\SB08\\scenarios\\recipe-pantry-planner\\screenshots\\recipe-pantry-planner-mobile.png"
  ]
}
```

## Console
```json
[
  {
    "kind": "console",
    "level": "info",
    "text": "Debugging hotkey: Shift+Alt+D (when application has focus)",
    "timestamp": "2026-06-02T05:38:13.115Z"
  },
  {
    "kind": "console",
    "level": "info",
    "text": "Debugging hotkey: Shift+Alt+D (when application has focus)",
    "timestamp": "2026-06-02T05:38:13.900Z"
  },
  {
    "kind": "console",
    "level": "info",
    "text": "Debugging hotkey: Shift+Alt+D (when application has focus)",
    "timestamp": "2026-06-02T05:38:16.764Z"
  }
]
```
