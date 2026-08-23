using System;
using UnityEngine;

namespace QuizBattle.Networking
{
    /// Holds the current connection's identity/session info in memory for the
    /// duration of the app run. Not persisted — students re-enter classCode/name/PIN
    /// each session per the privacy-conscious identity design (see project plan).
    public static class SessionManager
    {
        public static string ServerHost = "localhost";
        public static int ServerPort = 7777;
        public static bool UseSsl = false;
        public static string AuthToken;
        public static int? PlayerId;
        public static string Role; // "student" | "teacher"
        public static string StudentName;
        public static string SelectedCharacterId;
        public static int MatchId;
        public static string JoinCode;

        public static string WsScheme => UseSsl ? "wss" : "ws";
        public static string HttpScheme => UseSsl ? "https" : "http";

        public static string WsUrl => (UseSsl && ServerPort == 443) || (!UseSsl && ServerPort == 80)
            ? $"{WsScheme}://{ServerHost}/ws"
            : $"{WsScheme}://{ServerHost}:{ServerPort}/ws";

        public static string HttpBaseUrl => (UseSsl && ServerPort == 443) || (!UseSsl && ServerPort == 80)
            ? $"{HttpScheme}://{ServerHost}"
            : $"{HttpScheme}://{ServerHost}:{ServerPort}";

        /// Automatically detects host, port, and SSL from browser origin when running in WebGL, or defaults to localhost:8080.
        public static void AutoDetectEndpoint()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                if (!string.IsNullOrEmpty(Application.absoluteURL))
                {
                    var uri = new Uri(Application.absoluteURL);
                    ServerHost = uri.Host;
                    UseSsl = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
                    if (uri.Port > 0 && uri.Port != 80 && uri.Port != 443)
                    {
                        ServerPort = uri.Port;
                    }
                    else
                    {
                        ServerPort = UseSsl ? 443 : 80;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SessionManager] WebGL URL autodetect skipped: {ex.Message}");
            }
#else
            if (string.IsNullOrEmpty(ServerHost) || ServerHost == "localhost")
            {
                ServerHost = "localhost";
                ServerPort = 8080;
                UseSsl = false;
            }
#endif
        }

        /// Parses a raw host/URL input (e.g. "https://quizbattle.run.app", "192.168.1.5:7777", "quizbattle.com")
        public static void SetEndpoint(string rawHostOrUrl, int? explicitPort = null)
        {
            if (string.IsNullOrWhiteSpace(rawHostOrUrl))
            {
                ServerHost = "localhost";
                ServerPort = explicitPort ?? 7777;
                UseSsl = false;
                return;
            }

            var input = rawHostOrUrl.Trim().TrimEnd('/');

            if (input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                UseSsl = true;
                input = input.Substring(8);
                ServerPort = explicitPort ?? 443;
            }
            else if (input.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            {
                UseSsl = true;
                input = input.Substring(6);
                ServerPort = explicitPort ?? 443;
            }
            else if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                UseSsl = false;
                input = input.Substring(7);
                ServerPort = explicitPort ?? 80;
            }
            else if (input.StartsWith("ws://", StringComparison.OrdinalIgnoreCase))
            {
                UseSsl = false;
                input = input.Substring(5);
                ServerPort = explicitPort ?? 80;
            }
            else
            {
                UseSsl = explicitPort == 443;
                ServerPort = explicitPort ?? (UseSsl ? 443 : 7777);
            }

            // Remove any path after host:port (e.g. /play or /ws)
            var slashIdx = input.IndexOf('/');
            if (slashIdx >= 0)
            {
                input = input.Substring(0, slashIdx);
            }

            // Parse port from host:port if embedded
            var colonIdx = input.LastIndexOf(':');
            if (colonIdx >= 0 && int.TryParse(input.Substring(colonIdx + 1), out var parsedPort))
            {
                ServerHost = input.Substring(0, colonIdx);
                ServerPort = parsedPort;
                if (parsedPort == 443) UseSsl = true;
            }
            else
            {
                ServerHost = input;
            }
        }

        public static void Reset()
        {
            AuthToken = null;
            PlayerId = null;
            Role = null;
        }
    }
}

