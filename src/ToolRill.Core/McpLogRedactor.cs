using System.Text.RegularExpressions;

namespace ToolRill.Core;

public static class McpLogRedactor
{
    private const string Replacement = "[REDACTED]";
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly Regex Url = CreateRegex("https?://[^\\s\\\"'<>]+");
    private static readonly Regex UserInfo = CreateRegex(@"(https?://)[^/?#\s]*@");
    private static readonly Regex QueryValue = CreateRegex(@"([?&][^=&#\s]+)=([^&#\s]*)");
    private static readonly Regex Fragment = CreateRegex(@"#.*");
    private static readonly Regex Credentials = CreateRegex(
        """\b(authorization|proxy-authorization|[\w-]*api[-_ ]?key|[\w-]*token|password|passwd|pwd|[\w-]*secret|client[-_]?secret|cookie|set-cookie|signature|credential)\b["']?\s*[:=]\s*(?:"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|[^\r\n,;&"'<>{}]+)""");
    private static readonly Regex Authorization = CreateRegex(
        """\b((?:proxy-)?authorization|(?:set-)?cookie)["']?\s*[:=]\s*(?:"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|[^\r\n"'<>{}]+)""");
    private static readonly Regex Authentication = CreateRegex(
        "\\b(Bearer|Basic)\\s+[^\\s,;\\\"'<>]+");

    public static string Redact(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        message = ToolRill.Client.HttpConnectionSettings.RedactHeaderValues(message);

        try
        {
            message = Url.Replace(message, match =>
            {
                var url = UserInfo.Replace(match.Value, "$1" + Replacement + "@");
                url = QueryValue.Replace(url, "$1=" + Replacement);
                return Fragment.Replace(url, "#" + Replacement);
            });
            message = Authorization.Replace(message, "$1=" + Replacement);
            message = Credentials.Replace(message, "$1=" + Replacement);
            return Authentication.Replace(message, "$1 " + Replacement);
        }
        catch (RegexMatchTimeoutException)
        {
            return Replacement;
        }
    }

    public static void WriteError(string message)
        => Console.Error.WriteLine(Redact(message));

    private static Regex CreateRegex(string pattern)
        => new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);
}