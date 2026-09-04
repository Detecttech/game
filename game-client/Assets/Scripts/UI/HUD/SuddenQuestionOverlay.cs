using System;
using System.Collections;
using System.Collections.Generic;
using QuizBattle.Arena;
using QuizBattle.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuizBattle.UI.HUD
{
    /// Unique full-takeover overlay for sudden question events triggered by the teacher.
    /// Provides a high-stakes arena challenge experience with custom gold/crimson styling,
    /// a dedicated countdown timer, and reward callout.
    /// Once resolved (answered or timed out), it closes and is never shown again,
    /// allowing whatever was on the student's screen to continue immediately.
    public class SuddenQuestionOverlay : MonoBehaviour
    {
        private GameObject _panel;
        private TMP_Text _headerText;
        private TMP_Text _rewardText;
        private TMP_Text _questionText;
        private TMP_Text _timerText;
        private Button[] _choiceButtons = new Button[4];
        private TMP_Text[] _choiceLabels = new TMP_Text[4];
        private TMP_Text _feedbackText;

        private float _timeRemaining;
        private bool _active;
        private Action<int> _onChoiceSelected;

        public static SuddenQuestionOverlay Create(Transform parent)
        {
            var go = new GameObject("SuddenQuestionOverlay");
            go.transform.SetParent(parent, false);

            var overlay = go.AddComponent<SuddenQuestionOverlay>();
            overlay.Build(go.transform);
            go.SetActive(false);
            return overlay;
        }

        private void Build(Transform parent)
        {
            // Dark vignette backdrop covering the screen
            var backdrop = UiFactory.CreateRect(parent, "Backdrop", new Vector2(0.5f, 0.5f), new Vector2(1920, 1080));
            var backdropImg = backdrop.gameObject.AddComponent<Image>();
            backdropImg.color = new Color(0.04f, 0.04f, 0.08f, 0.88f);
            backdropImg.raycastTarget = true; // Blocks all clicks to underlying UI while active

            // Main Challenge Modal Card
            var (modalRect, innerCard) = UiFactory.CreatePlacardPanel(
                parent, "ModalCard", new Vector2(0.5f, 0.5f), new Vector2(880, 520), new Color(0.10f, 0.12f, 0.20f));
            _panel = modalRect.gameObject;

            // Header Banner
            var (bannerRect, _) = UiFactory.CreateBannerPanel(
                modalRect, "HeaderBanner", new Vector2(0.5f, 1f), new Vector2(560, 48), new Color(0.85f, 0.20f, 0.20f), new Vector2(0, -6));
            _headerText = UiFactory.CreateText(
                bannerRect, "HeaderText", new Vector2(0.5f, 0.5f), new Vector2(540, 40), 20);
            _headerText.text = "⚡ SUDDEN BATTLE CHALLENGE ⚡";
            _headerText.fontStyle = FontStyles.Bold;
            _headerText.color = QuizBattlePalette.GoldTrim;

            // Reward Tag Callout
            var rewardRect = UiFactory.CreateRect(
                modalRect, "RewardTag", new Vector2(0.5f, 0.86f), new Vector2(620, 32));
            _rewardText = rewardRect.gameObject.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) _rewardText.font = TMP_Settings.defaultFontAsset;
            _rewardText.alignment = TextAlignmentOptions.Center;
            _rewardText.fontSize = 17;
            _rewardText.fontStyle = FontStyles.Bold;
            _rewardText.color = new Color(1f, 0.85f, 0.25f);
            _rewardText.outlineWidth = 0.2f;
            _rewardText.outlineColor = Color.black;

            // Countdown Timer Pill
            var timerPill = UiFactory.CreateRect(
                modalRect, "TimerPill", new Vector2(0.88f, 0.92f), new Vector2(100, 34));
            var timerImg = timerPill.gameObject.AddComponent<Image>();
            timerImg.sprite = UiFactory.RoundedSprite;
            timerImg.type = Image.Type.Sliced;
            timerImg.color = new Color(0.2f, 0.05f, 0.05f, 0.9f);
            _timerText = UiFactory.CreateText(
                timerPill, "TimerText", new Vector2(0.5f, 0.5f), new Vector2(90, 28), 16);
            _timerText.fontStyle = FontStyles.Bold;
            _timerText.color = new Color(1f, 0.35f, 0.35f);

            // Question Text Placard in the center
            var qCard = UiFactory.CreateRect(
                modalRect, "QuestionBox", new Vector2(0.5f, 0.64f), new Vector2(820, 110));
            var qCardImg = qCard.gameObject.AddComponent<Image>();
            qCardImg.sprite = UiFactory.RoundedSprite;
            qCardImg.type = Image.Type.Sliced;
            qCardImg.color = new Color(0.06f, 0.07f, 0.12f, 0.95f);

            _questionText = UiFactory.CreateText(
                qCard, "QuestionText", new Vector2(0.5f, 0.5f), new Vector2(790, 95), 20);
            _questionText.fontStyle = FontStyles.Bold;
            _questionText.color = Color.white;
            _questionText.alignment = TextAlignmentOptions.Center;

            // 4 Big Tactile Clash Buttons (A, B, C, D) in 2x2 grid
            (float xOffset, float yAnchor)[] gridOffsets = {
                (-210f, 0.36f), (+210f, 0.36f),
                (-210f, 0.20f), (+210f, 0.20f)
            };
            string[] badges = { "A", "B", "C", "D" };
            Color[] colors = {
                new Color(0.16f, 0.44f, 0.88f),
                new Color(0.18f, 0.68f, 0.28f),
                new Color(0.88f, 0.56f, 0.12f),
                new Color(0.82f, 0.22f, 0.22f),
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
                var (xOffset, yAnchor) = gridOffsets[i];
                var (button, label) = UiFactory.CreateClashButton(
                    modalRect, $"SuddenChoice_{i}", new Vector2(0.5f, yAnchor), new Vector2(390, 58), "", colors[i], shadows[i], badges[i], new Vector2(xOffset, 0));
                label.fontSize = 18;
                button.onClick.AddListener(() => OnChoiceClicked(index));
                _choiceButtons[i] = button;
                _choiceLabels[i] = label;
            }

            // Feedback / Result banner at the bottom
            var fbRect = UiFactory.CreateRect(
                modalRect, "Feedback", new Vector2(0.5f, 0.06f), new Vector2(700, 36));
            _feedbackText = fbRect.gameObject.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) _feedbackText.font = TMP_Settings.defaultFontAsset;
            _feedbackText.alignment = TextAlignmentOptions.Center;
            _feedbackText.fontSize = 16;
            _feedbackText.fontStyle = FontStyles.Bold;
            _feedbackText.color = Color.white;
            _feedbackText.text = "";
        }

        public void Show(string questionText, IReadOnlyList<string> choices, string rewardName, int rewardDamage, int timeLimitMs, Action<int> onChoiceSelected)
        {
            _onChoiceSelected = onChoiceSelected;
            _timeRemaining = Mathf.Max(5f, timeLimitMs / 1000f);
            _active = true;

            var rewardLabel = !string.IsNullOrEmpty(rewardName)
                ? $"🏆 WINNER PRIZE: {rewardName.ToUpper()} ({rewardDamage} DMG)!"
                : $"🏆 WINNER PRIZE: MEGA ATTACK ({rewardDamage} DMG)!";
            _rewardText.text = rewardLabel;
            _questionText.text = questionText;
            _feedbackText.text = "";

            for (int i = 0; i < _choiceButtons.Length; i++)
            {
                _choiceLabels[i].text = i < choices.Count ? choices[i] : "";
                _choiceButtons[i].interactable = i < choices.Count;
            }

            gameObject.SetActive(true);
            AudioManager.Instance.PlayAttack();
        }

        private void Update()
        {
            if (!_active) return;

            _timeRemaining -= Time.deltaTime;
            _timerText.text = $"⏱️ {Mathf.CeilToInt(Mathf.Max(0, _timeRemaining))}s";

            if (_timeRemaining <= 0f)
            {
                _active = false;
                _feedbackText.text = "⏱️ Time's Up!";
                _feedbackText.color = new Color(0.9f, 0.3f, 0.3f);
                StartCoroutine(CloseAfterDelay(1.2f));
            }
        }

        private void OnChoiceClicked(int index)
        {
            if (!_active) return;
            _active = false;

            foreach (var b in _choiceButtons) b.interactable = false;
            _feedbackText.text = "Answer submitted! Checking...";
            _feedbackText.color = QuizBattlePalette.GoldTrim;

            _onChoiceSelected?.Invoke(index);
        }

        public void ShowResult(bool correct, string rewardName, int rewardDamage)
        {
            _active = false;
            if (correct)
            {
                var rw = !string.IsNullOrEmpty(rewardName) ? rewardName : "Mega Strike";
                _feedbackText.text = $"🎉 CORRECT! {rw.ToUpper()} UNLOCKED!";
                _feedbackText.color = new Color(0.2f, 0.95f, 0.4f);
                AudioManager.Instance.PlayVictory();
            }
            else
            {
                _feedbackText.text = "❌ MISSED! Sudden challenge ended.";
                _feedbackText.color = new Color(0.95f, 0.35f, 0.35f);
            }

            StartCoroutine(CloseAfterDelay(1.3f));
        }

        private IEnumerator CloseAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            Hide();
        }

        public void Hide()
        {
            _active = false;
            gameObject.SetActive(false);
        }
    }
}
