using System.Collections.Generic;
using QuizBattle.Characters;
using UnityEngine;

namespace QuizBattle.Arena.Visuals
{
    public struct CharacterVisualResult
    {
        public GameObject Root;
        public Renderer[] Renderers;
    }

    /// Builds 4 distinct, highly creative, full-body stylized characters with expressive heads,
    /// armor, weapons, capes/scarves, and visual props:
    /// 1. Blaze: Golden Horned Gladiator Knight with Flaming Broadsword & Crimson Cape
    /// 2. Aegis: Cyber Titan Mech Golem with Heavy Golden Tower Shield & Power Core
    /// 3. Zephyr: Emerald Shadow Ninja with Dual Storm Daggers, Ninja Hood & Swirling Wind Ring
    /// 4. Vera: Celestial Arcane Sorceress with Wizard Hat, Crystal Staff, Spellbook & Orbiting Halos
    public static class CharacterVisualBuilder
    {
        private static Mesh _bladeMesh;

        public static CharacterVisualResult Build(in CharacterVisual visual, Transform parent)
        {
            var renderers = new List<Renderer>();
            var animator = parent.gameObject.AddComponent<TokenIdleAnimator>();

            switch (visual.Archetype)
            {
            case CharacterArchetype.Fire:
                BuildBlaze(parent, visual, renderers, animator);
                break;
            case CharacterArchetype.Tank:
                BuildAegis(parent, visual, renderers, animator);
                break;
            case CharacterArchetype.Wind:
                BuildZephyr(parent, visual, renderers, animator);
                break;
            case CharacterArchetype.Arcane:
                BuildVera(parent, visual, renderers, animator);
                break;
            default:
                BuildBlaze(parent, visual, renderers, animator);
                break;
            }

            return new CharacterVisualResult { Root = parent.gameObject, Renderers = renderers.ToArray() };
        }

