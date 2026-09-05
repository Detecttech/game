using System;
using System.Collections.Generic;
using System.Text;
using QuizBattle.Arena;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QuizBattle.UI.HUD
{
    /// Question/streak/log HUD, built entirely from code (via UiFactory) so no
    /// Editor-authored prefab/scene wiring is required. Choice buttons are real,
    /// clickable Buttons — ArenaController subscribes to ChoiceSelected to submit answers.
    public class HudController : MonoBehaviour
    {
        private TMP_Text _roundText;
        private TMP_Text _questionText;
        private Button[] _choiceButtons;
        private TMP_Text[] _choiceLabels;
        private ScrollRect _questionScroll;
        private ScrollRect[] _choiceScrolls;
        private TMP_Text _logText;
        private readonly List<string> _logLines = new List<string>();

        public event Action<int> ChoiceSelected;

        public struct ArenaLayout
        {
            public Rect Board;
            public Rect Question;
            public Rect Choices;
            public Rect Countdown;
            public Rect Waiting;
            public float Scale;
            public bool SideHud;
            public bool CompactChoices;

            public Rect Choice(int index)
            {
                int columns = SideHud && !CompactChoices ? 1 : 2;
                int rows = SideHud && !CompactChoices ? 4 : 2;
                float gap = 8f * Scale;
                float width = (Choices.width - (columns - 1) * gap) / columns;
                float height = (Choices.height - (rows - 1) * gap) / rows;
                return new Rect(Choices.x + index % columns * (width + gap),
                                Choices.yMax - (index / columns + 1) * height - index / columns * gap, width, height);
            }
        }

        private ArenaLayout _layout;
        private Rect _layoutPixels;
        private Rect _layoutSafeArea;
        private float _layoutCanvasScale;
        private Camera _layoutCamera;
        private RenderMode _layoutRenderMode;
        private int _layoutState = -1;
        public Rect BoardPixelRect => _layout.Board;

        public static Rect GetCameraPixelRect(Camera camera)
        {
            if (camera == null) return new Rect(0f, 0f, Screen.width, Screen.height);
            if (camera.targetTexture == null) return camera.pixelRect;
            return new Rect(0f, 0f, camera.targetTexture.width, camera.targetTexture.height);
        }

        public static Rect NormalizeViewport(Rect board, Rect pixels)
        {
            if (pixels.width <= 0f || pixels.height <= 0f) return Rect.zero;
            return new Rect((board.x - pixels.x) / pixels.width, (board.y - pixels.y) / pixels.height,
                            board.width / pixels.width, board.height / pixels.height);
        }

        public static ArenaLayout CalculateLayout(Rect pixels, Rect safeArea, bool question, bool countdown, bool waiting)
        {
            var safe = Rect.MinMaxRect(Mathf.Max(pixels.xMin, safeArea.xMin), Mathf.Max(pixels.yMin, safeArea.yMin),
                                       Mathf.Min(pixels.xMax, safeArea.xMax), Mathf.Min(pixels.yMax, safeArea.yMax));
            if (safe.width <= 0f || safe.height <= 0f) return new ArenaLayout();
            float scale = Mathf.Clamp(safe.height / 720f, 1f, 1.5f);
            if (safe.width < safe.height) scale = Mathf.Clamp(safe.width / 390f, 1f, 1.5f);
            float gap = 8f * scale;
            var content = Rect.MinMaxRect(safe.xMin + gap, safe.yMin + gap, safe.xMax - gap, safe.yMax - gap);
            if (content.width <= 0f || content.height <= 0f) return new ArenaLayout();
            bool side = question && safe.width / safe.height >= 1.6f && content.width >= 480f * scale
                        && content.height >= 220f * scale;
            bool compact = side && content.height < 300f * scale;
            var layout = new ArenaLayout { Board = content, Scale = scale, SideHud = side, CompactChoices = compact };
            float hudWidth = side ? Mathf.Max((compact ? 304f : 240f) * scale, safe.width * 0.32f) - 2f * gap : content.width;
            float top = content.yMax;
            if (question)
            {
                top -= 12f * scale;
                float questionHeight = side ? Mathf.Clamp(content.height - (compact ? 148f : 252f) * scale, 48f * scale, 112f * scale) : 72f * scale;
                layout.Question = new Rect(content.x, top - questionHeight, hudWidth, questionHeight);
                top = layout.Question.yMin - gap;
                float choicesHeight = (side && !compact ? 232f : 120f) * scale;
                layout.Choices = new Rect(content.x, top - choicesHeight, hudWidth, choicesHeight);
                top = layout.Choices.yMin - gap;
            }
            float statusX = content.x;
            if (side)
            {
                layout.Board.xMin = content.x + hudWidth + 2f * gap;
                statusX = layout.Board.xMin;
                hudWidth = layout.Board.width;
                top = content.yMax;
            }
            if (countdown)
            {
                layout.Countdown = new Rect(statusX, top - 44f * scale, hudWidth, 44f * scale);
                top = layout.Countdown.yMin - gap;
            }
            if (waiting)
            {
                layout.Waiting = new Rect(statusX, top - 44f * scale, hudWidth, 44f * scale);
                top = layout.Waiting.yMin - gap;
            }
            if ((!side && question) || countdown || waiting) layout.Board.yMax = top;
            if (layout.Board.width <= 0f || layout.Board.height <= 0f) layout.Board = Rect.zero;
            return layout;
        }

        public Rect GetBoardViewport(Camera camera)
        {
            var pixels = GetCameraPixelRect(camera);
            var canvas = GetComponent<Canvas>();
            bool rendersHud = isActiveAndEnabled && canvas != null && canvas.isActiveAndEnabled &&
                              ((canvas.renderMode == RenderMode.ScreenSpaceOverlay && (camera == null || camera.targetTexture == null)) ||
                               (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == camera));
            if (!rendersHud)
            {
                var safe = camera != null && camera.targetTexture != null ? pixels : Screen.safeArea;
                return NormalizeViewport(CalculateLayout(pixels, safe, false, false, false).Board, pixels);
            }
            RefreshLayout(camera);
            return NormalizeViewport(BoardPixelRect, pixels);
        }

        private void RefreshLayout(Camera camera)
        {
            if (_questionPlacard == null) return;
            var canvas = GetComponent<Canvas>();
            if (canvas == null || canvas.scaleFactor <= 0f) return;
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera) camera = canvas.worldCamera;
            else if (camera != null && camera.targetTexture != null) camera = null;
            var pixels = GetCameraPixelRect(camera);
            var safe = camera != null && camera.targetTexture != null ? pixels : Screen.safeArea;
            int state = (_questionPlacard.activeSelf ? 1 : 0) | (_timerBanner.activeSelf ? 2 : 0)
                        | (_finishedWaitingBanner.activeSelf ? 4 : 0);
            if (_layoutPixels == pixels && _layoutSafeArea == safe && _layoutState == state && _layoutCanvasScale == canvas.scaleFactor
                    && _layoutCamera == camera && _layoutRenderMode == canvas.renderMode) return;
            _layoutPixels = pixels;
            _layoutSafeArea = safe;
            _layoutState = state;
            _layoutCanvasScale = canvas.scaleFactor;
            _layoutCamera = camera;
            _layoutRenderMode = canvas.renderMode;
            _layout = CalculateLayout(pixels, safe, (state & 1) != 0, (state & 2) != 0, (state & 4) != 0);
            Place((RectTransform)_questionPlacard.transform, _layout.Question, canvas);
            for (int i = 0; i < _choiceButtons.Length; i++)
                Place((RectTransform)_choiceButtons[i].transform, _layout.Choice(i), canvas);
            Place((RectTransform)_timerBanner.transform, _layout.Countdown, canvas);
            Place((RectTransform)_finishedWaitingBanner.transform, _layout.Waiting, canvas);
        }

        private void Place(RectTransform rect, Rect pixels, Canvas canvas)
        {
            if (pixels.width <= 0f || pixels.height <= 0f || _layout.Scale <= 0f) return;
            var canvasPixels = GetCameraPixelRect(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera);
            if (canvasPixels.width <= 0f || canvasPixels.height <= 0f) return;
            rect.anchorMin = rect.anchorMax = new Vector2((pixels.center.x - canvasPixels.x) / canvasPixels.width,
                    (pixels.center.y - canvasPixels.y) / canvasPixels.height);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = pixels.size / _layout.Scale;
            rect.localScale = Vector3.one * (_layout.Scale / canvas.scaleFactor);
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }

        private static ScrollRect CreateScrollableText(TMP_Text text, Vector2 min, Vector2 max, Button owner = null)
        {
            var host = QuizBattle.UI.UiFactory.CreateRect(text.transform.parent, text.name + "Scroll", Vector2.zero, Vector2.zero);
            Stretch(host, min, max);
            host.gameObject.AddComponent<Image>().color = Color.clear;
            var scroll = (owner != null ? owner.gameObject : host.gameObject).AddComponent<HudTextScroll>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.inertia = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            var viewport = QuizBattle.UI.UiFactory.CreateRect(host, "Viewport", Vector2.zero, Vector2.zero);
            Stretch(viewport, Vector2.zero, new Vector2(-14f, 0f));
            viewport.gameObject.AddComponent<RectMask2D>();
            text.transform.SetParent(viewport, false);
            text.rectTransform.anchorMin = new Vector2(0f, 1f);
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.pivot = new Vector2(0.5f, 1f);
            text.rectTransform.sizeDelta = Vector2.zero;
            text.rectTransform.anchoredPosition = Vector2.zero;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableAutoSizing = false;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = text.rectTransform;

            var track = QuizBattle.UI.UiFactory.CreatePanel(host, "ScrollTrack", Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.16f));
            track.rectTransform.anchorMin = new Vector2(1f, 0f);
            track.rectTransform.anchorMax = Vector2.one;
            track.rectTransform.offsetMin = new Vector2(-10f, 0f);
            track.rectTransform.offsetMax = Vector2.zero;
            var handle = QuizBattle.UI.UiFactory.CreatePanel(track.transform, "Handle", Vector2.zero, Vector2.zero, QuizBattlePalette.GoldTrim);
            Stretch(handle.rectTransform, Vector2.zero, Vector2.zero);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }

        private static void ResetScroll(ScrollRect scroll)
        {
            scroll.StopMovement();
            scroll.content.anchoredPosition = Vector2.zero;
        }

        public static HudController Create()
        {
            var canvas = QuizBattle.UI.UiFactory.CreateCanvas("HUD_Canvas");
            var hud = canvas.gameObject.AddComponent<HudController>();
            hud.Build(canvas.transform);
            return hud;
        }

        private void Build(Transform parent)
        {
            // Question Placard Banner at the top
            var (placard, innerCard) = QuizBattle.UI.UiFactory.CreatePlacardPanel(
                                           parent, "QuestionPlacard", new Vector2(0.5f, 0.938f), new Vector2(860, 56), QuizBattlePalette.PanelDeep);
            _questionPlacard = placard.gameObject;
            Stretch((RectTransform)placard.Find("PlacardShadow"), new Vector2(-2, -7), new Vector2(2, -3));

            // Question Sequence Ribbon Badge
            var (badge, _) = QuizBattle.UI.UiFactory.CreateBannerPanel(
                                 placard, "QuestionBadge", new Vector2(0.5f, 1f), new Vector2(170, 24), QuizBattlePalette.BannerBlue, new Vector2(0, 0));
            _roundText = QuizBattle.UI.UiFactory.CreateText(badge, "RoundText", new Vector2(0.5f, 0.5f), new Vector2(160, 22), 13);
            _roundText.fontStyle = FontStyles.Bold;
            _roundText.color = QuizBattlePalette.GoldTrim;

            // Question Text inside the placard
            _questionText = QuizBattle.UI.UiFactory.CreateText(
                                innerCard.transform, "QuestionText", new Vector2(0.5f, 0.5f), new Vector2(830, 44), 16);
            _questionText.fontStyle = FontStyles.Bold;
            _questionText.color = Color.white;
            _questionText.outlineWidth = 0.18f;
            _questionText.outlineColor = Color.black;
            _questionScroll = CreateScrollableText(_questionText, new Vector2(8, 4), new Vector2(-8, -4));

            _choiceButtons = new Button[4];
            _choiceLabels = new TMP_Text[4];
            _choiceScrolls = new ScrollRect[4];
            (float xOffset, float yAnchor)[] gridOffsets =
            {
                (-220f, 0.840f), (+220f, 0.840f),
                (-220f, 0.745f), (+220f, 0.745f)
            };
            string[] badges = { "A", "B", "C", "D" };
            Color[] colors =
            {
                new Color(0.16f, 0.44f, 0.88f), // Royal Blue
                new Color(0.18f, 0.68f, 0.28f), // Emerald Green
                new Color(0.88f, 0.56f, 0.12f), // Amber Gold
                new Color(0.82f, 0.22f, 0.22f), // Crimson
            };
            Color[] shadows =
            {
                new Color(0.10f, 0.28f, 0.60f),
                new Color(0.10f, 0.44f, 0.18f),
                new Color(0.58f, 0.35f, 0.08f),
                new Color(0.52f, 0.12f, 0.12f),
            };

            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var (xOffset, yAnchor) = gridOffsets[i];
                var (button, label) = QuizBattle.UI.UiFactory.CreateClashButton(
                                          parent, $"Choice_{i}", new Vector2(0.5f, yAnchor), new Vector2(420, 56), "", colors[i], shadows[i], badges[i], new Vector2(xOffset, 0));
                label.fontSize = 18;
                _choiceScrolls[i] = CreateScrollableText(label, new Vector2(44, 3), new Vector2(-6, -3), button);
                button.onClick.AddListener(() => ChoiceSelected?.Invoke(index));
                _choiceButtons[i] = button;
                _choiceLabels[i] = label;
            }

            _celebrationOverlay = AnswerCelebrationOverlay.Create(parent);
            _suddenOverlay = SuddenQuestionOverlay.Create(parent);

            var (timerPlacard, timerInner) = QuizBattle.UI.UiFactory.CreatePlacardPanel(
                                                 parent, "TimerBanner", new Vector2(0.5f, 0.70f), new Vector2(500, 38), new Color(0.9f, 0.2f, 0.2f, 0.9f));
            _timerBanner = timerPlacard.gameObject;
            Stretch((RectTransform)timerPlacard.Find("PlacardShadow"), new Vector2(-2, -7), new Vector2(2, -3));
            _timerText = QuizBattle.UI.UiFactory.CreateText(
                             timerInner.transform, "TimerText", new Vector2(0.5f, 0.5f), new Vector2(480, 32), 14);
            _timerText.fontStyle = FontStyles.Bold;
            _timerText.color = Color.white;
            Stretch(_timerText.rectTransform, new Vector2(6, 2), new Vector2(-6, -2));
            _timerText.overflowMode = TextOverflowModes.Ellipsis;
            _timerBanner.SetActive(false);

            var (waitPlacard, waitInner) = QuizBattle.UI.UiFactory.CreatePlacardPanel(
                                               parent, "WaitBanner", new Vector2(0.5f, 0.08f), new Vector2(540, 44), QuizBattlePalette.PanelDeep);
            _finishedWaitingBanner = waitPlacard.gameObject;
            Stretch((RectTransform)waitPlacard.Find("PlacardShadow"), new Vector2(-2, -7), new Vector2(2, -3));
            _finishedWaitingText = QuizBattle.UI.UiFactory.CreateText(
                                       waitInner.transform, "WaitText", new Vector2(0.5f, 0.5f), new Vector2(520, 36), 15);
            _finishedWaitingText.fontStyle = FontStyles.Bold;
            _finishedWaitingText.color = QuizBattlePalette.GoldTrim;
            Stretch(_finishedWaitingText.rectTransform, new Vector2(6, 2), new Vector2(-6, -2));
            _finishedWaitingText.overflowMode = TextOverflowModes.Ellipsis;
            _finishedWaitingBanner.SetActive(false);
            RefreshLayout(Camera.main);
        }

        private GameObject _questionPlacard;
        private AnswerCelebrationOverlay _celebrationOverlay;
        private SuddenQuestionOverlay _suddenOverlay;
        private GameObject _timerBanner;
        private TMP_Text _timerText;
        private float _remainingTimerSeconds = 0f;
        private bool _timerActive = false;

        private GameObject _finishedWaitingBanner;
        private TMP_Text _finishedWaitingText;

        private void Update()
        {
            RefreshLayout(Camera.main);
            if (_timerActive && _remainingTimerSeconds > 0f)
            {
                _remainingTimerSeconds -= Time.deltaTime;
                int sec = Mathf.CeilToInt(Mathf.Max(0f, _remainingTimerSeconds));
                if (_timerText != null)
                {
                    _timerText.text = $"1st Place Finished! {sec}s to cross the goal!";
                }
                if (_remainingTimerSeconds <= 0f)
                {
                    _timerActive = false;
                    if (_timerText != null) _timerText.text = "Time's up!";
                }
            }
        }

        public void ShowCountdown(int remainingSeconds, string message)
        {
            _remainingTimerSeconds = remainingSeconds;
            _timerActive = true;
            if (_timerBanner != null)
            {
                _timerBanner.SetActive(true);
                if (_timerText != null) _timerText.text = message;
            }
        }

        public void ShowWaitingFinished(int rank)
        {
            SetChoicesInteractable(false);
            if (_choiceButtons != null)
            {
                foreach (var b in _choiceButtons)
                {
                    if (b != null) b.gameObject.SetActive(false);
                }
            }
            if (_questionPlacard != null) _questionPlacard.SetActive(false);

            if (_finishedWaitingBanner != null)
            {
                _finishedWaitingBanner.SetActive(true);
                if (_finishedWaitingText != null)
                {
                    _finishedWaitingText.text = rank == 1
                                                ? "CHAMPION! 1ST PLACE REACHED! SPECTATING RACE..."
                                                : $"FINISHED #{rank}! SPECTATING RACE...";
                }
            }
        }

        public void SetSpectatorMode(bool isSpectator)
        {
            if (isSpectator)
            {
                SetChoicesInteractable(false);
                if (_choiceButtons != null)
                {
                    foreach (var b in _choiceButtons)
                    {
                        if (b != null) b.gameObject.SetActive(false);
                    }
                }
                if (_questionPlacard != null) _questionPlacard.SetActive(false);

                if (_finishedWaitingBanner != null)
                {
                    _finishedWaitingBanner.SetActive(true);
                    if (_finishedWaitingText != null)
                    {
                        _finishedWaitingText.text = "🎥 TEACHER SPECTATOR MODE — WATCHING LIVE ARENA";
                    }
                }
            }
        }

        public void ShowFeedback(bool correct, int streak, int chosenIndex = -1)
        {
            // Big overlay is suppressed per user request — crisp orange pop-up animation is
            // rendered directly above the character's head in the arena instead!
        }

        public void ShowQuestion(int questionNumber, string text, IReadOnlyList<string> choices, bool isSudden = false, string rewardName = null, int rewardDamage = 0, int timeLimitMs = 20000)
        {
            if (isSudden)
            {
                // Unique full-takeover overlay for students — does NOT alter regular question state!
                if (_suddenOverlay != null)
                {
                    _suddenOverlay.Show(text, choices, rewardName, rewardDamage, timeLimitMs, index => ChoiceSelected?.Invoke(index));
                }
                return;
            }

            // If returning from sudden question, ensure overlay is closed
            if (_suddenOverlay != null && _suddenOverlay.gameObject.activeSelf)
            {
                _suddenOverlay.Hide();
            }

            _roundText.text = $"Question {questionNumber}";
            _roundText.color = Color.white;
            _questionText.text = text;
            ResetScroll(_questionScroll);
            for (int i = 0; i < _choiceLabels.Length; i++)
            {
                _choiceLabels[i].text = i < choices.Count ? choices[i] : "";
                ResetScroll(_choiceScrolls[i]);
                _choiceButtons[i].interactable = i < choices.Count;
            }
        }

        public void ShowAnswerResult(bool correct, string rewardName = null, int rewardDamage = 0)
        {
            if (_suddenOverlay != null && _suddenOverlay.gameObject.activeSelf)
            {
                _suddenOverlay.ShowResult(correct, rewardName, rewardDamage);
            }
        }

        public void SetChoicesInteractable(bool interactable)
        {
            foreach (var b in _choiceButtons) b.interactable = interactable;
        }

        public void Log(string line)
        {
            Debug.Log($"[HUD] {line}");
        }
    }

    public class HudTextScroll : ScrollRect
    {
        public override void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsActive()) return;
            eventData.eligibleForClick = false;
            base.OnBeginDrag(eventData);
        }
    }
}
