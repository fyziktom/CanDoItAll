# Tetris Mini Game Browser Proof

- Scenario key: tetris-mini-game
- Process run id: 54923282-a686-409f-a8a1-59eeb1f5d932
- Runtime URL: http://127.0.0.1:5201
- App root: C:\repositories\CanDoItAll\.artifacts\sb08-multidomain-e2e\20260602-013426\apps\tetris-mini-game
- Desktop screenshot: screenshots/tetris-mini-game-desktop.png
- Mobile screenshot: screenshots/tetris-mini-game-mobile.png
- Console error count: 0
- Browser assertions: passed

## Checklist
- [x] Page loads without console errors except harmless favicon/static asset warnings.
- [x] Game board is visible.
- [x] Keyboard interaction changes board or active piece state.
- [x] Score or status is visible.
- [x] Reload preserves best score when achievable in the implementation.

## Captured State
```json
{
  "scenario": "tetris-mini-game",
  "url": "http://127.0.0.1:5201",
  "finalState": {
    "title": "Tetris Mini Game",
    "url": "http://127.0.0.1:5201/",
    "bodyText": "Tetris Mini Game\n\nPlay a compact falling-block board with keyboard and button controls. Best score is stored locally.\n\nClient-only\nBlazor WASM PWA\nScore\n0\nBest\n53\nStatus\nReady\nLeft\nRight\nRotate\nDrop\nRestart\n\nKeyboard: Arrow keys or W/A/S/D.",
    "ready": "tetris-mini-game",
    "appState": {
      "score": 0,
      "best": 53,
      "active": {
        "x": 4,
        "y": 0,
        "rotation": 0
      },
      "lockedCount": 0,
      "boardCells": 160
    }
  },
  "snapshot": {
    "heading": "Tetris Mini Game",
    "text": "Tetris Mini Game\n\nPlay a compact falling-block board with keyboard and button controls. Best score is stored locally.\n\nClient-only\nBlazor WASM PWA\nScore\n0\nBest\n53\nStatus\nReady\nLeft\nRight\nRotate\nDrop\nRestart\n\nKeyboard: Arrow keys or W/A/S/D.",
    "activeElement": "H1",
    "storageKeys": [
      "sb08-tetris-mini-game:best"
    ]
  },
  "assertions": "passed",
  "screenshots": [
    "C:\\repositories\\CanDoItAll\\codex\\bundles\\process-workflow-agent-hardening-v1\\proof\\SB08\\scenarios\\tetris-mini-game\\screenshots\\tetris-mini-game-desktop.png",
    "C:\\repositories\\CanDoItAll\\codex\\bundles\\process-workflow-agent-hardening-v1\\proof\\SB08\\scenarios\\tetris-mini-game\\screenshots\\tetris-mini-game-mobile.png"
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
    "timestamp": "2026-06-02T05:36:16.834Z"
  },
  {
    "kind": "console",
    "level": "info",
    "text": "Debugging hotkey: Shift+Alt+D (when application has focus)",
    "timestamp": "2026-06-02T05:36:17.513Z"
  },
  {
    "kind": "console",
    "level": "info",
    "text": "Debugging hotkey: Shift+Alt+D (when application has focus)",
    "timestamp": "2026-06-02T05:36:20.600Z"
  }
]
```
