using System.Collections.Generic;
using System.IO;
using QuizBattle.Arena;
using QuizBattle.Arena.Vfx;
using QuizBattle.Arena.Visuals;
using QuizBattle.Characters;
using QuizBattle.GameState;
using QuizBattle.GameState.MockEngine;
using QuizBattle.UI.HUD;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CaptureCurrentArena
{
    public static void Capture()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var defs = LoadCharacterDefs();
        var manager = GameManager.Bootstrap(defs);
        var result = manager.RunAutoPlayMatch(seed: 1, maxRounds: 4);

        var grid = Object.FindFirstObjectByType<GridController>();
        if (grid != null)
        {
            foreach (var fct in Object.FindObjectsByType<FloatingCombatText>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(fct.gameObject);
            }
            FloatingCombatText.Spawn(grid.TileToWorldPos(3, 1) + Vector3.up * 1.5f, "-15 HP", QuizBattlePalette.RoofTilesRed, 1.15f);

            var tokenMap = new Dictionary<string, CharacterToken>();
            foreach (var t in Object.FindObjectsByType<CharacterToken>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                tokenMap[t.name] = t;

            if (tokenMap.TryGetValue("Blaze", out var b)) { b.gameObject.SetActive(true); b.MoveTo(grid.TileToWorldPos(3, 1)); b.SetHp(30, 45); }
            if (tokenMap.TryGetValue("Vera", out var v)) { v.gameObject.SetActive(true); v.MoveTo(grid.TileToWorldPos(4, 3)); v.SetHp(50, 50); v.SetStreak(2); }
            if (tokenMap.TryGetValue("Zephyr", out var z)) { z.gameObject.SetActive(true); z.MoveTo(grid.TileToWorldPos(6, 1)); z.SetHp(50, 50); }
            if (tokenMap.TryGetValue("Aegis", out var a)) { a.gameObject.SetActive(true); a.MoveTo(grid.TileToWorldPos(1, 2)); a.SetHp(40, 55); }
        }

        var hud = Object.FindFirstObjectByType<HudController>();
        if (hud != null)
        {
            hud.ShowQuestion(2, "Demo question #5", new List<string> { "Option A", "Option B", "Option C", "Option D" });
        }

        var camera = Camera.main;
        if (camera == null)
        {
            Debug.LogError("[CaptureCurrentArena] No Camera.main found");
            EditorApplication.Exit(1);
            return;
        }

        foreach (var token in Object.FindObjectsByType<CharacterToken>(FindObjectsSortMode.None))
        {
            token.CompleteMovement();
        }

        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1.5f;
            canvas.sortingOrder = 1000;
        }
        Canvas.ForceUpdateCanvases();

        string artifactDir = @"C:\Users\engmh\.gemini\antigravity-ide\brain\1c945747-d0fc-4277-b165-3e3b6cc283d2";
        if (!Directory.Exists(artifactDir)) Directory.CreateDirectory(artifactDir);

        var framer = camera.GetComponent<ArenaCameraAutoFramer>();

        // Render multiple aspect ratios: Standard 16:9, iPhone/Galaxy 19.5:9, Ultra-wide 20:9, iPad 4:3
        var resolutions = new (int w, int h, string suffix)[]
        {
            (1280, 720, "arena_preview.png"),
            (1280, 720, "arena_preview_16_9.png"),
            (1560, 720, "arena_preview_19_5_9.png"),
            (1600, 720, "arena_preview_20_9.png"),
            (960, 720, "arena_preview_4_3.png"),
        };

        foreach (var (w, h, filename) in resolutions)
        {
            camera.aspect = (float)w / h;
            if (framer != null) framer.ApplyFraming();

            var rt = new RenderTexture(w, h, 24);
            camera.targetTexture = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            camera.Render();

            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);

            string path = Path.Combine(artifactDir, filename);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[CaptureCurrentArena] Saved {filename} to {path}");
        }

        EditorApplication.Exit(0);
    }

    private static List<CharacterDefinitionSO> LoadCharacterDefs()
    {
        var defs = new List<CharacterDefinitionSO>();
        var guids = AssetDatabase.FindAssets("t:CharacterDefinitionSO", new[] { "Assets/Resources/Characters" });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var def = AssetDatabase.LoadAssetAtPath<CharacterDefinitionSO>(path);
            if (def != null) defs.Add(def);
        }
        defs.Sort((a, b) => string.CompareOrdinal(a.characterId, b.characterId));
        return defs;
    }
}
