using System;
using System.Text;

namespace b2xtranslator.OpenXmlLib
{
    /// <summary>
    /// Replacement for the obsolete Uri.EscapeUriString (SYSLIB0013) with identical output:
    /// RFC 3986 unreserved and reserved ASCII characters are kept, every other character
    /// (including '%') is UTF-8 encoded and percent-escaped with upper-case hex digits.
    /// </summary>
    internal static class UriEscaping
    {
        private const string ReservedChars = "!#$&'()*+,/:;=?@[]";

        internal static string EscapeUriString(string stringToEscape)
        {
            if (stringToEscape == null)
                throw new ArgumentNullException(nameof(stringToEscape));

            StringBuilder sb = null;
            int i = 0;
            while (i < stringToEscape.Length)
            {
                if (IsAllowed(stringToEscape[i]))
                {
                    sb?.Append(stringToEscape[i]);
                    i++;
                    continue;
                }

                if (sb == null)
                    sb = new StringBuilder(stringToEscape, 0, i, stringToEscape.Length + 16);

                // Encode the whole run of disallowed chars at once so surrogate pairs stay together;
                // lone surrogates become U+FFFD, as in Uri.EscapeUriString.
                int start = i;
                while (i < stringToEscape.Length && !IsAllowed(stringToEscape[i]))
                    i++;
                foreach (byte b in Encoding.UTF8.GetBytes(stringToEscape.Substring(start, i - start)))
                    sb.Append('%').Append(b.ToString("X2"));
            }
            return sb == null ? stringToEscape : sb.ToString();
        }

        private static bool IsAllowed(char c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
            || c == '-' || c == '.' || c == '_' || c == '~'
            || ReservedChars.IndexOf(c) >= 0;
    }
}