        // =========================================================================
        // 1. BLAZE — The Flaming Gladiator Knight
        // =========================================================================
        private static void BuildBlaze(Transform parent, in CharacterVisual visual, List<Renderer> renderers, TokenIdleAnimator animator)
        {
            var armorMat = ToonMaterialFactory.Instance(new Color(0.92f, 0.22f, 0.15f)); // Crimson Red
            var goldMat = ToonMaterialFactory.Instance(new Color(1.0f, 0.82f, 0.18f));  // Gold Trim
            var skinMat = ToonMaterialFactory.Instance(new Color(0.98f, 0.78f, 0.62f));  // Skin
            var steelMat = ToonMaterialFactory.Instance(new Color(0.68f, 0.72f, 0.80f)); // Steel
            var flameGlowMat = ToonMaterialFactory.GlowInstance(new Color(1.0f, 0.55f, 0.05f), intensity: 0.65f);
            var eyeGlowMat = ToonMaterialFactory.Instance(new Color(1.0f, 0.90f, 0.20f));

            var body = new GameObject("BlazeBody");
            body.transform.SetParent(parent, false);
            body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            animator.SetBodyRoot(body.transform);

            // Torso & Chestplate
            CreateArmorPart(body.transform, "Torso", new Vector3(0, 0.52f, 0), Quaternion.identity, new Vector3(0.40f, 0.40f, 0.28f), armorMat, renderers);
            CreateArmorPart(body.transform, "ChestPlate", new Vector3(0, 0.55f, 0.10f), Quaternion.identity, new Vector3(0.34f, 0.28f, 0.16f), goldMat, renderers);
            CreatePart(body.transform, "CoreGem", PrimitiveMeshFactory.Cone(4, 0.045f, 0f, 0.035f), new Vector3(0, 0.58f, 0.18f), Quaternion.Euler(90f, 0f, 0f), Vector3.one, flameGlowMat, renderers);

            // Cape on back
            CreatePart(body.transform, "Cape", PrimitiveMeshFactory.Cone(4, 0.5f, 0.34f, 1f), new Vector3(0, 0.16f, -0.28f), Quaternion.Euler(14f, 0f, 0f), new Vector3(0.42f, 0.54f, 0.06f), armorMat, renderers);

            // Head & Face
            CreatePrimitivePart(body.transform, "Head", PrimitiveType.Sphere, new Vector3(0, 0.88f, 0), Quaternion.identity, new Vector3(0.27f, 0.30f, 0.28f), skinMat, renderers);
            CreatePrimitivePart(body.transform, "EyeL", PrimitiveType.Sphere, new Vector3(-0.07f, 0.89f, 0.13f), Quaternion.identity, Vector3.one * 0.055f, eyeGlowMat, renderers);
            CreatePrimitivePart(body.transform, "EyeR", PrimitiveType.Sphere, new Vector3(0.07f, 0.89f, 0.13f), Quaternion.identity, Vector3.one * 0.055f, eyeGlowMat, renderers);

            // Gladiator Horned Helmet & Flame Crest
            CreatePart(body.transform, "HelmetDome", PrimitiveMeshFactory.Cone(8, 0.16f, 0.11f, 0.15f), new Vector3(0, 0.93f, -0.02f), Quaternion.Euler(0f, 22.5f, 0f), Vector3.one, goldMat, renderers);
            CreateArmorPart(body.transform, "CheekGuardL", new Vector3(-0.12f, 0.86f, 0.08f), Quaternion.Euler(0f, 0f, -12f), new Vector3(0.055f, 0.16f, 0.08f), goldMat, renderers);
            CreateArmorPart(body.transform, "CheekGuardR", new Vector3(0.12f, 0.86f, 0.08f), Quaternion.Euler(0f, 0f, 12f), new Vector3(0.055f, 0.16f, 0.08f), goldMat, renderers);
            CreatePart(body.transform, "HornL", PrimitiveMeshFactory.Cone(5, 0.04f, 0f, 0.22f), new Vector3(-0.13f, 1.0f, 0f), Quaternion.Euler(0f, 0f, 35f), Vector3.one, goldMat, renderers);
            CreatePart(body.transform, "HornR", PrimitiveMeshFactory.Cone(5, 0.04f, 0f, 0.22f), new Vector3(0.13f, 1.0f, 0f), Quaternion.Euler(0f, 0f, -35f), Vector3.one, goldMat, renderers);

            var crestMesh = PrimitiveMeshFactory.Cone(6, 0.08f, 0f, 0.32f);
            CreatePart(body.transform, "FlameCrest", crestMesh, new Vector3(0, 1.02f, 0.02f), Quaternion.Euler(-18f, 0f, 0f), new Vector3(0.7f, 0.8f, 1f), armorMat, renderers);

            // Shoulders
            CreateArmorPart(body.transform, "ShoulderL", new Vector3(-0.25f, 0.65f, 0), Quaternion.Euler(0f, 0f, 18f), new Vector3(0.22f, 0.18f, 0.26f), goldMat, renderers);
            CreateArmorPart(body.transform, "ShoulderR", new Vector3(0.25f, 0.65f, 0), Quaternion.Euler(0f, 0f, -18f), new Vector3(0.22f, 0.18f, 0.26f), goldMat, renderers);

            // Arms
            CreatePrimitivePart(body.transform, "ArmL", PrimitiveType.Cylinder, new Vector3(-0.25f, 0.46f, 0.04f), Quaternion.Euler(15f, 0f, 10f), new Vector3(0.09f, 0.18f, 0.09f), steelMat, renderers);
            CreatePrimitivePart(body.transform, "ArmR", PrimitiveType.Cylinder, new Vector3(0.25f, 0.46f, 0.04f), Quaternion.Euler(-25f, 0f, -10f), new Vector3(0.09f, 0.18f, 0.09f), steelMat, renderers);

            // Flaming Sword (Right Hand)
            var sword = new GameObject("FlamingSword");
            sword.transform.SetParent(body.transform, false);
            sword.transform.localPosition = new Vector3(0.30f, 0.55f, 0.20f);
            sword.transform.localRotation = Quaternion.Euler(35f, 20f, -15f);
            CreatePrimitivePart(sword.transform, "Hilt", PrimitiveType.Cylinder, Vector3.zero, Quaternion.identity, new Vector3(0.04f, 0.10f, 0.04f), goldMat, renderers);
            CreatePrimitivePart(sword.transform, "Guard", PrimitiveType.Cube, new Vector3(0, 0.10f, 0), Quaternion.identity, new Vector3(0.18f, 0.04f, 0.06f), goldMat, renderers);
            CreatePart(sword.transform, "Blade", BladeMesh(), new Vector3(0, 0.12f, 0), Quaternion.identity, new Vector3(0.11f, 0.58f, 0.035f), steelMat, renderers);
            CreatePart(sword.transform, "FlameInlay", BladeMesh(), new Vector3(0, 0.15f, 0.017f), Quaternion.identity, new Vector3(0.022f, 0.42f, 0.009f), flameGlowMat, renderers);
            CreateArmorPart(body.transform, "SwordGauntlet", new Vector3(0.30f, 0.48f, 0.15f), sword.transform.localRotation, new Vector3(0.12f, 0.13f, 0.12f), goldMat, renderers);

            // Legs & Armored Boots
            CreatePrimitivePart(body.transform, "LegL", PrimitiveType.Cube, new Vector3(-0.10f, 0.18f, 0), Quaternion.identity, new Vector3(0.13f, 0.32f, 0.15f), steelMat, renderers);
            CreatePrimitivePart(body.transform, "LegR", PrimitiveType.Cube, new Vector3(0.10f, 0.18f, 0), Quaternion.identity, new Vector3(0.13f, 0.32f, 0.15f), steelMat, renderers);
            CreatePrimitivePart(body.transform, "BootL", PrimitiveType.Cube, new Vector3(-0.10f, 0.05f, 0.03f), Quaternion.identity, new Vector3(0.14f, 0.10f, 0.20f), goldMat, renderers);
            CreatePrimitivePart(body.transform, "BootR", PrimitiveType.Cube, new Vector3(0.10f, 0.05f, 0.03f), Quaternion.identity, new Vector3(0.14f, 0.10f, 0.20f), goldMat, renderers);

            AddGroundDisc(parent, visual.BaseColor, renderers);
        }

