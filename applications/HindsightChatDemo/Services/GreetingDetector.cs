namespace HindsightChatDemo.Services;

/// <summary>
/// Recognizes trivial greeting-only messages ("merhaba", "selam", ...) so they can bypass
/// the LLM/tool-calling pipeline entirely.
///
/// Why this exists: llama3.1:8b reliably calls <c>retain</c> on a bare greeting with
/// garbled content (e.g. content: "Mürderi merhaba"), even though the system prompt
/// explicitly says not to. Three rounds of prompt hardening -- adding the rule, adding
/// concrete examples, moving it to the very first paragraph in all caps -- all still
/// failed 4/4 in testing. That's a real limitation of this small local model on
/// short/low-information input, not something a better prompt fixes. This guard is the
/// pragmatic answer: it only matches messages that are ENTIRELY a known greeting, so
/// anything with real content (e.g. "Merhaba, ben Ahmet, ...") still goes to the agent
/// as normal -- it's a narrow bypass, not a general intent classifier.
/// </summary>
public static class GreetingDetector
{
    private static readonly HashSet<string> Greetings = new(StringComparer.OrdinalIgnoreCase)
    {
        "merhaba", "selam", "selamlar", "iyi günler", "günaydın", "iyi akşamlar",
        "naber", "nasılsın", "nasılsınız", "teşekkürler", "teşekkür ederim", "sağ ol", "sağol",
    };

    private static readonly string[] CannedReplies =
    [
        "Merhaba! Size nasıl yardımcı olabilirim?",
        "İyi günler! Nasıl yardımcı olabilirim?",
        "Merhaba, hoş geldiniz! Sizi dinliyorum.",
    ];

    public static bool IsGreetingOnly(string message) =>
        Greetings.Contains(message.Trim().TrimEnd('!', '.', '?', ','));

    /// <summary>Picks a reply deterministically per session, so a retry in the same
    /// conversation doesn't feel random, but different sessions don't all sound identical.</summary>
    public static string PickReply(string sessionId)
    {
        var index = Math.Abs(sessionId.GetHashCode()) % CannedReplies.Length;
        return CannedReplies[index];
    }
}
