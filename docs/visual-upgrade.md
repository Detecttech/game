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

## Verification Commands

Run from the repository root with Unity `6000.5.7f1` (`UNITY` is the editor executable path). Use Node 20 for the server, matching the Dockerfile:

```bash
"$UNITY" -batchmode -nographics -quit -projectPath "$PWD/game-client" -logFile "$PWD/unity-visual-compile.log"
"$UNITY" -batchmode -nographics -projectPath "$PWD/game-client" -runTests -testPlatform EditMode -testFilter QuizBattle.Tests.EditMode.ArenaVisualRegressionTests -testResults "$PWD/unity-visual-tests.xml" -logFile "$PWD/unity-visual-tests.log"
"$UNITY" -batchmode -projectPath "$PWD/game-client" -executeMethod PipelineCheck.Run -logFile "$PWD/unity-visual-pipeline.log"
"$UNITY" -batchmode -projectPath "$PWD/game-client" -executeMethod ArenaDemoRunner.Run -logFile "$PWD/unity-visual-demo.log"
npm --prefix server ci
npm --prefix server run build
DB_PATH=:memory: npm --prefix server test
```

Server build and all 22 server tests passed, including hazard/sudden-question and spectator-flow tests, using isolated in-memory test databases. Runtime C# type checks passed against the Unity managed assemblies already tracked in this repository. Those older player-build DLLs are not a substitute for the configured Editor's build pipeline.

Unity is not installed in the verification environment. Unity EditMode execution, shader compilation, WebGL builds, screenshots, device frame rate, and end-to-end visual behavior remain unverified. Rendering checks need a graphics-capable editor; do not use `-nographics` for screenshots. Existing scene-building code can reach EditMode-unsafe `Destroy` calls, so inspect logs as well as exit codes.

The Dockerfile copies prebuilt WebGL files; pushing these source changes alone does not update an already deployed game. Rebuild the Unity WebGL client before packaging or deploying.
