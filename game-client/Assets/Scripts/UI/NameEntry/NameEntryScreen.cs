using System;
using System.Threading.Tasks;
using QuizBattle.Arena;
using QuizBattle.Bootstrap;
using QuizBattle.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QuizBattle.UI.NameEntry
{
    /// The "greeted by a name screen" flow, scoped to a class per the privacy-conscious
    /// identity design: classCode -> a name the student picks themselves (created on
    /// first use, no teacher pre-registration required) -> a PIN the student sets on
    /// first login and confirms on every later one, plus the match code the teacher is
    /// displaying for this session. No email, no third-party auth — see
    /// server/src/http/routes/authRoutes.ts for the matching login endpoint.
    public class NameEntryScreen : MonoBehaviour
    {
        private TMP_InputField _classCodeField;
        private TMP_InputField _nameField;
        private TMP_InputField _pinField;
        private TMP_InputField _matchCodeField;
        private TMP_Text _statusText;
        private Button _joinButton;

        private void Start()
        {
            SessionManager.AutoDetectEndpoint();
            Build();
            _ = PreconnectWs();
        }

        private async Task PreconnectWs()
        {
            var client = AppRoot.Instance.Client;
            if (!client.IsConnected)
            {
                try
                {
                    await client.Connect(SessionManager.WsUrl);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[NameEntryScreen] Background pre-connect: {ex.Message}");
                }
            }
        }

        private void Build()
        {
            var canvas = UiFactory.CreateCanvas("NameEntry_Canvas");

            // Main center placard card
            var (cardRect, innerCard) = UiFactory.CreatePlacardPanel(
                canvas.transform, "CenterCard", new Vector2(0.5f, 0.5f), new Vector2(480, 540), QuizBattlePalette.PanelDeep);

            // Title Ribbon Banner at the top of the card
            var (bannerRect, _) = UiFactory.CreateBannerPanel(
                cardRect, "TitleBanner", new Vector2(0.5f, 1f), new Vector2(340, 48), QuizBattlePalette.BannerBlue, new Vector2(0, 8));
            var titleText = UiFactory.CreateText(bannerRect, "TitleText", new Vector2(0.5f, 0.5f), new Vector2(320, 36), 22);
            titleText.text = "JOIN MATCH";
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = QuizBattlePalette.GoldTrim;

            // Subtitle
            var subText = UiFactory.CreateText(innerCard.transform, "SubText", new Vector2(0.5f, 0.88f), new Vector2(420, 30), 14);
            subText.text = "Enter your details to jump into the arena:";
            subText.color = QuizBattlePalette.CreamText;

            _classCodeField = UiFactory.CreateInputField(innerCard.transform, "ClassCodeField", new Vector2(0.5f, 0.74f), new Vector2(360, 46), "Class code (e.g. MATH101)");
            _nameField = UiFactory.CreateInputField(innerCard.transform, "NameField", new Vector2(0.5f, 0.60f), new Vector2(360, 46), "Your nickname (e.g. Alex)");
            _pinField = UiFactory.CreateInputField(innerCard.transform, "PinField", new Vector2(0.5f, 0.46f), new Vector2(360, 46), "PIN (4 digits)");
            _pinField.contentType = TMP_InputField.ContentType.IntegerNumber;
            _pinField.characterLimit = 4;
            _matchCodeField = UiFactory.CreateInputField(innerCard.transform, "MatchCodeField", new Vector2(0.5f, 0.32f), new Vector2(360, 46), "Match code (e.g. LOBBY1)");

            var (joinBtn, joinLabel) = UiFactory.CreateClashButton(
                innerCard.transform, "JoinButton", new Vector2(0.5f, 0.16f), new Vector2(260, 52), "JOIN GAME >>",
                new Color(0.18f, 0.68f, 0.28f), new Color(0.10f, 0.44f, 0.18f), "");
            joinLabel.fontSize = 20;
            _joinButton = joinBtn;
            _joinButton.onClick.AddListener(OnJoinClicked);

            _statusText = UiFactory.CreateText(innerCard.transform, "Status", new Vector2(0.5f, 0.04f), new Vector2(440, 30), 13);
            _statusText.color = QuizBattlePalette.CreamText;
        }

        public void OnJoinClicked()
        {
            _ = Join(_classCodeField.text.Trim(), _nameField.text.Trim(), _pinField.text.Trim(), _matchCodeField.text.Trim());
        }

        public async Task Join(string classCode, string name, string pin, string matchCode)
        {
            if (string.IsNullOrWhiteSpace(classCode) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(matchCode))
            {
                _statusText.text = "Please fill in all fields.";
                return;
            }

            _joinButton.interactable = false;
            _statusText.text = "Connecting & signing in...";

            SessionManager.AutoDetectEndpoint();
            var client = AppRoot.Instance.Client;

            if (!client.IsConnected)
            {
                try
                {
                    await client.Connect(SessionManager.WsUrl);
                }
                catch (Exception e)
                {
                    _statusText.text = $"Could not reach server: {e.Message}";
                    _joinButton.interactable = true;
                    return;
                }
            }

            AuthClient.StudentLoginResult login;
            try
            {
                login = await AuthClient.StudentLogin(classCode, name, pin);
            }
            catch (Exception e)
            {
                _statusText.text = e.Message;
                _joinButton.interactable = true;
                return;
            }

            SessionManager.AuthToken = login.Token;
            SessionManager.StudentName = login.Name;
            SessionManager.Role = "student";
            SessionManager.JoinCode = matchCode;

            var store = AppRoot.Instance.Store;

            bool acked = false;
            void OnAck(Networking.Protocol.Envelope env)
            {
                if (env.Type == "hello_ack")
                {
                    SessionManager.PlayerId = env.Payload["playerId"].ToObject<int>();
                    acked = true;
                }
            }
            client.MessageReceived += OnAck;
            client.Send("hello", new { role = "student", token = login.Token });
            if (!await WsClient.WaitUntil(client, () => acked, 5000))
            {
                client.MessageReceived -= OnAck;
                _statusText.text = "Lost connection to the server. Please try again.";
                _joinButton.interactable = true;
                return;
            }
            client.MessageReceived -= OnAck;

            _statusText.text = "Joining match lobby...";
            client.Send("join_lobby", new { joinCode = matchCode, name = login.Name });
            bool inLobby = await WsClient.WaitUntil(client, () => store.LobbyPlayers.Exists(p => p.playerId == SessionManager.PlayerId), 5000);

            if (!inLobby)
            {
                _statusText.text = "Could not join that match. Check the match code.";
                _joinButton.interactable = true;
                return;
            }

            SceneManager.LoadScene("CharacterSelect");
        }
    }
}
