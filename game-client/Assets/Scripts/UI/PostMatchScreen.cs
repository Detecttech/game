using System.Collections.Generic;
using System.Linq;
using QuizBattle.Arena;
using QuizBattle.Bootstrap;
using QuizBattle.Characters;
using QuizBattle.Networking;
using QuizBattle.Networking.Protocol;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QuizBattle.UI
{
    /// High-energy, funky celebration and results screen.
    /// Uses procedural high-res vector-style sprites (Trophy, Sunburst, Sparkles, Alarm Clock)
    /// to ensure 100% crisp visuals and zero missing-font tofu glyphs on any platform.
    public class PostMatchScreen : MonoBehaviour
    {
        private void Start()
        {
            var canvas = UiFactory.CreateCanvas("PostMatchCanvas");
            var result = AppRoot.Instance.Store.MatchResult;
            var xp = AppRoot.Instance.Store.LastXpAward;
            int myPlayerId = SessionManager.PlayerId ?? -1;

            StandingEntry myStanding = null;
            if (result?.Standings != null)
            {
                myStanding = result.Standings.FirstOrDefault(s => s.PlayerId == myPlayerId);
            }

            bool isWinner = result != null && (
                (result.WinnerId != null && result.WinnerId.ToString() == myPlayerId.ToString()) ||
                (myStanding != null && myStanding.Placement == 1)
            );

            bool isTimedOut = myStanding != null && myStanding.TimedOut;
            int placement = myStanding?.Placement ?? (isWinner ? 1 : 2);

            if (isWinner)
            {
                BuildWinnerCelebration(canvas.transform, result, xp, myStanding);
            }
            else if (isTimedOut)
            {
                BuildFunkyTimeoutScreen(canvas.transform, result, xp, myStanding, placement);
            }
            else
            {
                BuildRunnerUpScreen(canvas.transform, result, xp, myStanding, placement);
            }
        }

        private void BuildWinnerCelebration(Transform parent, MatchEndPayload result, XpAwardPayload xp, StandingEntry myStanding)
        {
            // Full-screen rich disco backdrop
            var backdrop = UiFactory.CreatePanel(
                parent, "Backdrop", new Vector2(0.5f, 0.5f), new Vector2(1060, 710),
                new Color(0.12f, 0.05f, 0.22f, 0.96f));
            backdrop.gameObject.AddComponent<FunkyDiscoAnimator>();

            // --- 1. PROCEDURAL 3D/2D GOLDEN TROPHY & RADIANT SUNBURST ---
            var trophyContainer = new GameObject("TrophyContainer");
            trophyContainer.transform.SetParent(backdrop.transform, false);
            var tcRect = trophyContainer.AddComponent<RectTransform>();
            tcRect.anchorMin = tcRect.anchorMax = new Vector2(0.5f, 0.86f);
            tcRect.sizeDelta = new Vector2(240, 150);
            trophyContainer.AddComponent<FunkyPulseEffect>();

            // Rotating Sunburst Halo behind the Trophy
            var sunburstObj = new GameObject("SunburstHalo");
            sunburstObj.transform.SetParent(trophyContainer.transform, false);
            var sbRect = sunburstObj.AddComponent<RectTransform>();
            sbRect.anchorMin = sbRect.anchorMax = new Vector2(0.5f, 0.5f);
            sbRect.sizeDelta = new Vector2(220, 220);
            var sbImage = sunburstObj.AddComponent<Image>();
            sbImage.sprite = PostMatchVisualFactory.GetSunburstSprite();
            sbImage.color = new Color(1f, 0.85f, 0.2f, 0.65f);
            sunburstObj.AddComponent<FunkySpinEffect>();

            // Sparkling Gold Trophy Cup Image
            var trophyObj = new GameObject("TrophyCup");
            trophyObj.transform.SetParent(trophyContainer.transform, false);
            var trRect = trophyObj.AddComponent<RectTransform>();
            trRect.anchorMin = trRect.anchorMax = new Vector2(0.5f, 0.5f);
            trRect.sizeDelta = new Vector2(140, 140);
            var trImage = trophyObj.AddComponent<Image>();
            trImage.sprite = PostMatchVisualFactory.GetTrophySprite();
            trImage.color = Color.white;

            // 4 Floating Sparkle Stars
            AddSparkle(trophyContainer.transform, new Vector2(-80, 50), 32, 0f);
            AddSparkle(trophyContainer.transform, new Vector2(80, 55), 36, 0.5f);
            AddSparkle(trophyContainer.transform, new Vector2(-95, -25), 26, 1.0f);
            AddSparkle(trophyContainer.transform, new Vector2(90, -20), 28, 1.5f);

            // --- 2. VIBRANT HEADLINE PLACARD ---
            var (banner, innerBanner) = UiFactory.CreateBannerPanel(
                backdrop.transform, "WinBanner", new Vector2(0.5f, 0.71f), new Vector2(760, 68),
                new Color(0.96f, 0.68f, 0.08f), new Vector2(0, 0));
            var headline = UiFactory.CreateText(
                innerBanner.transform, "Headline", new Vector2(0.5f, 0.5f), new Vector2(740, 58), 28);
            headline.text = "GROOVY CHAMPION! 1ST PLACE!";
            headline.fontStyle = FontStyles.Bold;
            headline.color = new Color(0.15f, 0.05f, 0.25f);

            // Subtitle
            var sub = UiFactory.CreateText(
                backdrop.transform, "Subtitle", new Vector2(0.5f, 0.63f), new Vector2(700, 32), 17);
            sub.text = "You conquered the quiz and crossed the finish line first!";
            sub.fontStyle = FontStyles.Bold;
            sub.color = new Color(1f, 0.88f, 0.45f);

            // --- 3. TOP FINISHERS PODIUM ---
            BuildPodium(backdrop.transform, result, new Vector2(0.5f, 0.45f));

            // --- 4. GROOVY XP CARD ---
            BuildXpCard(backdrop.transform, xp, new Vector2(0.5f, 0.24f), true);

            // --- 5. PLAY AGAIN BUTTON ---
            BuildPlayAgainButton(backdrop.transform, new Vector2(0.5f, 0.09f));
        }

        private void BuildFunkyTimeoutScreen(Transform parent, MatchEndPayload result, XpAwardPayload xp, StandingEntry myStanding, int placement)
        {
            // Comical funky retro backdrop
            var backdrop = UiFactory.CreatePanel(
                parent, "Backdrop", new Vector2(0.5f, 0.5f), new Vector2(1060, 710),
                new Color(0.20f, 0.06f, 0.08f, 0.96f));

            // Procedural Ringing Alarm Clock Graphic
            var clockContainer = new GameObject("ClockContainer");
            clockContainer.transform.SetParent(backdrop.transform, false);
            var ccRect = clockContainer.AddComponent<RectTransform>();
            ccRect.anchorMin = ccRect.anchorMax = new Vector2(0.5f, 0.86f);
            ccRect.sizeDelta = new Vector2(200, 140);
            clockContainer.AddComponent<FunkyWiggleEffect>();

            var clockObj = new GameObject("AlarmClock");
            clockObj.transform.SetParent(clockContainer.transform, false);
            var clkRect = clockObj.AddComponent<RectTransform>();
            clkRect.anchorMin = clkRect.anchorMax = new Vector2(0.5f, 0.5f);
            clkRect.sizeDelta = new Vector2(130, 130);
            var clkImg = clockObj.AddComponent<Image>();
            clkImg.sprite = PostMatchVisualFactory.GetAlarmClockSprite();
            clkImg.color = Color.white;

            // Comical "FUNKY SORRY! TIME'S UP!" Banner
            var (banner, innerBanner) = UiFactory.CreateBannerPanel(
                backdrop.transform, "TimeoutBanner", new Vector2(0.5f, 0.71f), new Vector2(760, 68),
                new Color(0.92f, 0.22f, 0.22f), new Vector2(0, 0));
            var headline = UiFactory.CreateText(
                innerBanner.transform, "Headline", new Vector2(0.5f, 0.5f), new Vector2(740, 58), 28);
            headline.text = "FUNKY SORRY! TIME'S UP!";
            headline.fontStyle = FontStyles.Bold;
            headline.color = Color.white;

            // Funky punchline subtitle
            var sub = UiFactory.CreateText(
                backdrop.transform, "Subtitle", new Vector2(0.5f, 0.63f), new Vector2(740, 36), 16);
            sub.text = "The groove stopped before the finish line! Great hustle — here's your groovy XP!";
            sub.fontStyle = FontStyles.Bold;
            sub.color = new Color(1f, 0.75f, 0.75f);

            // Standings Showcase
            BuildPodium(backdrop.transform, result, new Vector2(0.5f, 0.45f));

            // Consolation XP Card
            BuildXpCard(backdrop.transform, xp, new Vector2(0.5f, 0.24f), false);

            // Action Button
            BuildPlayAgainButton(backdrop.transform, new Vector2(0.5f, 0.09f));
        }

        private void BuildRunnerUpScreen(Transform parent, MatchEndPayload result, XpAwardPayload xp, StandingEntry myStanding, int placement)
        {
            var backdrop = UiFactory.CreatePanel(
                parent, "Backdrop", new Vector2(0.5f, 0.5f), new Vector2(1060, 710),
                new Color(0.08f, 0.12f, 0.22f, 0.96f));

            string rankTitle = placement == 2 ? "2ND PLACE! AWESOME RUN!" : $"#{placement} PLACE FINISH!";

            // Procedural Silver/Bronze Medallion Graphic
            var medalContainer = new GameObject("MedalContainer");
            medalContainer.transform.SetParent(backdrop.transform, false);
            var mcRect = medalContainer.AddComponent<RectTransform>();
            mcRect.anchorMin = mcRect.anchorMax = new Vector2(0.5f, 0.86f);
            mcRect.sizeDelta = new Vector2(200, 140);
            medalContainer.AddComponent<FunkyPulseEffect>();

            var medalObj = new GameObject("TrophyCup");
            medalObj.transform.SetParent(medalContainer.transform, false);
            var trRect = medalObj.AddComponent<RectTransform>();
            trRect.anchorMin = trRect.anchorMax = new Vector2(0.5f, 0.5f);
            trRect.sizeDelta = new Vector2(130, 130);
            var trImage = medalObj.AddComponent<Image>();
            trImage.sprite = PostMatchVisualFactory.GetTrophySprite();
            trImage.color = placement == 2 ? new Color(0.85f, 0.90f, 1f) : new Color(0.95f, 0.70f, 0.45f);

            var (banner, innerBanner) = UiFactory.CreateBannerPanel(
                backdrop.transform, "RankBanner", new Vector2(0.5f, 0.71f), new Vector2(760, 68),
                new Color(0.25f, 0.55f, 0.95f), new Vector2(0, 0));
            var headline = UiFactory.CreateText(
                innerBanner.transform, "Headline", new Vector2(0.5f, 0.5f), new Vector2(740, 58), 28);
            headline.text = rankTitle;
            headline.fontStyle = FontStyles.Bold;
            headline.color = Color.white;

            var sub = UiFactory.CreateText(
                backdrop.transform, "Subtitle", new Vector2(0.5f, 0.63f), new Vector2(700, 32), 17);
            sub.text = "You reached the goal! Great quiz performance!";
            sub.fontStyle = FontStyles.Bold;
            sub.color = new Color(0.7f, 0.85f, 1f);

            BuildPodium(backdrop.transform, result, new Vector2(0.5f, 0.45f));
            BuildXpCard(backdrop.transform, xp, new Vector2(0.5f, 0.24f), false);
            BuildPlayAgainButton(backdrop.transform, new Vector2(0.5f, 0.09f));
        }

        private void AddSparkle(Transform parent, Vector2 pos, float size, float phaseOffset)
        {
            var spObj = new GameObject("Sparkle");
            spObj.transform.SetParent(parent, false);
            var rect = spObj.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(size, size);
            var img = spObj.AddComponent<Image>();
            img.sprite = PostMatchVisualFactory.GetSparkleSprite();
            img.color = new Color(1f, 0.95f, 0.6f);
            var pulse = spObj.AddComponent<FunkySparkleAnimator>();
            pulse.phase = phaseOffset;
        }

        private void BuildPodium(Transform parent, MatchEndPayload result, Vector2 anchor)
        {
            if (result?.Standings == null || result.Standings.Count == 0) return;

            var (placard, inner) = UiFactory.CreatePlacardPanel(
                parent, "PodiumPanel", anchor, new Vector2(780, 115),
                new Color(0.06f, 0.08f, 0.14f, 0.92f));

            var standings = result.Standings.Take(4).ToList();
            float itemWidth = 740f / Mathf.Max(1, standings.Count);

            for (int i = 0; i < standings.Count; i++)
            {
                var s = standings[i];
                float xOffset = -370f + (i + 0.5f) * itemWidth;

                var entryObj = new GameObject($"Podium_{i}");
                entryObj.transform.SetParent(inner.transform, false);
                var entryRect = entryObj.AddComponent<RectTransform>();
                entryRect.anchorMin = entryRect.anchorMax = new Vector2(0.5f, 0.5f);
                entryRect.anchoredPosition = new Vector2(xOffset, 0);
                entryRect.sizeDelta = new Vector2(itemWidth - 10f, 95f);

                var entryText = entryObj.AddComponent<TextMeshProUGUI>();
                string badge = s.Placement == 1 ? "<color=#FFD700>[ 1ST ]</color>" :
                               s.Placement == 2 ? "<color=#C0C0C0>[ 2ND ]</color>" :
                               s.Placement == 3 ? "<color=#CD7F32>[ 3RD ]</color>" :
                               $"<color=#888888>[ #{s.Placement} ]</color>";

                string name = string.IsNullOrEmpty(s.Name) ? $"Player {s.PlayerId}" : s.Name;
                string status = s.GoalReached ? "Finished" : s.TimedOut ? "Time's Up" : $"{s.Hp} HP";

                entryText.text = $"<b>{badge}</b>\n<size=16>{name}</size>\n<size=13><color=#aaaaaa>{status}</color></size>";
                entryText.fontSize = 17;
                entryText.alignment = TextAlignmentOptions.Center;
                entryText.color = Color.white;
            }
        }

        private void BuildXpCard(Transform parent, XpAwardPayload xp, Vector2 anchor, bool goldTheme)
        {
            if (xp == null) return;

            Color bgColor = goldTheme ? new Color(0.35f, 0.25f, 0.05f, 0.95f) : new Color(0.08f, 0.15f, 0.25f, 0.95f);
            var (placard, inner) = UiFactory.CreatePlacardPanel(
                parent, "XpCard", anchor, new Vector2(560, 80), bgColor);

            var text = UiFactory.CreateText(
                inner.transform, "XpText", new Vector2(0.5f, 0.5f), new Vector2(540, 70), 18);
            text.fontStyle = FontStyles.Bold;
            text.color = goldTheme ? new Color(1f, 0.9f, 0.4f) : Color.white;
            text.text = $"+{xp.XpGained} XP EARNED  |  TOTAL: {xp.NewTotalXp} XP";

            if (xp.NewUnlocks != null && xp.NewUnlocks.Count > 0)
            {
                var defs = CharacterCatalogLoader.LoadAll();
                var names = xp.NewUnlocks
                    .Select(id => defs.FirstOrDefault(d => d.characterId == id)?.displayName ?? id)
                    .ToList();
                text.text += $"\n<size=14><color=#00FFB0>UNLOCKED: {string.Join(", ", names).ToUpper()}!</color></size>";
            }
        }

        private void BuildPlayAgainButton(Transform parent, Vector2 anchor)
        {
            var (btn, label) = UiFactory.CreateClashButton(
                parent, "PlayAgainBtn", anchor, new Vector2(280, 50),
                "PLAY AGAIN",
                new Color(0.18f, 0.68f, 0.28f),
                new Color(0.10f, 0.44f, 0.18f),
                ">>");
            label.fontSize = 18;
            btn.onClick.AddListener(() => SceneManager.LoadScene("NameEntry"));
        }
    }

    /// Generates high-resolution procedural visual textures for the Trophy, Sunburst, Sparkles, and Alarm Clock.
    public static class PostMatchVisualFactory
    {
        private static Sprite _trophySprite;
        private static Sprite _sunburstSprite;
        private static Sprite _sparkleSprite;
        private static Sprite _alarmClockSprite;

        public static Sprite GetTrophySprite()
        {
            if (_trophySprite != null) return _trophySprite;
            const int w = 128, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            Color goldBright = new Color(1f, 0.92f, 0.45f);
            Color goldMid = new Color(0.96f, 0.72f, 0.12f);
            Color goldShadow = new Color(0.65f, 0.42f, 0.05f);
            Color baseDark = new Color(0.22f, 0.16f, 0.08f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float cx = x - 64f;
                    float nx = cx / 64f;

                    // 1. Pedestal Base (Y: 10 to 30)
                    if (y >= 10 && y <= 24 && Mathf.Abs(cx) <= 38f)
                    {
                        float shade = (cx + 38f) / 76f;
                        pixels[y * w + x] = Color.Lerp(baseDark, goldMid, shade);
                        continue;
                    }
                    if (y >= 24 && y <= 32 && Mathf.Abs(cx) <= 28f)
                    {
                        pixels[y * w + x] = goldBright;
                        continue;
                    }

                    // 2. Stem Pillar (Y: 32 to 55)
                    if (y >= 32 && y <= 55)
                    {
                        float stemW = 9f + 6f * Mathf.Cos((y - 32f) / 23f * Mathf.PI);
                        if (Mathf.Abs(cx) <= stemW)
                        {
                            float shade = (cx + stemW) / (2f * stemW);
                            pixels[y * w + x] = Color.Lerp(goldShadow, goldBright, shade);
                            continue;
                        }
                    }

                    // 3. Trophy Cup Chalice (Y: 55 to 112)
                    if (y >= 55 && y <= 112)
                    {
                        float cupW = 14f + 32f * Mathf.Sqrt((y - 55f) / 57f);
                        if (Mathf.Abs(cx) <= cupW)
                        {
                            float shade = (cx + cupW) / (2f * cupW);
                            Color c = Color.Lerp(goldShadow, goldBright, shade);
                            if (cx < -4f && cx > -16f) c = Color.Lerp(c, Color.white, 0.45f); // highlight
                            pixels[y * w + x] = c;
                            continue;
                        }
                    }

                    // 4. Handles (Left and Right curved loops: Y 68 to 104, X 34 to 58)
                    float hx = Mathf.Abs(cx);
                    if (y >= 68 && y <= 104 && hx >= 32f && hx <= 56f)
                    {
                        float ringDist = Vector2.Distance(new Vector2(hx, y), new Vector2(42f, 86f));
                        if (ringDist >= 10f && ringDist <= 18f)
                        {
                            pixels[y * w + x] = goldMid;
                            continue;
                        }
                    }

                    // 5. Star Emblem in Center (Y: 76 to 92, X: -10 to 10)
                    if (y >= 76 && y <= 92 && Mathf.Abs(cx) <= 8f)
                    {
                        if (Mathf.Abs(cx) + Mathf.Abs(y - 84f) <= 7f)
                        {
                            pixels[y * w + x] = Color.white;
                            continue;
                        }
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            _trophySprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            return _trophySprite;
        }

        public static Sprite GetSunburstSprite()
        {
            if (_sunburstSprite != null) return _sunburstSprite;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - 64f;
                    float dy = y - 64f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > 63f)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }
                    float angle = Mathf.Atan2(dy, dx);
                    float rays = Mathf.Max(0f, Mathf.Cos(angle * 10f));
                    float falloff = Mathf.Clamp01((63f - dist) / 63f);
                    float alpha = rays * falloff * 0.75f;
                    pixels[y * size + x] = new Color(1f, 0.85f, 0.25f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            _sunburstSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _sunburstSprite;
        }

        public static Sprite GetSparkleSprite()
        {
            if (_sparkleSprite != null) return _sparkleSprite;
            const int size = 48;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - 24f) / 24f;
                    float dy = Mathf.Abs(y - 24f) / 24f;
                    float diamond = Mathf.Sqrt(dx) + Mathf.Sqrt(dy);
                    if (diamond <= 1f)
                    {
                        float intensity = 1f - diamond;
                        pixels[y * size + x] = new Color(1f, 0.95f, 0.6f, intensity);
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            _sparkleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _sparkleSprite;
        }

        public static Sprite GetAlarmClockSprite()
        {
            if (_alarmClockSprite != null) return _alarmClockSprite;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            Color bodyRed = new Color(0.90f, 0.22f, 0.22f);
            Color bellSilver = new Color(0.85f, 0.88f, 0.92f);
            Color faceWhite = new Color(0.96f, 0.96f, 0.96f);
            Color handBlack = new Color(0.12f, 0.12f, 0.15f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = x - 64f;
                    float cy = y - 56f;
                    float dist = Mathf.Sqrt(cx * cx + cy * cy);

                    // 1. Left & Right Bells (Y: 90 to 118, X: 35 and 93)
                    float bellLeft = Vector2.Distance(new Vector2(x, y), new Vector2(36f, 98f));
                    float bellRight = Vector2.Distance(new Vector2(x, y), new Vector2(92f, 98f));
                    if (bellLeft <= 16f || bellRight <= 16f)
                    {
                        pixels[y * size + x] = bellSilver;
                        continue;
                    }

                    // 2. Legs (Y: 10 to 24, X: 36 and 92)
                    if (y >= 10 && y <= 24 && (Mathf.Abs(x - 36) <= 5 || Mathf.Abs(x - 92) <= 5))
                    {
                        pixels[y * size + x] = bellSilver;
                        continue;
                    }

                    // 3. Main Clock Body Outer Casing (Radius 42)
                    if (dist <= 44f)
                    {
                        if (dist >= 36f)
                        {
                            pixels[y * size + x] = bodyRed;
                        }
                        else
                        {
                            // 4. White Clock Face (Radius 36)
                            if (dist <= 36f)
                            {
                                // Hands at 10:10
                                bool onHourHand = (cx <= 0 && cy >= 0 && Mathf.Abs(cx + cy * 0.7f) <= 2.5f && dist <= 22f);
                                bool onMinHand = (cx >= 0 && cy >= 0 && Mathf.Abs(cx - cy * 0.8f) <= 2.5f && dist <= 28f);
                                if (onHourHand || onMinHand || dist <= 4f)
                                {
                                    pixels[y * size + x] = handBlack;
                                }
                                else
                                {
                                    pixels[y * size + x] = faceWhite;
                                }
                            }
                        }
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            _alarmClockSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _alarmClockSprite;
        }
    }

    /// Animates pulsating background color on disco victory
    public class FunkyDiscoAnimator : MonoBehaviour
    {
        private Image _bg;
        private void Awake()
        {
            _bg = GetComponent<Image>();
        }
        private void Update()
        {
            if (_bg != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.5f);
                _bg.color = Color.Lerp(
                    new Color(0.12f, 0.05f, 0.22f, 0.96f),
                    new Color(0.18f, 0.04f, 0.28f, 0.96f),
                    pulse
                );
            }
        }
    }

    public class FunkyPulseEffect : MonoBehaviour
    {
        private void Update()
        {
            float scale = 1f + 0.06f * Mathf.Sin(Time.time * 3.5f);
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    public class FunkySpinEffect : MonoBehaviour
    {
        private void Update()
        {
            transform.Rotate(0, 0, -25f * Time.deltaTime);
        }
    }

    public class FunkySparkleAnimator : MonoBehaviour
    {
        public float phase = 0f;
        private void Update()
        {
            float scale = 0.85f + 0.35f * Mathf.Sin(Time.time * 4.5f + phase);
            transform.localScale = new Vector3(scale, scale, 1f);
            transform.Rotate(0, 0, 30f * Time.deltaTime);
        }
    }

    public class FunkyWiggleEffect : MonoBehaviour
    {
        private void Update()
        {
            float rot = Mathf.Sin(Time.time * 12f) * 10f;
            transform.localEulerAngles = new Vector3(0, 0, rot);
            float scale = 1f + 0.06f * Mathf.Cos(Time.time * 6f);
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
