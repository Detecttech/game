using System;
using System.Text;
using System.Threading.Tasks;
using QuizBattle.Bootstrap;
using QuizBattle.GameState;
using QuizBattle.Networking;
using QuizBattle.Networking.Protocol;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QuizBattle.UI.Lobby
{
    /// Shows who else has joined and their ready state; the match itself starts only when
    /// the teacher sends teacher_start_match (see ws/handlers/lobbyHandler.ts) — this
    /// screen just reflects lobby_state and reacts to match_start by moving on to Arena.
    /// For team matches, players pick their own team here (Kahoot-style self-assignment)
    /// via select_team — see server/src/matchEngine/LiveMatchRegistry.ts selectTeam.
    public class LobbyScreen : MonoBehaviour
    {
        private TMP_Text _playerListText;
        private TMP_Text _statusText;
        private Button _readyButton;
        private Button _teamAButton;
        private Button _teamBButton;
        private Button _retryButton;
        private Button _backButton;
        private bool _isReady;
        private bool _isTeamMatch;
        private bool _reconnecting;
        private bool _hasLobby;
        private bool _stopped;
        private bool _leaving;
        private bool _closing;
        private bool _connectionClosed;
        private Task<string> _closeTask;
        private bool? _pendingReady;
        private string _pendingTeam;
        private float _pendingDeadline;
        private string _feedback;
        private string _rejoinError;
        private int _matchId;
        private WsClient _client;
        private MatchStateStore _store;
        private Action _unsubscribeRejoin;

        private void Start()
        {
            Build();

            _store = AppRoot.Instance.Store;
            _client = AppRoot.Instance.Client;
            _store.LobbyUpdated += OnLobbyUpdated;
            _store.CharacterLocked += OnCharacterLocked;
            _store.MatchStarted += OnMatchStarted;
            _store.ServerError += OnServerError;
            _client.Disconnected += OnDisconnected;
            _client.Connected += OnConnected;
            _client.Error += OnConnectionError;
            _matchId = _store.MatchId;
            _connectionClosed = !_client.IsConnected;
            _hasLobby = _client.IsConnected && _store.LobbyPlayers.Exists(p => p.playerId == SessionManager.PlayerId);
            RenderPlayers(_store);
            if (!_hasLobby) OnDisconnected("Not connected to lobby");
        }

        private void OnDisable()
        {
            _stopped = true;
            _unsubscribeRejoin?.Invoke();
            if (_store != null)
            {
                _store.LobbyUpdated -= OnLobbyUpdated;
                _store.CharacterLocked -= OnCharacterLocked;
                _store.MatchStarted -= OnMatchStarted;
                _store.ServerError -= OnServerError;
            }
            if (_client != null)
            {
                _client.Disconnected -= OnDisconnected;
                _client.Connected -= OnConnected;
                _client.Error -= OnConnectionError;
            }
        }

        private void OnDestroy() => OnDisable();

        private void Update()
        {
            if (_stopped) return;
            if ((_pendingReady.HasValue || _pendingTeam != null) && Time.realtimeSinceStartup >= _pendingDeadline)
            {
                _pendingReady = null;
                _pendingTeam = null;
                _feedback = "No confirmation received. Check your connection and try again.";
            }
            RefreshControls();
        }

        private void OnDisconnected(string reason)
        {
            if (_stopped) return;
            _connectionClosed = true;
            if (_leaving) return;
            _hasLobby = false;
            _pendingReady = null;
            _pendingTeam = null;
            if (_reconnecting)
            {
                _rejoinError = "Connection lost while rejoining. Retry or return to sign-in.";
                return;
            }
            _ = Rejoin();
        }

        private async Task Rejoin()
        {
            if (_stopped || _leaving || _reconnecting) return;
            _reconnecting = true;
            _hasLobby = false;
            _rejoinError = null;
            _feedback = "Connection lost. Reconnecting...";
            RefreshControls();
            var client = _client;
            var store = _store;
            bool acked = false;
            bool joiningLobby = false;
            LobbyStatePayload snapshot = null;
            void OnAck(Envelope env)
            {
                if (!_stopped && env.Type == "hello_ack" && !acked)
                {
                    SessionManager.PlayerId = env.Payload["playerId"].ToObject<int>();
                    acked = true;
                }
            }
            void OnSnapshot(LobbyStatePayload payload)
            {
                if (!_stopped && joiningLobby && payload.MatchId == _matchId)
                    snapshot = payload.Players.Exists(p => p.PlayerId == SessionManager.PlayerId) ? payload : null;
            }
            Action unsubscribe = () =>
            {
                client.MessageReceived -= OnAck;
                store.LobbyUpdated -= OnSnapshot;
            };
            _unsubscribeRejoin = unsubscribe;
            try
            {
                if (string.IsNullOrEmpty(SessionManager.AuthToken) || string.IsNullOrEmpty(SessionManager.JoinCode))
                    throw new Exception("Session missing. Return to sign-in with your nickname and PIN.");
                if (!client.IsConnected)
                {
                    if (!_connectionClosed)
                        throw new Exception("The previous connection has not closed. Use Back to Sign-In to disconnect first.");
                    _connectionClosed = false;
                    await client.Connect(SessionManager.WsUrl);
                }
                if (_stopped) return;
                if (_rejoinError != null) throw new Exception(_rejoinError);

                client.MessageReceived += OnAck;
                store.LobbyUpdated += OnSnapshot;
                client.Send("hello", new { role = "student", token = SessionManager.AuthToken });
                await WsClient.WaitUntil(client, () => _stopped || _rejoinError != null || acked || !client.IsConnected, 5000);
                if (_stopped) return;
                if (_rejoinError != null || !acked || !client.IsConnected)
                    throw new Exception(_rejoinError ?? "Sign-in confirmation timed out. Retry or return to sign-in.");

                joiningLobby = true;
                client.Send("join_lobby", new { joinCode = SessionManager.JoinCode, name = SessionManager.StudentName });
                await WsClient.WaitUntil(client, () => _stopped || _rejoinError != null || snapshot != null || !client.IsConnected, 5000);
                if (_stopped) return;
                if (_rejoinError != null || snapshot == null || !client.IsConnected)
                    throw new Exception(_rejoinError ?? "No fresh lobby confirmation. Retry or check the match code at sign-in.");

                var mine = snapshot.Players.Find(p => p.PlayerId == SessionManager.PlayerId);
                SessionManager.SelectedCharacterId = mine.CharacterId;
                if (string.IsNullOrEmpty(mine.CharacterId))
                {
                    OnDisable();
                    SceneManager.LoadScene("CharacterSelect");
                    return;
                }
                _hasLobby = true;
                _feedback = null;
            }
            catch (Exception e)
            {
                if (!_stopped) _feedback = e is TimeoutException
                                               ? "Server did not respond. Check your connection, then retry."
                                               : $"Could not rejoin: {e.Message}";
            }
            finally
            {
                unsubscribe();
                _unsubscribeRejoin = null;
                _reconnecting = false;
                if (!_stopped) RenderPlayers(store);
            }
        }

        private void OnConnected()
        {
            if (!_stopped) _connectionClosed = false;
        }

        private async Task BackToSignIn()
        {
            if (_stopped || _reconnecting || _closing) return;
            _leaving = true;
            _closing = true;
            _hasLobby = false;
            _pendingReady = null;
            _pendingTeam = null;
            _feedback = "Leaving lobby. Waiting for disconnection...";
            RefreshControls();
            var client = _client;
            async Task<string> CloseConnection()
            {
                try
                {
                    await client.Close();
                    return null;
                }
                catch (Exception e)
                {
                    return e.Message;
                }
            }
            try
            {
                if (_closeTask == null || _closeTask.IsCompleted)
                    _closeTask = _connectionClosed && !client.IsConnected ? Task.FromResult<string>(null) : CloseConnection();
                var closeTask = _closeTask;
                await WsClient.WaitUntil(client, () => _stopped ||
                                         (closeTask.IsCompleted && (_connectionClosed || closeTask.Result != null)), 5000);
                if (_stopped) return;
                if (!closeTask.IsCompleted)
                    throw new TimeoutException();
                var error = await closeTask;
                if (_stopped) return;
                if (!_connectionClosed || client.IsConnected)
                    throw new Exception(error ?? "Disconnection not confirmed.");

                OnDisable();
                SceneManager.LoadScene("NameEntry");
            }
            catch (Exception)
            {
                if (!_stopped) _feedback = "Could not confirm disconnection. Retry Back to Sign-In, or reload/close the app.";
            }
            finally
            {
                _closing = false;
                if (!_stopped) RefreshControls();
            }
        }

        private void Build()
        {
            var canvas = UiFactory.CreateCanvas();
            var title = UiFactory.CreateText(canvas.transform, "Title", new Vector2(0.5f, 0.88f), new Vector2(700, 60), 32);
            title.text = "Lobby";

            var whoAmI = UiFactory.CreateText(canvas.transform, "WhoAmI", new Vector2(0.5f, 0.95f), new Vector2(500, 30), 16);
            whoAmI.richText = false;
            whoAmI.text = $"Playing as: {SessionManager.StudentName}";

            var panel = UiFactory.CreatePanel(canvas.transform, "PlayerListPanel", new Vector2(0.5f, 0.58f), new Vector2(500, 260), new Color(0, 0, 0, 0.4f));
            _playerListText = UiFactory.CreateText(panel.transform, "PlayerList", new Vector2(0.5f, 0.5f), new Vector2(460, 240), 18);
            _playerListText.alignment = TextAlignmentOptions.TopLeft;
            _playerListText.richText = false;

            _teamAButton = UiFactory.CreateButton(canvas.transform, "TeamAButton", new Vector2(0.4f, 0.36f), new Vector2(160, 45), "Join Team A");
            _teamAButton.onClick.AddListener(() => OnTeamClicked("A"));
            _teamBButton = UiFactory.CreateButton(canvas.transform, "TeamBButton", new Vector2(0.6f, 0.36f), new Vector2(160, 45), "Join Team B");
            _teamBButton.onClick.AddListener(() => OnTeamClicked("B"));
            _teamAButton.gameObject.SetActive(false);
            _teamBButton.gameObject.SetActive(false);

            _readyButton = UiFactory.CreateButton(canvas.transform, "ReadyButton", new Vector2(0.5f, 0.24f), new Vector2(200, 50), "Ready");
            _readyButton.onClick.AddListener(OnReadyClicked);
            _readyButton.interactable = false;

            _retryButton = UiFactory.CreateButton(canvas.transform, "RetryButton", new Vector2(0.5f, 0.36f), new Vector2(200, 45), "Retry Connection");
            _retryButton.onClick.AddListener(() => { _ = Rejoin(); });
            _retryButton.gameObject.SetActive(false);
            _backButton = UiFactory.CreateButton(canvas.transform, "BackButton", new Vector2(0.5f, 0.035f), new Vector2(200, 35), "Back to Sign-In");
            _backButton.onClick.AddListener(() => { _ = BackToSignIn(); });

            _statusText = UiFactory.CreateText(canvas.transform, "Status", new Vector2(0.5f, 0.12f), new Vector2(700, 60), 18);
            _statusText.text = "Waiting for the teacher to start the match...";
            _statusText.richText = false;
        }

        private void OnReadyClicked()
        {
            if (_stopped) return;
            RefreshControls();
            if (!_readyButton.interactable) return;
            _pendingReady = !_isReady;
            _pendingDeadline = Time.realtimeSinceStartup + 5f;
            _feedback = null;
            _client.Send("player_ready", new { ready = _pendingReady.Value });
            RefreshControls();
        }

        private void OnTeamClicked(string team)
        {
            if (_stopped || (team != "A" && team != "B")) return;
            RefreshControls();
            if (!(team == "A" ? _teamAButton : _teamBButton).interactable) return;
            _pendingTeam = team;
            _pendingDeadline = Time.realtimeSinceStartup + 5f;
            _feedback = null;
            _client.Send("select_team", new { team });
            RefreshControls();
        }

        private void OnLobbyUpdated(LobbyStatePayload payload)
        {
            if (_stopped || _leaving || payload.MatchId != _matchId) return;
            var mine = payload.Players.Find(p => p.PlayerId == SessionManager.PlayerId);
            if (mine == null) _hasLobby = false;
            if (mine != null && ((_pendingReady.HasValue && mine.Ready == _pendingReady.Value) ||
                                 (_pendingTeam != null && mine.Team == _pendingTeam)))
            {
                _pendingReady = null;
                _pendingTeam = null;
                _feedback = null;
            }
            if (_hasLobby && !_reconnecting && !_pendingReady.HasValue && _pendingTeam == null) _feedback = null;
            RenderPlayers(_store);
        }

        private void OnCharacterLocked(CharacterLockedPayload payload)
        {
            if (!_stopped && !_leaving) RenderPlayers(_store);
        }

        private void OnServerError(ErrorPayload payload)
        {
            if (_stopped || _leaving) return;
            _pendingReady = null;
            _pendingTeam = null;
            switch (payload.Code)
            {
            case "no_character_selected": _feedback = "Choose a character before getting ready. Return to sign-in to rejoin."; break;
            case "no_team_selected": _feedback = "Choose Team A or Team B, then click Ready."; break;
            case "not_in_lobby": _hasLobby = false; _feedback = "You are no longer in the lobby. Retry to rejoin."; break;
            case "not_found": _feedback = "Match not found. Check the match code at sign-in."; break;
            default: _feedback = payload.Message ?? "The server rejected the request. Please try again."; break;
            }
            if (_reconnecting) _rejoinError = _feedback;
            RefreshControls();
        }

        private void OnConnectionError(string message)
        {
            if (_stopped || _leaving) return;
            _pendingReady = null;
            _pendingTeam = null;
            _hasLobby = false;
            _feedback = $"Connection problem: {message}. Retry or return to sign-in.";
            if (_reconnecting) _rejoinError = _feedback;
            RefreshControls();
        }

        private void RefreshControls()
        {
            var mine = _hasLobby ? _store.LobbyPlayers.Find(p => p.playerId == SessionManager.PlayerId) : null;
            bool connected = _client.IsConnected && !_reconnecting && !_leaving && mine != null;
            bool pending = _pendingReady.HasValue || _pendingTeam != null;
            _isReady = mine?.ready ?? false;
            _isTeamMatch = _store.Mode == "teams";
            bool hasCharacter = !string.IsNullOrEmpty(mine?.characterId);
            bool hasTeam = mine?.team == "A" || mine?.team == "B";
            _readyButton.interactable = connected && !pending && hasCharacter && (!_isTeamMatch || hasTeam);
            _readyButton.GetComponentInChildren<TMP_Text>().text = _pendingReady.HasValue ? "Confirming..." : (_isReady ? "Not Ready" : "Ready");
            _teamAButton.gameObject.SetActive(_isTeamMatch && _hasLobby);
            _teamBButton.gameObject.SetActive(_isTeamMatch && _hasLobby);
            _teamAButton.interactable = connected && !pending && _isTeamMatch && mine.team != "A";
            _teamBButton.interactable = connected && !pending && _isTeamMatch && mine.team != "B";
            _retryButton.gameObject.SetActive(!_hasLobby && !_reconnecting && !_leaving);
            _backButton.interactable = !_reconnecting && !_closing;
            _backButton.GetComponentInChildren<TMP_Text>().text = _reconnecting ? "Wait for reconnect..." : _closing ? "Disconnecting..." : "Back to Sign-In";
            _statusText.text = _feedback ?? (pending ? "Waiting for server confirmation..." :
                                             !connected ? "Not connected to the lobby. Retry or return to sign-in." :
                                             !hasCharacter ? "Choose a character before getting ready." :
                                             _isTeamMatch && !hasTeam ? "Choose Team A or Team B, then click Ready." :
                                             _isReady ? "You are ready. Waiting for the teacher to start the match..." : "Click Ready when you are ready to play. Only the teacher can start.");
        }

        private void RenderPlayers(GameState.MatchStateStore store)
        {
            _isTeamMatch = store.Mode == "teams";
            var sb = new StringBuilder();
            foreach (var p in store.LobbyPlayers)
            {
                var character = string.IsNullOrEmpty(p.characterId) ? "no character" : p.characterId;
                var team = _isTeamMatch ? $" - {(string.IsNullOrEmpty(p.team) ? "no team" : $"Team {p.team}")}" : "";
                var you = p.playerId == SessionManager.PlayerId ? " (you)" : "";
                sb.AppendLine($"{p.name}{you} - {character}{team} - {(p.ready ? "Ready" : "Not ready")}");
            }
            _playerListText.text = sb.ToString();
            RefreshControls();
        }

        private void OnMatchStarted(MatchStartPayload payload)
        {
            if (_stopped || _leaving) return;
            OnDisable();
            SceneManager.LoadScene("Arena");
        }
    }
}