        // =========================================================================
        // 2. AEGIS — The Cyber Titan Mech Golem
        // =========================================================================
        private static void BuildAegis(Transform parent, in CharacterVisual visual, List<Renderer> renderers, TokenIdleAnimator animator)
        {
            var mechMat = ToonMaterialFactory.Instance(new Color(0.24f, 0.32f, 0.42f)); // Dark Titanium
            var goldArmorMat = ToonMaterialFactory.Instance(new Color(0.95f, 0.72f, 0.18f)); // Golden Mech Plating
            var cyanGlowMat = ToonMaterialFactory.GlowInstance(new Color(0.10f, 0.90f, 1.0f), intensity: 0.7f);
            var jointMat = ToonMaterialFactory.Instance(new Color(0.12f, 0.15f, 0.18f));

            var body = new GameObject("AegisBody");
            body.transform.SetParent(parent, false);
            body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            animator.SetBodyRoot(body.transform);

            // Heavy Titan Torso
            CreateArmorPart(body.transform, "Torso", new Vector3(0, 0.54f, 0), Quaternion.identity, new Vector3(0.52f, 0.42f, 0.36f), mechMat, renderers);
            CreateArmorPart(body.transform, "ChestPlate", new Vector3(0, 0.58f, 0.12f), Quaternion.identity, new Vector3(0.44f, 0.30f, 0.18f), goldArmorMat, renderers);
            CreatePart(body.transform, "ReactorSocket", PrimitiveMeshFactory.Cone(8, 0.095f, 0.08f, 0.03f), new Vector3(0, 0.58f, 0.20f), Quaternion.Euler(90f, 0f, 0f), Vector3.one, jointMat, renderers);
            CreatePrimitivePart(body.transform, "PowerReactor", PrimitiveType.Cylinder, new Vector3(0, 0.58f, 0.235f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.11f, 0.009f, 0.11f), cyanGlowMat, renderers);

            // Dual Exhaust Stacks on back
            CreatePrimitivePart(body.transform, "ExhaustL", PrimitiveType.Cylinder, new Vector3(-0.15f, 0.76f, -0.18f), Quaternion.Euler(-12f, 0f, 0f), new Vector3(0.06f, 0.22f, 0.06f), jointMat, renderers);
            CreatePrimitivePart(body.transform, "ExhaustR", PrimitiveType.Cylinder, new Vector3(0.15f, 0.76f, -0.18f), Quaternion.Euler(-12f, 0f, 0f), new Vector3(0.06f, 0.22f, 0.06f), jointMat, renderers);

            // Robot Head & Cyber Visor
            CreateArmorPart(body.transform, "Head", new Vector3(0, 0.88f, 0.02f), Quaternion.identity, new Vector3(0.32f, 0.26f, 0.28f), mechMat, renderers);
            CreatePrimitivePart(body.transform, "HeadPlate", PrimitiveType.Cube, new Vector3(0, 0.98f, 0.02f), Quaternion.identity, new Vector3(0.24f, 0.06f, 0.26f), goldArmorMat, renderers);
            CreatePrimitivePart(body.transform, "VisorSocket", PrimitiveType.Cube, new Vector3(0, 0.88f, 0.15f), Quaternion.identity, new Vector3(0.26f, 0.09f, 0.035f), jointMat, renderers);
            CreatePrimitivePart(body.transform, "CyanVisor", PrimitiveType.Cube, new Vector3(0, 0.88f, 0.17f), Quaternion.identity, new Vector3(0.22f, 0.035f, 0.015f), cyanGlowMat, renderers);

            // Massive Shoulder Pauldrons
            CreateArmorPart(body.transform, "PauldronsL", new Vector3(-0.32f, 0.68f, 0), Quaternion.Euler(0f, 0f, 20f), new Vector3(0.26f, 0.22f, 0.32f), goldArmorMat, renderers);
            CreateArmorPart(body.transform, "PauldronsR", new Vector3(0.32f, 0.68f, 0), Quaternion.Euler(0f, 0f, -20f), new Vector3(0.26f, 0.22f, 0.32f), goldArmorMat, renderers);

            // Arms
            CreatePrimitivePart(body.transform, "ArmL", PrimitiveType.Cylinder, new Vector3(-0.30f, 0.44f, 0.06f), Quaternion.Euler(15f, 0f, 15f), new Vector3(0.11f, 0.20f, 0.11f), mechMat, renderers);
            CreatePrimitivePart(body.transform, "ArmR", PrimitiveType.Cylinder, new Vector3(0.30f, 0.44f, 0.06f), Quaternion.Euler(-10f, 0f, -15f), new Vector3(0.11f, 0.20f, 0.11f), mechMat, renderers);

            // Giant Spiked Tower Shield (Left Arm)
            var shield = new GameObject("TowerShield");
            shield.transform.SetParent(body.transform, false);
            shield.transform.localPosition = new Vector3(-0.39f, 0.46f, 0.24f);
            shield.transform.localRotation = Quaternion.Euler(5f, -12f, -6f);
            CreateArmorPart(shield.transform, "ShieldPlate", Vector3.zero, Quaternion.identity, new Vector3(0.40f, 0.65f, 0.10f), goldArmorMat, renderers);
            CreateArmorPart(shield.transform, "ShieldInset", new Vector3(0, 0.015f, 0.05f), Quaternion.identity, new Vector3(0.30f, 0.52f, 0.035f), mechMat, renderers);
            CreatePart(shield.transform, "ShieldCore", PrimitiveMeshFactory.Cone(6, 0.075f, 0.06f, 0.018f), new Vector3(0, 0f, 0.07f), Quaternion.Euler(90f, 0f, 0f), Vector3.one, cyanGlowMat, renderers);
            CreatePart(shield.transform, "ShieldSpike", PrimitiveMeshFactory.Cone(4, 0.045f, 0f, 0.12f), new Vector3(0, 0f, 0.088f), Quaternion.Euler(90f, 0f, 0f), Vector3.one, goldArmorMat, renderers);

            // Right Power Gauntlet
            CreateArmorPart(body.transform, "PowerFist", new Vector3(0.32f, 0.32f, 0.18f), Quaternion.identity, new Vector3(0.19f, 0.18f, 0.22f), goldArmorMat, renderers);

            // Heavy Hydraulic Legs
            CreatePrimitivePart(body.transform, "LegL", PrimitiveType.Cube, new Vector3(-0.13f, 0.18f, 0), Quaternion.identity, new Vector3(0.17f, 0.34f, 0.20f), mechMat, renderers);
            CreatePrimitivePart(body.transform, "LegR", PrimitiveType.Cube, new Vector3(0.13f, 0.18f, 0), Quaternion.identity, new Vector3(0.17f, 0.34f, 0.20f), mechMat, renderers);
            CreateArmorPart(body.transform, "FootL", new Vector3(-0.13f, 0.055f, 0.04f), Quaternion.identity, new Vector3(0.20f, 0.11f, 0.28f), jointMat, renderers);
            CreateArmorPart(body.transform, "FootR", new Vector3(0.13f, 0.055f, 0.04f), Quaternion.identity, new Vector3(0.20f, 0.11f, 0.28f), jointMat, renderers);

            AddGroundDisc(parent, visual.BaseColor, renderers);
        }

