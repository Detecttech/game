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

        const int width = 1280;
        const int height = 720;
        var rt = new RenderTexture(width, height, 24);
        camera.targetTexture = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        camera.Render();

        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        camera.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);

        string outputPath = @"C:\Users\engmh\.gemini\antigravity-ide\brain\531cdd05-2b28-4037-b8c7-b10c24a8917d\arena_preview.png";
        File.WriteAllBytes(outputPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        Debug.Log($"[CaptureCurrentArena] Arena preview saved to {outputPath}");
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
