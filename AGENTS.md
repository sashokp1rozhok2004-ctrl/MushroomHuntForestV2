# Mushroom Hunt

Unity 6000.6.2f1, built-in render pipeline. First-person forest game: WASD/mouse, E collects a targeted mushroom within 3 metres, 10 mushrooms win the game. The playable map is bounded by boundary trees and box colliders; mushrooms are deliberately distributed across the map.

Create or recreate the scene with `BuildMushroomGame.CreateScene` or menu `Mushroom Hunt > Create playable scene`. The source generator is Assets/Editor/BuildMushroomGame.cs. Build Windows with `BuildMushroomGame.BuildWindows`. Scene generation replaces the currently open scene; do not run it over unsaved user changes.

Uses classic Input Manager axes Horizontal/Vertical/Mouse X/Mouse Y. Set Active Input Handling to Input Manager (Old) or Both. MCP for Unity is embedded in Packages/com.coplaydev.unity-mcp. Use STDIO transport to match the registered Codex server.

Python analysis environment: C:/Users/zemss/Documents/Codex/tools/data-env/Scripts/python.exe. User skills game-development, data-science-statistics and systems-analysis are installed globally.
