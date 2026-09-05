using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using QuizBattle.GameState;
using QuizBattle.Networking;
using QuizBattle.Networking.Protocol;
using QuizBattle.UI.Lobby;
using QuizBattle.UI.NameEntry;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace QuizBattle.Tests.EditMode
{
    public class JoinUrlPrefillTests
    {
        [TestCase(null, "", "")]
        [TestCase("", "", "")]
        [TestCase("not a url", "", "")]
        [TestCase("file:///play/?classCode=CLASS", "", "")]
        [TestCase("https://school.test/play/", "", "")]
        [TestCase("https://school.test/play/?classCode=MATH101&joinCode=LOBBY1", "MATH101", "LOBBY1")]
        [TestCase("https://school.test/play/?joinCode=A%26B%3DC%23D&classCode=Year+5", "Year 5", "A&B=C#D")]
        [TestCase("https://school.test/play/?classCode=%20A%2BB%20&joinCode=%2526", "A+B", "%26")]
        [TestCase("https://school.test/play/?class%43ode=ROOM&joinCode=M", "ROOM", "M")]
        [TestCase("https://school.test/play/?classCode=FIRST&classCode=SECOND&joinCode=X&joinCode=Y", "FIRST", "X")]
        [TestCase("https://school.test/play/?classCode=&classCode=SECOND&joinCode=", "", "")]
        [TestCase("https://school.test/play/?classCode&joinCode=M", "", "M")]
        [TestCase("https://school.test/play/?ClassCode=NO&matchCode=NO&joinCode=YES", "", "YES")]
        [TestCase("https://school.test/play/?classCode=%&joinCode=OK", "", "OK")]
        [TestCase("https://school.test/play/?classCode=%2&joinCode=OK", "", "OK")]
        [TestCase("https://school.test/play/?classCode=%GG&joinCode=OK", "", "OK")]
        [TestCase("https://school.test/play/?classCode=%00X&joinCode=OK", "", "OK")]
        [TestCase("https://school.test/play/?classCode=A%0AB&joinCode=OK", "", "OK")]
        [TestCase("https://school.test/play/?classCode=%GG&classCode=VALID&joinCode=M", "VALID", "M")]
        [TestCase("https://school.test/play/?classCode=C&joinCode=M#joinCode=OTHER", "C", "M")]
        [TestCase("https://school.test/play/#?classCode=NO&joinCode=NO", "", "")]
        [TestCase("https://school.test/play/?name=Alex&pin=1234&token=secret&classCode=C&joinCode=M", "C", "M")]
        [TestCase("https://school.test/play/?classCode=%E6%95%B0%E5%AD%A6&joinCode=M", "\u6570\u5b66", "M")]
        public void PrefillsOnlyDecodedClassAndJoinCodes(string url, string classCode, string joinCode)
        {
            var result = JoinUrlPrefill.Parse(url);
            Assert.AreEqual(classCode, result.classCode);
            Assert.AreEqual(joinCode, result.joinCode);
        }

        [Test]
        public void ParsingDoesNotChangeIdentityOrSession()
        {
            var token = SessionManager.AuthToken;
            var name = SessionManager.StudentName;
            var playerId = SessionManager.PlayerId;
            var joinCode = SessionManager.JoinCode;
            JoinUrlPrefill.Parse("https://school.test/play/?token=secret&name=OTHER&pin=1234&joinCode=M");
            Assert.AreEqual(token, SessionManager.AuthToken);
            Assert.AreEqual(name, SessionManager.StudentName);
            Assert.AreEqual(playerId, SessionManager.PlayerId);
            Assert.AreEqual(joinCode, SessionManager.JoinCode);
        }
    }

    public class StudentLoginValidationTests
    {
        [TestCase("7")]
        [TestCase("12")]
        [TestCase("123")]
        [TestCase("1234")]
        [TestCase("0")]
        [TestCase("001")]
        [TestCase("0000")]
        public void ExistingShortPinsPassLoginValidation(string pin)
        {
            var error = typeof(NameEntryScreen).GetMethod("ValidateDetails", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { "CLASS", "Alex", pin, "MATCH" });
            Assert.IsNull(error);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void MissingPinStillRequiresAnExistingOrFirstUsePin(string pin)
        {
            var error = (string)typeof(NameEntryScreen).GetMethod("ValidateDetails", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { "CLASS", "Alex", pin, "MATCH" });
            StringAssert.Contains("existing PIN", error);
            StringAssert.Contains("new nickname", error);
        }
    }

    public class StudentLobbyTests
    {
        private GameObject _root;
        private LobbyScreen _screen;
        private MatchStateStore _store;
        private int? _playerId;

        [SetUp]
        public void SetUp()
        {
            _playerId = SessionManager.PlayerId;
            SessionManager.PlayerId = 7;
            _root = new GameObject("StudentLobbyTest");
            _screen = _root.AddComponent<LobbyScreen>();
            _store = new MatchStateStore();
            Set("_store", _store);
            Set("_client", new WsClient());
            Set("_matchId", 42);
            Set("_hasLobby", true);
            var textType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro", true);
            foreach (var field in new[] { "_readyButton", "_teamAButton", "_teamBButton", "_retryButton", "_backButton" })
            {
                var button = new GameObject(field, typeof(RectTransform), typeof(Button));
                button.transform.SetParent(_root.transform);
                var label = new GameObject("Label", typeof(RectTransform), textType);
                label.transform.SetParent(button.transform);
                Set(field, button.GetComponent<Button>());
            }
            foreach (var field in new[] { "_statusText", "_playerListText" })
            {
                var text = new GameObject(field, typeof(RectTransform), textType);
                text.transform.SetParent(_root.transform);
                Set(field, text.GetComponent(textType));
            }
            Snapshot(false, "A");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            SessionManager.PlayerId = _playerId;
        }

        [Test]
        public void ReadyReflectsAcknowledgementNotPendingIntent()
        {
            Set("_pendingReady", (bool?)true);
            Snapshot(false, "A");
            Assert.IsFalse(Get<bool>("_isReady"));
            Assert.AreEqual(true, Get<bool?>("_pendingReady"));
            Snapshot(true, "A");
            Assert.IsTrue(Get<bool>("_isReady"));
            Assert.IsNull(Get<bool?>("_pendingReady"));
            var label = Get<Button>("_readyButton").transform.Find("Label").GetComponent(Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro", true));
            Assert.AreEqual("Not Ready", label.GetType().GetProperty("text").GetValue(label));
        }

        [Test]
        public void TeamAcknowledgementDoesNotAutoReady()
        {
            Set("_pendingTeam", "B");
            Snapshot(false, "A");
            Assert.AreEqual("B", Get<string>("_pendingTeam"));
            Snapshot(false, "B");
            Assert.IsNull(Get<string>("_pendingTeam"));
            Assert.IsFalse(Get<bool>("_isReady"));
            Assert.IsNull(Get<bool?>("_pendingReady"));
        }

        [Test]
        public void DisconnectedClickCannotToggleReady()
        {
            Call("OnReadyClicked");
            Assert.IsFalse(Get<Button>("_readyButton").interactable);
            Assert.IsFalse(Get<bool>("_isReady"));
            Assert.IsNull(Get<bool?>("_pendingReady"));
        }

        [Test]
        public void CachedSelfCannotCompleteRejoin()
        {
            Set("_hasLobby", false);
            Set("_reconnecting", true);
            Snapshot(true, "A");
            Assert.IsFalse(Get<bool>("_hasLobby"));
            Assert.IsFalse(Get<Button>("_readyButton").interactable);
            Assert.IsFalse(Get<bool>("_isReady"));
        }

        [Test]
        public void TimeoutClearsPendingWithoutChangingServerReady()
        {
            Set("_pendingReady", (bool?)true);
            Set("_pendingDeadline", -1f);
            Call("Update");
            Assert.IsNull(Get<bool?>("_pendingReady"));
            Assert.IsFalse(Get<bool>("_isReady"));
            StringAssert.Contains("No confirmation", Status());
        }

        [Test]
        public void LateSnapshotReplacesTimeoutFeedbackWithServerState()
        {
            Set("_pendingReady", (bool?)true);
            Set("_pendingDeadline", -1f);
            Call("Update");
            Snapshot(true, "A");
            Assert.IsTrue(Get<bool>("_isReady"));
            Assert.IsNull(Get<string>("_feedback"));
        }

        [Test]
        public void RejectionClearsPendingAndExplainsTeamRequirement()
        {
            Set("_pendingReady", (bool?)true);
            Call("OnServerError", new ErrorPayload { Code = "no_team_selected" });
            Assert.IsNull(Get<bool?>("_pendingReady"));
            Assert.IsFalse(Get<bool>("_isReady"));
            StringAssert.Contains("Choose Team A or Team B", Status());
        }

        [Test]
        public void DisableCancelsWaitersAndIgnoresLateCallbacks()
        {
            bool unsubscribed = false;
            Set("_unsubscribeRejoin", (Action)(() => unsubscribed = true));
            Call("OnDisable");
            Assert.IsTrue(unsubscribed);
            Assert.IsTrue(Get<bool>("_stopped"));
            Call("OnServerError", new ErrorPayload { Message = "Late error" });
            Assert.IsNull(Get<string>("_feedback"));
        }

        [Test]
        public void DisabledNameEntryIgnoresJoinWithoutTouchingDestroyedFields()
        {
            var entry = _root.AddComponent<NameEntryScreen>();
            typeof(NameEntryScreen).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(entry, null);
            Assert.DoesNotThrow(() => entry.OnJoinClicked());
            var join = entry.Join("C", "Alex", "1234", "M");
            Assert.IsTrue(join.IsCompleted);
            Assert.IsFalse(join.IsFaulted);
        }

        [Test]
        public void BackIsDisabledAndDoesNotCloseDuringRejoin()
        {
            Set("_reconnecting", true);
            Call("RefreshControls");
            Assert.IsFalse(Get<Button>("_backButton").interactable);
            var back = Back();
            Assert.IsTrue(back.IsCompleted);
            Assert.IsFalse(back.IsFaulted);
            Assert.IsFalse(Get<bool>("_leaving"));
            Assert.IsNull(Get<Task<string>>("_closeTask"));
        }

        [Test]
        public async Task BackWaitsForPendingCloseAndSuppressesRejoin()
        {
            var close = new TaskCompletionSource<string>();
            Set("_closeTask", close.Task);
            var back = Back();
            Assert.IsFalse(back.IsCompleted);
            Assert.IsTrue(Get<bool>("_leaving"));
            Assert.IsFalse(Get<Button>("_backButton").interactable);
            Assert.IsFalse(Get<Button>("_readyButton").interactable);
            Call("OnDisconnected", "Normal");
            Assert.IsTrue(Get<bool>("_connectionClosed"));
            Assert.IsFalse(Get<bool>("_reconnecting"));
            Assert.IsFalse(back.IsCompleted);
            Assert.IsTrue(Back().IsCompleted);
            Assert.AreSame(close.Task, Get<Task<string>>("_closeTask"));
            Call("OnDisable");
            close.SetResult(null);
            await back;
            Assert.IsTrue(Get<bool>("_stopped"));
        }

        [Test]
        public async Task CompletedCloseWithoutDisconnectDoesNotNavigate()
        {
            var back = Back();
            Assert.IsTrue(Get<Task<string>>("_closeTask").IsCompleted);
            Assert.IsFalse(Get<bool>("_connectionClosed"));
            Assert.IsFalse(back.IsCompleted);
            Assert.IsFalse(Get<bool>("_stopped"));
            Call("OnDisable");
            await back;
        }

        [Test]
        public async Task DestroyedScreenCannotActOnLateCloseCompletion()
        {
            var close = new TaskCompletionSource<string>();
            Set("_closeTask", close.Task);
            var back = Back();
            Object.DestroyImmediate(_root);
            close.SetResult("Late close error");
            await back;
            Assert.IsFalse(back.IsFaulted);
        }

        [Test]
        public void RejoinCannotReplaceAnUnconfirmedPreviousConnection()
        {
            var token = SessionManager.AuthToken;
            var joinCode = SessionManager.JoinCode;
            try
            {
                SessionManager.AuthToken = "test-token";
                SessionManager.JoinCode = "MATCH";
                var rejoin = (Task)typeof(LobbyScreen).GetMethod("Rejoin", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_screen, null);
                Assert.IsTrue(rejoin.IsCompleted);
                Assert.IsFalse(rejoin.IsFaulted);
                Assert.IsNull(typeof(WsClient).GetField("_socket", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Get<WsClient>("_client")));
                StringAssert.Contains("previous connection has not closed", Status());
            }
            finally
            {
                SessionManager.AuthToken = token;
                SessionManager.JoinCode = joinCode;
            }
        }

        [Test]
        public async Task CloseTimeoutStaysInLobbyAndRetryReusesThePendingClose()
        {
            var close = new TaskCompletionSource<string>();
            Set("_closeTask", close.Task);
            await Back();
            Assert.IsTrue(Get<bool>("_leaving"));
            Assert.IsFalse(Get<bool>("_stopped"));
            Assert.IsFalse(Get<bool>("_closing"));
            Assert.IsTrue(Get<Button>("_backButton").interactable);
            StringAssert.Contains("Could not confirm disconnection", Status());
            var retry = Back();
            Assert.AreSame(close.Task, Get<Task<string>>("_closeTask"));
            Call("OnDisable");
            close.SetResult(null);
            await retry;
        }

        private Task Back() => (Task)typeof(LobbyScreen).GetMethod("BackToSignIn", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_screen, null);

        private void Snapshot(bool ready, string team)
        {
            var payload = new LobbyStatePayload
            {
                MatchId = 42,
                Mode = "teams",
                Players = new List<LobbyPlayerPayload>
                {
                    new LobbyPlayerPayload { PlayerId = 7, Name = "Alex", CharacterId = "blaze", Team = team, Ready = ready }
                }
            };
            typeof(MatchStateStore).GetMethod("HandleLobbyState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_store, new object[] { payload });
            Call("OnLobbyUpdated", payload);
        }

        private void Set(string name, object value) => typeof(LobbyScreen).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_screen, value);
        private T Get<T>(string name) => (T)typeof(LobbyScreen).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_screen);
        private void Call(string name, params object[] args) => typeof(LobbyScreen).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_screen, args);
        private string Status()
        {
            var text = Get<Component>("_statusText");
            return (string)text.GetType().GetProperty("text").GetValue(text);
        }
    }
}