        // =========================================================================
        // 3. ZEPHYR — The Emerald Shadow Ninja
        // =========================================================================
        private static void BuildZephyr(Transform parent, in CharacterVisual visual, List<Renderer> renderers, TokenIdleAnimator animator)
        {
            var rogueMat = ToonMaterialFactory.Instance(new Color(0.12f, 0.18f, 0.16f)); // Dark Shinobi Obsidian
            var emeraldMat = ToonMaterialFactory.Instance(new Color(0.18f, 0.75f, 0.35f)); // Emerald Green
            var goldTrimMat = ToonMaterialFactory.Instance(new Color(1.0f, 0.85f, 0.20f));
            var steelMat = ToonMaterialFactory.Instance(new Color(0.62f, 0.76f, 0.74f));
            var windGlowMat = ToonMaterialFactory.GlowInstance(new Color(0.20f, 1.0f, 0.50f), intensity: 0.55f);
            var eyeGlowMat = ToonMaterialFactory.Instance(new Color(0.40f, 1.0f, 0.60f));

            var body = new GameObject("ZephyrBody");
            body.transform.SetParent(parent, false);
            body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            animator.SetBodyRoot(body.transform);

            // Sleek Ninja Torso
            CreateArmorPart(body.transform, "Torso", new Vector3(0, 0.50f, 0), Quaternion.identity, new Vector3(0.34f, 0.38f, 0.22f), rogueMat, renderers);
            CreateArmorPart(body.transform, "Vest", new Vector3(0, 0.52f, 0.06f), Quaternion.identity, new Vector3(0.28f, 0.30f, 0.14f), emeraldMat, renderers);
            CreatePrimitivePart(body.transform, "Belt", PrimitiveType.Cube, new Vector3(0, 0.34f, 0.02f), Quaternion.identity, new Vector3(0.34f, 0.08f, 0.24f), goldTrimMat, renderers);

            // Ninja Hood & Masked Face
            CreateArmorPart(body.transform, "NinjaHood", new Vector3(0, 0.86f, 0), Quaternion.identity, new Vector3(0.30f, 0.32f, 0.30f), rogueMat, renderers);
            CreateArmorPart(body.transform, "FaceMask", new Vector3(0, 0.83f, 0.12f), Quaternion.identity, new Vector3(0.22f, 0.14f, 0.10f), emeraldMat, renderers);
            CreatePrimitivePart(body.transform, "EyeL", PrimitiveType.Sphere, new Vector3(-0.065f, 0.89f, 0.13f), Quaternion.identity, new Vector3(0.045f, 0.035f, 0.045f), eyeGlowMat, renderers);
            CreatePrimitivePart(body.transform, "EyeR", PrimitiveType.Sphere, new Vector3(0.065f, 0.89f, 0.13f), Quaternion.identity, new Vector3(0.045f, 0.035f, 0.045f), eyeGlowMat, renderers);

            // Flowing Wind Scarf trailing behind
            CreatePrimitivePart(body.transform, "ScarfCollar", PrimitiveType.Cylinder, new Vector3(0, 0.73f, 0), Quaternion.identity, new Vector3(0.26f, 0.04f, 0.24f), emeraldMat, renderers);
            CreatePart(body.transform, "ScarfTailL", PrimitiveMeshFactory.Cone(4, 0.045f, 0f, 0.42f), new Vector3(-0.07f, 0.73f, -0.10f), Quaternion.Euler(-115f, -15f, -12f), new Vector3(1f, 1f, 0.25f), emeraldMat, renderers);
            CreatePart(body.transform, "ScarfTailR", PrimitiveMeshFactory.Cone(4, 0.045f, 0f, 0.48f), new Vector3(0.07f, 0.73f, -0.10f), Quaternion.Euler(-120f, 15f, 12f), new Vector3(1f, 1f, 0.25f), emeraldMat, renderers);
            CreateArmorPart(body.transform, "ArmL", new Vector3(-0.20f, 0.52f, 0.06f), Quaternion.Euler(-25f, 0f, -12f), new Vector3(0.10f, 0.28f, 0.11f), rogueMat, renderers);
            CreateArmorPart(body.transform, "ArmR", new Vector3(0.20f, 0.52f, 0.06f), Quaternion.Euler(-25f, 0f, 12f), new Vector3(0.10f, 0.28f, 0.11f), rogueMat, renderers);
            CreateArmorPart(body.transform, "GloveL", new Vector3(-0.24f, 0.42f, 0.13f), Quaternion.identity, new Vector3(0.11f, 0.11f, 0.12f), emeraldMat, renderers);
            CreateArmorPart(body.transform, "GloveR", new Vector3(0.24f, 0.42f, 0.13f), Quaternion.identity, new Vector3(0.11f, 0.11f, 0.12f), emeraldMat, renderers);

            // Dual Storm Daggers (Left & Right Hands)
            var daggerL = new GameObject("DaggerL");
            daggerL.transform.SetParent(body.transform, false);
            daggerL.transform.localPosition = new Vector3(-0.24f, 0.45f, 0.16f);
            daggerL.transform.localRotation = Quaternion.Euler(40f, 0f, 25f);
            CreatePart(daggerL.transform, "Blade", BladeMesh(), new Vector3(0, 0.08f, 0), Quaternion.identity, new Vector3(0.075f, 0.28f, 0.025f), steelMat, renderers);
            CreatePrimitivePart(daggerL.transform, "Guard", PrimitiveType.Cube, new Vector3(0, 0.08f, 0), Quaternion.identity, new Vector3(0.12f, 0.025f, 0.045f), goldTrimMat, renderers);
            CreatePrimitivePart(daggerL.transform, "Hilt", PrimitiveType.Cylinder, Vector3.zero, Quaternion.identity, new Vector3(0.03f, 0.08f, 0.03f), goldTrimMat, renderers);

            var daggerR = new GameObject("DaggerR");
            daggerR.transform.SetParent(body.transform, false);
            daggerR.transform.localPosition = new Vector3(0.24f, 0.45f, 0.16f);
            daggerR.transform.localRotation = Quaternion.Euler(40f, 0f, -25f);
            CreatePart(daggerR.transform, "Blade", BladeMesh(), new Vector3(0, 0.08f, 0), Quaternion.identity, new Vector3(0.075f, 0.28f, 0.025f), steelMat, renderers);
            CreatePrimitivePart(daggerR.transform, "Guard", PrimitiveType.Cube, new Vector3(0, 0.08f, 0), Quaternion.identity, new Vector3(0.12f, 0.025f, 0.045f), goldTrimMat, renderers);
            CreatePrimitivePart(daggerR.transform, "Hilt", PrimitiveType.Cylinder, Vector3.zero, Quaternion.identity, new Vector3(0.03f, 0.08f, 0.03f), goldTrimMat, renderers);

            // Aerodynamic Wind Fins on Shoulders
            var finMesh = PrimitiveMeshFactory.Cone(4, 0.04f, 0f, 0.24f);
            CreatePart(body.transform, "FinLeft", finMesh, new Vector3(-0.16f, 0.68f, -0.04f), Quaternion.Euler(65f, 20f, 60f), Vector3.one, emeraldMat, renderers);
            CreatePart(body.transform, "FinRight", finMesh, new Vector3(0.16f, 0.68f, -0.04f), Quaternion.Euler(65f, -20f, -60f), Vector3.one, emeraldMat, renderers);

            // Spinning Ankle Wind Tornado Ring
            var ringMesh = PrimitiveMeshFactory.Torus(0.24f, 0.009f, 16, 6);
            var ring = CreatePart(body.transform, "AnkleRing", ringMesh, new Vector3(0, 0.08f, 0), Quaternion.identity, Vector3.one, windGlowMat, renderers);
            animator.Register(ring.transform, bobAmount: 0f, spinSpeed: 35f);

            // Legs & Ninja Tabis
            CreatePrimitivePart(body.transform, "LegL", PrimitiveType.Cube, new Vector3(-0.09f, 0.18f, 0), Quaternion.identity, new Vector3(0.11f, 0.30f, 0.13f), rogueMat, renderers);
            CreatePrimitivePart(body.transform, "LegR", PrimitiveType.Cube, new Vector3(0.09f, 0.18f, 0), Quaternion.identity, new Vector3(0.11f, 0.30f, 0.13f), rogueMat, renderers);
            CreatePrimitivePart(body.transform, "FootL", PrimitiveType.Cube, new Vector3(-0.09f, 0.04f, 0.03f), Quaternion.identity, new Vector3(0.12f, 0.08f, 0.18f), emeraldMat, renderers);
            CreatePrimitivePart(body.transform, "FootR", PrimitiveType.Cube, new Vector3(0.09f, 0.04f, 0.03f), Quaternion.identity, new Vector3(0.12f, 0.08f, 0.18f), emeraldMat, renderers);

            AddGroundDisc(parent, visual.BaseColor, renderers);
        }

