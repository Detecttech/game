using System;
using System.Collections.Generic;
using System.Text;
using QuizBattle.Arena;
using TMPro;
using UnityEngine;
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
        private TMP_Text _logText;
        private readonly List<string> _logLines = new List<string>();

        public event Action<int> ChoiceSelected;

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

            // 2x2 grid of 3D-beveled tactile Clash Royale choice buttons (compacted into top 24%)
            _choiceButtons = new Button[4];
            _choiceLabels = new TMP_Text[4];
            (float x, float y)[] gridPositions = { (0.285f, 0.842f), (0.715f, 0.842f), (0.285f, 0.762f), (0.715f, 0.762f) };
            string[] badges = { "A", "B", "C", "D" };
            Color[] colors = {
                new Color(0.16f, 0.44f, 0.88f), // Royal Blue
                new Color(0.18f, 0.68f, 0.28f), // Emerald Green
                new Color(0.88f, 0.56f, 0.12f), // Amber Gold
                new Color(0.82f, 0.22f, 0.22f), // Crimson
            };
            Color[] shadows = {
                new Color(0.10f, 0.28f, 0.60f),
                new Color(0.10f, 0.44f, 0.18f),
                new Color(0.58f, 0.35f, 0.08f),
                new Color(0.52f, 0.12f, 0.12f),
            };

            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var (x, y) = gridPositions[i];
                var (button, label) = QuizBattle.UI.UiFactory.CreateClashButton(
                    parent, $"Choice_{i}", new Vector2(x, y), new Vector2(360, 42), "", colors[i], shadows[i], badges[i]);
                label.fontSize = 16;
                button.onClick.AddListener(() => ChoiceSelected?.Invoke(index));
                _choiceButtons[i] = button;
                _choiceLabels[i] = label;
            }

            _celebrationOverlay = AnswerCelebrationOverlay.Create(parent);

            // Floating countdown banner under the question card
            var (timerPlacard, timerInner) = QuizBattle.UI.UiFactory.CreatePlacardPanel(
                parent, "TimerBanner", new Vector2(0.5f, 0.70f), new Vector2(500, 38), new Color(0.9f, 0.2f, 0.2f, 0.9f));
            _timerBanner = timerPlacard.gameObject;
            _timerText = QuizBattle.UI.UiFactory.CreateText(
                timerInner.transform, "TimerText", new Vector2(0.5f, 0.5f), new Vector2(480, 32), 14);
            _timerText.fontStyle = FontStyles.Bold;
            _timerText.color = Color.white;
            _timerBanner.SetActive(false);

            // Sleek spectator badge pill anchored at the bottom edge so the arena is completely visible
            var (waitPlacard, waitInner) = QuizBattle.UI.UiFactory.CreatePlacardPanel(
                parent, "WaitBanner", new Vector2(0.5f, 0.08f), new Vector2(540, 44), QuizBattlePalette.PanelDeep);
            _finishedWaitingBanner = waitPlacard.gameObject;
            _finishedWaitingText = QuizBattle.UI.UiFactory.CreateText(
                waitInner.transform, "WaitText", new Vector2(0.5f, 0.5f), new Vector2(520, 36), 15);
            _finishedWaitingText.fontStyle = FontStyles.Bold;
            _finishedWaitingText.color = QuizBattlePalette.GoldTrim;
            _finishedWaitingBanner.SetActive(false);
        }

        private GameObject _questionPlacard;
        private AnswerCelebrationOverlay _celebrationOverlay;
        private GameObject _timerBanner;
        private TMP_Text _timerText;
        private float _remainingTimerSeconds = 0f;
        private bool _timerActive = false;

        private GameObject _finishedWaitingBanner;
        private TMP_Text _finishedWaitingText;

        private void Update()
        {
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

        public void ShowFeedback(bool correct, int streak, int chosenIndex = -1)
        {
            // Big overlay is suppressed per user request — crisp orange pop-up animation is
            // rendered directly above the character's head in the arena instead!
        }

        public void ShowQuestion(int questionNumber, string text, IReadOnlyList<string> choices)
        {
            // "Question N" not "Round N" — every player answers independently at their
            // own pace now, so there's no shared match-wide round to label.
            _roundText.text = $"Question {questionNumber}";
            _questionText.text = text;
            for (int i = 0; i < _choiceLabels.Length; i++)
            {
                _choiceLabels[i].text = i < choices.Count ? choices[i] : "";
                _choiceButtons[i].interactable = i < choices.Count;
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
}
