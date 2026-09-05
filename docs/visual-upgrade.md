# Arena Visual Upgrade

The visual pass retains the current procedural Blaze, Aegis, Zephyr, and Vera characters, with faceted armor and weapons, restrained glow, team rings, and idle breathing. Arena framing and cleaner floor colors keep the finish line and combat readable. Native particle bursts separate anticipation, travel, impact, and recovery without a coroutine-driven effect clock.

Teacher hazards, sudden questions, audio, rewards, finish rules, and the live monitor are preserved. Server rules and protocol files are unchanged. This upgrades the Unity student game, not the separate React teacher-monitor artwork.

## Regression Coverage

`ArenaVisualRegressionTests.cs` adapts the mesh and particle checks from `Detecttech/class-main` commit `4950f5603ec60a705d0f2d9b9c9c7c9a57047860`. Its EditMode cases cover:

- Centered glow UVs and white vertex colors for cones, truncated cones, and tori (3).
- Outward, nondegenerate torus winding consistent with analytic normals (1).
- Tile coordinates under translation, rotation, and nonuniform scale, retaining local surface Y `0.01` without building the grid (3).
- Fireball, shield, wind, life drain, freeze, basic strike, and unknown-tag fallback: effect counts, delayed impact/elimination, bounded single bursts, deterministic seeds, finite lifetimes, and native simulation expiry (7).
- Duration, particle-count, and delay bounds, including negative/zero burst counts emitting zero particles with minimum capacity one (4).
- Native fireball simulation at travel `0.30s`, impact `0.58s`, and expiry `2.0s`, then replay consistency for particle seeds, position, velocity, and lifetime (1).
- Flat versus upright particle-ring orientation and stationary world position (2).
- Isolated idle pause preserves body/accent pose; resume restores breathing and bobbing (1).
- Large hazards retain feedback for every target. After 16 active casts, new effects use a short ring and burst rather than destroying earlier targets' effects (1).

Full procedural character/team-ring construction and `CharacterToken.SetFrozen` integration still need PlayMode coverage. Particle tests clean up their systems with stop-action disabled. The detailed-cast limit reduces effect complexity under load; it does not impose a limit on players or guarantee device frame rate.

## Visual Capture

`ArenaDemoRunner.Run` retains both mock-match paths, character selection, frozen-token presentation, and combat text. Screenshots now target `game-client/Builds/VisualChecks` on Windows and other platforms, rather than `D:/temp`. The six-ability showcase captures travel (`0.20s`), impact (`0.48s`), and recovery (`0.80s`); the existing `vfx-showcase.png` is retained as the impact image. Abilities have different timings, so these are overview snapshots, not synchronized impact frames or pixel-baseline assertions.

## Responsive Arena and Startup

The browser screenshot exposed a framing mistake: fitting the entire stadium into a small area beneath the question controls made the racers tiny. The camera now fits only the playable board and character/nameplate envelope, using an orthographic view. Decorative scenery can crop instead of forcing the camera farther away.

- Wide screens place questions beside the arena; portrait/tablet layouts use a compact top panel. Short landscape layouts retain a side panel with a compact answer grid when necessary.
- Camera and HUD share the same pixel layout, accounting for safe areas, viewport changes, countdowns, spectator state, and HUDs rendered into screenshot textures.
- Questions and answers wrap and scroll at readable font sizes. Dragging answer text does not submit an answer or bypass a locked button.
- Create & open lobby guards duplicate submissions, validates that the bank contains questions, and opens the teacher waiting room directly. A sole class/bank is preselected.
- The lobby displays both codes and a copyable `/play/?classCode=...&joinCode=...` link. Only codes are prefilled; student nickname/PIN authentication remains required.
- Ready reflects server acknowledgement, team requirements, and connection state. Only the teacher starts, with a minimum of two eligible ready players and confirmation if others will be excluded.
- Reconnection requires a fresh lobby snapshot. Leaving the lobby waits for socket closure; completed teacher results remain visible after disconnection.

`ArenaResponsiveFramingTests` covers board containment, responsive control bounds, scrolling, and render-texture HUD placement. `StudentStartupTests` covers URL parsing, existing PIN compatibility, readiness, and departure lifecycle. Screen cases include 1920x1080, 1900x877, 1366x768, 1024x768, 390x844, 844x390, 640x360, and 568x320 with 6-, 11-, and 31-row boards. Whole long boards still have smaller characters than short boards; this is not a follow camera.

## Verification Commands

Run from the repository root with Unity `6000.5.7f1` (`UNITY` is the editor executable path). Use Node 20 for the server, matching the Dockerfile:

```bash
"$UNITY" -batchmode -nographics -quit -projectPath "$PWD/game-client" -logFile "$PWD/unity-visual-compile.log"
"$UNITY" -batchmode -nographics -projectPath "$PWD/game-client" -runTests -testPlatform EditMode -testFilter QuizBattle.Tests.EditMode.ArenaVisualRegressionTests -testResults "$PWD/unity-visual-tests.xml" -logFile "$PWD/unity-visual-tests.log"
"$UNITY" -batchmode -nographics -projectPath "$PWD/game-client" -runTests -testPlatform EditMode -testResults "$PWD/unity-all-tests.xml" -logFile "$PWD/unity-all-tests.log"
"$UNITY" -batchmode -projectPath "$PWD/game-client" -executeMethod PipelineCheck.Run -logFile "$PWD/unity-visual-pipeline.log"
"$UNITY" -batchmode -projectPath "$PWD/game-client" -executeMethod ArenaDemoRunner.Run -logFile "$PWD/unity-visual-demo.log"
npm --prefix server ci
npm --prefix server run build
DB_PATH=:memory: npm --prefix server test
npm --prefix server/web-portal ci
npm --prefix server/web-portal run build
npm --prefix server/web-portal run lint
```

Server build and all 22 server tests passed, including hazard/sudden-question and spectator-flow tests, using isolated in-memory test databases. Runtime C# type checks passed against the Unity managed assemblies already tracked in this repository. Those older player-build DLLs are not a substitute for the configured Editor's build pipeline.

Unity is not installed in the verification environment. Unity EditMode execution, shader compilation, WebGL builds, screenshots, device frame rate, and end-to-end visual behavior remain unverified. Rendering checks need a graphics-capable editor; do not use `-nographics` for screenshots. Existing scene-building code can reach EditMode-unsafe `Destroy` calls, so inspect logs as well as exit codes.

The Dockerfile copies prebuilt WebGL and portal files; pushing these source changes alone does not update an already deployed game. Rebuild the Unity WebGL client and teacher portal before packaging or deploying.