        // =========================================================================
        // 4. VERA — The Celestial Arcane Sorceress
        // =========================================================================
        private static void BuildVera(Transform parent, in CharacterVisual visual, List<Renderer> renderers, TokenIdleAnimator animator)
        {
            var robeMat = ToonMaterialFactory.Instance(new Color(0.48f, 0.14f, 0.72f)); // Royal Amethyst Purple
            var magentaTrim = ToonMaterialFactory.Instance(new Color(0.85f, 0.22f, 0.65f)); // Magenta Silk
            var goldMat = ToonMaterialFactory.Instance(new Color(1.0f, 0.84f, 0.22f));
            var skinMat = ToonMaterialFactory.Instance(new Color(0.96f, 0.82f, 0.74f));
            var purpleGlowMat = ToonMaterialFactory.GlowInstance(new Color(0.80f, 0.25f, 1.0f), intensity: 0.6f);
            var crystalMat = ToonMaterialFactory.Instance(new Color(0.67f, 0.40f, 0.92f));
            var pageMat = ToonMaterialFactory.Instance(new Color(0.91f, 0.85f, 0.70f));
            var eyeGlowMat = ToonMaterialFactory.Instance(new Color(0.32f, 0.12f, 0.48f));

            var body = new GameObject("VeraBody");
            body.transform.SetParent(parent, false);
            body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            animator.SetBodyRoot(body.transform);

            // Flowing Wizard Robe & Bodice
            CreateArmorPart(body.transform, "RobeTorso", new Vector3(0, 0.52f, 0), Quaternion.identity, new Vector3(0.32f, 0.38f, 0.24f), robeMat, renderers);
            CreateArmorPart(body.transform, "Bodice", new Vector3(0, 0.54f, 0.06f), Quaternion.identity, new Vector3(0.24f, 0.28f, 0.16f), magentaTrim, renderers);
            CreatePart(body.transform, "RobeSkirt", PrimitiveMeshFactory.Cone(8, 0.26f, 0.14f, 0.34f), new Vector3(0, 0.035f, 0), Quaternion.identity, Vector3.one, robeMat, renderers);
            CreatePart(body.transform, "RobeHem", PrimitiveMeshFactory.Cone(8, 0.265f, 0.256f, 0.025f), new Vector3(0, 0.035f, 0), Quaternion.identity, Vector3.one, goldMat, renderers);
            CreateArmorPart(body.transform, "BootL", new Vector3(-0.09f, 0.035f, 0.10f), Quaternion.identity, new Vector3(0.12f, 0.07f, 0.21f), magentaTrim, renderers);
            CreateArmorPart(body.transform, "BootR", new Vector3(0.09f, 0.035f, 0.10f), Quaternion.identity, new Vector3(0.12f, 0.07f, 0.21f), magentaTrim, renderers);
            CreatePrimitivePart(body.transform, "GoldSash", PrimitiveType.Cube, new Vector3(0, 0.36f, 0.02f), Quaternion.identity, new Vector3(0.34f, 0.06f, 0.26f), goldMat, renderers);

            // Sorceress Head & Face
            CreatePrimitivePart(body.transform, "Head", PrimitiveType.Sphere, new Vector3(0, 0.86f, 0), Quaternion.identity, new Vector3(0.26f, 0.29f, 0.27f), skinMat, renderers);
            CreatePrimitivePart(body.transform, "EyeL", PrimitiveType.Sphere, new Vector3(-0.065f, 0.87f, 0.12f), Quaternion.identity, Vector3.one * 0.048f, eyeGlowMat, renderers);
            CreatePrimitivePart(body.transform, "EyeR", PrimitiveType.Sphere, new Vector3(0.065f, 0.87f, 0.12f), Quaternion.identity, Vector3.one * 0.048f, eyeGlowMat, renderers);

            // Pointed Wizard Hat with Gold Crescent Moon Buckle
            CreatePart(body.transform, "HatBrim", PrimitiveMeshFactory.Cone(8, 0.24f, 0.22f, 0.03f), new Vector3(0, 0.945f, -0.02f), Quaternion.identity, Vector3.one, robeMat, renderers);
            var hatConeMesh = PrimitiveMeshFactory.Cone(8, 0.20f, 0f, 0.42f);
            CreatePart(body.transform, "HatCone", hatConeMesh, new Vector3(0, 0.98f, -0.02f), Quaternion.Euler(-14f, 0f, 0f), Vector3.one, robeMat, renderers);
            CreatePrimitivePart(body.transform, "HatBand", PrimitiveType.Cylinder, new Vector3(0, 0.99f, -0.02f), Quaternion.identity, new Vector3(0.38f, 0.04f, 0.38f), magentaTrim, renderers);
            CreatePart(body.transform, "MoonBuckle", PrimitiveMeshFactory.Torus(0.06f, 0.015f, 12, 6), new Vector3(0, 1.01f, 0.16f), Quaternion.Euler(90f, 0f, 0f), Vector3.one, goldMat, renderers);
            CreateArmorPart(body.transform, "SleeveL", new Vector3(-0.20f, 0.57f, 0.06f), Quaternion.Euler(-25f, 0f, -25f), new Vector3(0.14f, 0.25f, 0.15f), robeMat, renderers);
            CreateArmorPart(body.transform, "SleeveR", new Vector3(0.20f, 0.56f, 0.06f), Quaternion.Euler(-25f, 0f, 25f), new Vector3(0.14f, 0.25f, 0.15f), robeMat, renderers);
            CreatePrimitivePart(body.transform, "HandL", PrimitiveType.Sphere, new Vector3(-0.27f, 0.49f, 0.14f), Quaternion.identity, Vector3.one * 0.10f, skinMat, renderers);
            CreatePrimitivePart(body.transform, "HandR", PrimitiveType.Sphere, new Vector3(0.28f, 0.46f, 0.15f), Quaternion.identity, Vector3.one * 0.10f, skinMat, renderers);

            // Crystal Mage Staff (Right Hand)
            var staff = new GameObject("CrystalStaff");
            staff.transform.SetParent(body.transform, false);
            staff.transform.localPosition = new Vector3(0.28f, 0.45f, 0.15f);
            staff.transform.localRotation = Quaternion.Euler(15f, -10f, -8f);
            CreatePrimitivePart(staff.transform, "Shaft", PrimitiveType.Cylinder, Vector3.zero, Quaternion.identity, new Vector3(0.035f, 0.425f, 0.035f), goldMat, renderers);
            CreatePart(staff.transform, "StaffHead", PrimitiveMeshFactory.Torus(0.12f, 0.015f, 14, 6), new Vector3(0, 0.46f, 0), Quaternion.Euler(90f, 0f, 0f), Vector3.one, goldMat, renderers);
            var gem = CreatePart(staff.transform, "FloatingGem", BladeMesh(), new Vector3(0, 0.40f, 0), Quaternion.identity, new Vector3(0.10f, 0.16f, 0.08f), crystalMat, renderers);
            animator.Register(gem.transform, bobSpeed: 2f, bobAmount: 0.008f, spinSpeed: 24f);

            // Floating Open Arcane Spellbook (Left Hand)
            var book = new GameObject("Spellbook");
            book.transform.SetParent(body.transform, false);
            book.transform.localPosition = new Vector3(-0.28f, 0.58f, 0.22f);
            book.transform.localRotation = Quaternion.Euler(25f, 25f, -10f);
            CreatePrimitivePart(book.transform, "CoverL", PrimitiveType.Cube, new Vector3(-0.075f, 0.02f, 0f), Quaternion.Euler(0f, 0f, -15f), new Vector3(0.16f, 0.02f, 0.22f), robeMat, renderers);
            CreatePrimitivePart(book.transform, "CoverR", PrimitiveType.Cube, new Vector3(0.075f, 0.02f, 0f), Quaternion.Euler(0f, 0f, 15f), new Vector3(0.16f, 0.02f, 0.22f), robeMat, renderers);
            CreatePrimitivePart(book.transform, "PagesL", PrimitiveType.Cube, new Vector3(-0.07f, 0.037f, 0f), Quaternion.Euler(0f, 0f, -15f), new Vector3(0.14f, 0.02f, 0.19f), pageMat, renderers);
            CreatePrimitivePart(book.transform, "PagesR", PrimitiveType.Cube, new Vector3(0.07f, 0.037f, 0f), Quaternion.Euler(0f, 0f, 15f), new Vector3(0.14f, 0.02f, 0.19f), pageMat, renderers);
            CreatePrimitivePart(book.transform, "Spine", PrimitiveType.Cube, Vector3.zero, Quaternion.identity, new Vector3(0.025f, 0.025f, 0.23f), goldMat, renderers);
            animator.Register(book.transform, bobSpeed: 1.6f, bobAmount: 0.012f);

            // Dual Rotating Celestial Halos around shoulders
            var haloMesh = PrimitiveMeshFactory.Torus(0.26f, 0.009f, 16, 6);
            var haloA = CreatePart(body.transform, "HaloA", haloMesh, new Vector3(0, 0.72f, 0), Quaternion.Euler(70f, 0f, 0f), Vector3.one, purpleGlowMat, renderers);
            var haloB = CreatePart(body.transform, "HaloB", haloMesh, new Vector3(0, 0.68f, 0), Quaternion.Euler(20f, 60f, 0f), Vector3.one * 0.88f, purpleGlowMat, renderers);
            animator.Register(haloA.transform, bobAmount: 0f, spinSpeed: 18f);
            animator.Register(haloB.transform, bobAmount: 0f, spinSpeed: -24f);

            // Floating Orbiting Mana Sphere
            var orb = CreatePrimitivePart(body.transform, "Orb", PrimitiveType.Sphere, new Vector3(0, 0.55f, 0.30f), Quaternion.identity, Vector3.one * 0.06f, purpleGlowMat, renderers);
            animator.Register(orb.transform, bobSpeed: 1.8f, bobAmount: 0.012f);

            AddGroundDisc(parent, visual.BaseColor, renderers);
        }

