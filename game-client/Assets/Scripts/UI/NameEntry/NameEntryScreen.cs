using System;
using System.Threading.Tasks;
using QuizBattle.Arena;
using QuizBattle.Bootstrap;
using QuizBattle.Networking;
using QuizBattle.Networking.Protocol;
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
        private bool _joining;
        private bool _stopped;
        private Action _unsubscribeJoin;

        private void Start()
        {
            SessionManager.AutoDetectEndpoint();
            Build();
            var codes = JoinUrlPrefill.Parse(Application.absoluteURL);
            _classCodeField.text = codes.classCode;
            _matchCodeField.text = codes.joinCode;
        }

        private void OnDisable()
        {
            _stopped = true;
            _unsubscribeJoin?.Invoke();
        }

        private void OnDestroy() => OnDisable();

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
            var subText = UiFactory.CreateText(innerCard.transform, "SubText", new Vector2(0.5f, 0.88f), new Vector2(420, 48), 14);
            subText.text = "Returning? Use your existing nickname and PIN.\nNew nickname? Choose a PIN (up to 4 digits).";
            subText.color = QuizBattlePalette.CreamText;

            _classCodeField = UiFactory.CreateInputField(innerCard.transform, "ClassCodeField", new Vector2(0.5f, 0.74f), new Vector2(360, 46), "Class code (e.g. MATH101)");
            _nameField = UiFactory.CreateInputField(innerCard.transform, "NameField", new Vector2(0.5f, 0.60f), new Vector2(360, 46), "Your nickname (e.g. Alex)");
            _pinField = UiFactory.CreateInputField(innerCard.transform, "PinField", new Vector2(0.5f, 0.46f), new Vector2(360, 46), "PIN (existing, or new on first use)");
            _pinField.contentType = TMP_InputField.ContentType.IntegerNumber;
            _pinField.inputType = TMP_InputField.InputType.Password;
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
            _statusText.richText = false;
        }

        public void OnJoinClicked()
        {
            if (_stopped || _joining) return;
            _ = Join(_classCodeField.text.Trim(), _nameField.text.Trim(), _pinField.text.Trim(), _matchCodeField.text.Trim());
        }

        private static string ValidateDetails(string classCode, string name, string pin, string matchCode)
        {
            if (string.IsNullOrWhiteSpace(classCode) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(matchCode))
                return "Please fill in all fields.";
            if (string.IsNullOrWhiteSpace(pin))
                return "Enter your existing PIN, or choose one for a new nickname.";
            return null;
        }

        public async Task Join(string classCode, string name, string pin, string matchCode)
        {
            if (_stopped || _joining) return;
            var validationError = ValidateDetails(classCode, name, pin, matchCode);
            if (validationError != null)
            {
                _statusText.text = validationError;
                return;
            }

            _joining = true;
            _joinButton.interactable = false;
            _statusText.text = "Connecting & signing in...";

            SessionManager.AutoDetectEndpoint();
            var client = AppRoot.Instance.Client;
            var store = AppRoot.Instance.Store;
            bool acked = false;
            bool joiningLobby = false;
            LobbyStatePayload snapshot = null;
            string error = null;
            void OnMessage(Envelope env)
            {
                if (_stopped) return;
                if (env.Type == "error") error = env.Payload?["code"]?.ToString() == "not_found"
                                                     ? "Match not found. Check the match code from your teacher."
                                                     : env.Payload?["message"]?.ToString() ?? "Server rejected the request. Please try again.";
                if (env.Type == "hello_ack" && !acked)
                {
                    SessionManager.PlayerId = env.Payload["playerId"].ToObject<int>();
                    acked = true;
                }
            }
            void OnLobby(LobbyStatePayload payload)
            {
                if (!_stopped && joiningLobby)
                    snapshot = payload.Players.Exists(p => p.PlayerId == SessionManager.PlayerId) ? payload : null;
            }
            void OnError(string message) => error = $"Connection problem: {message}";
            void OnDisconnected(string reason) => error = "Connection lost. Please try joining again.";
            Action unsubscribe = () =>
            {
                client.MessageReceived -= OnMessage;
                client.Error -= OnError;
                client.Disconnected -= OnDisconnected;
                store.LobbyUpdated -= OnLobby;
            };
            _unsubscribeJoin = unsubscribe;
            try
            {
                if (client.IsConnected) await client.Close();
                if (_stopped) return;
                await client.Connect(SessionManager.WsUrl);
                if (_stopped) return;

                var login = await AuthClient.StudentLogin(classCode.Trim(), name.Trim(), pin);
                if (_stopped) return;
                if (!client.IsConnected) throw new Exception("Connection lost during sign-in. Please try again.");

                SessionManager.AuthToken = login.Token;
                SessionManager.StudentName = login.Name;
                SessionManager.Role = "student";
                SessionManager.JoinCode = matchCode.Trim();
                SessionManager.SelectedCharacterId = null;

                client.MessageReceived += OnMessage;
                client.Error += OnError;
                client.Disconnected += OnDisconnected;
                store.LobbyUpdated += OnLobby;
                client.Send("hello", new { role = "student", token = login.Token });
                await WsClient.WaitUntil(client, () => _stopped || error != null || acked || !client.IsConnected, 5000);
                if (_stopped) return;
                if (error != null || !acked || !client.IsConnected)
                    throw new Exception(error ?? "Sign-in confirmation timed out. Please try again.");

                _statusText.text = "Joining match lobby...";
                joiningLobby = true;
                client.Send("join_lobby", new { joinCode = SessionManager.JoinCode, name = login.Name });
                await WsClient.WaitUntil(client, () => _stopped || error != null || snapshot != null || !client.IsConnected, 5000);
                if (_stopped) return;
                if (error != null || snapshot == null || !client.IsConnected)
                    throw new Exception(error ?? "No lobby confirmation. Check the match code and try again.");

                SessionManager.MatchId = snapshot.MatchId;
                _stopped = true;
                unsubscribe();
                SceneManager.LoadScene("CharacterSelect");
            }
            catch (Exception e)
            {
                if (!_stopped) _statusText.text = e is TimeoutException
                                                      ? "Server did not respond. Check your connection and try again."
                                                      : e.Message;
            }
            finally
            {
                unsubscribe();
                _unsubscribeJoin = null;
                _joining = false;
                if (!_stopped) _joinButton.interactable = true;
            }
        }
    }
}
