using System;

namespace QuizBattle.UI.NameEntry
{
    public static class JoinUrlPrefill
    {
        public static (string classCode, string joinCode) Parse(string absoluteUrl)
        {
            string classCode = null;
            string joinCode = null;
            if (!Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return ("", "");

            var url = absoluteUrl.Split('#')[0];
            int queryStart = url.IndexOf('?');
            if (queryStart < 0) return ("", "");
            foreach (var part in url.Substring(queryStart + 1).Split('&'))
            {
                int equals = part.IndexOf('=');
                if (equals < 0) continue;
                var key = Decode(part.Substring(0, equals));
                if (key != "classCode" && key != "joinCode") continue;
                var value = Decode(part.Substring(equals + 1));
                if (value == null) continue;
                if (key == "classCode" && classCode == null) classCode = value;
                if (key == "joinCode" && joinCode == null) joinCode = value;
            }
            return (classCode ?? "", joinCode ?? "");
        }

        private static string Decode(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] != '%') continue;
                if (i + 2 >= value.Length || !Uri.IsHexDigit(value[i + 1]) || !Uri.IsHexDigit(value[i + 2])) return null;
                i += 2;
            }
            try
            {
                var decoded = Uri.UnescapeDataString(value.Replace("+", " "));
                foreach (char c in decoded)
                    if (char.IsControl(c)) return null;
                return decoded.Trim();
            }
            catch (UriFormatException)
            {
                return null;
            }
        }
    }
}