        private static void AddGroundDisc(Transform parent, Color color, List<Renderer> renderers, float radius = 0.42f)
        {
            var ringMat = ToonMaterialFactory.Instance(color);
            var ringMesh = PrimitiveMeshFactory.Torus(radius * 1.10f, 0.022f, 24, 6);
            CreatePart(parent, "TeamBaseRing", ringMesh, new Vector3(0, 0.018f, 0), Quaternion.identity, new Vector3(1f, 0.7f, 1f), ringMat, renderers);
        }

        private static GameObject CreateArmorPart(Transform parent, string name, Vector3 localPos, Quaternion localRot,
                Vector3 localScale, Material material, List<Renderer> renderers)
        {
            return CreatePart(parent, name, PrimitiveMeshFactory.Cone(8, 0.42f, 0.5f, 1f),
                              localPos - localRot * new Vector3(0f, localScale.y * 0.5f, 0f), localRot, localScale, material, renderers);
        }

        private static Mesh BladeMesh()
        {
            if (_bladeMesh != null) return _bladeMesh;

            var points = new[]
            {
                new Vector3(0.5f, 0f, 0f), new Vector3(0f, 0f, 0.5f),
                new Vector3(-0.5f, 0f, 0f), new Vector3(0f, 0f, -0.5f),
                new Vector3(0.5f, 0.72f, 0f), new Vector3(0f, 0.72f, 0.5f),
                new Vector3(-0.5f, 0.72f, 0f), new Vector3(0f, 0.72f, -0.5f),
                new Vector3(0f, 1f, 0f),
            };
            var indices = new List<int> { 0, 1, 2, 0, 2, 3 };
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                indices.Add(i); indices.Add(i + 4); indices.Add(next);
                indices.Add(next); indices.Add(i + 4); indices.Add(next + 4);
                indices.Add(i + 4); indices.Add(8); indices.Add(next + 4);
            }

            var vertices = new Vector3[indices.Count];
            var triangles = new int[indices.Count];
            var uv = new Vector2[indices.Count];
            var colors = new Color[indices.Count];
            for (int i = 0; i < indices.Count; i++)
            {
                vertices[i] = points[indices[i]];
                triangles[i] = i;
                uv[i] = new Vector2(0.5f, 0.5f);
                colors[i] = Color.white;
            }

            _bladeMesh = new Mesh { name = "QB_FacetedBlade", hideFlags = HideFlags.DontUnloadUnusedAsset };
            _bladeMesh.vertices = vertices;
            _bladeMesh.triangles = triangles;
            _bladeMesh.uv = uv;
            _bladeMesh.colors = colors;
            _bladeMesh.RecalculateNormals();
            _bladeMesh.RecalculateBounds();
            return _bladeMesh;
        }

        private static GameObject CreatePart(Transform parent, string name, Mesh mesh, Vector3 localPos, Quaternion localRot,
                                             Vector3 localScale, Material material, List<Renderer> renderers)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderers.Add(renderer);
            return go;
        }

        private static GameObject CreatePrimitivePart(Transform parent, string name, PrimitiveType type, Vector3 localPos,
                Quaternion localRot, Vector3 localScale, Material material, List<Renderer> renderers)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            Object.Destroy(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderers.Add(renderer);
            return go;
        }
    }
}
