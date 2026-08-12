namespace HindsightChatDemo.Services;

/// <summary>
/// Recognizes whether a Turkish message reads as a question / reference to something
/// already said (recall territory) versus a plain new statement (retain territory).
///
/// Why this exists: llama3.1:8b sometimes calls <c>recall</c> for a message that is
/// actually a brand-new fact -- e.g. "geçen hafta taşındım" ("I moved last week") gets
/// routed to recall instead of retain, purely because it shares the phrase "geçen hafta"
/// with the recall example in system_message.txt. See
/// <see cref="HindsightAgentService"/>'s use of this detector as a safety net for that case.
///
/// This is a heuristic, not a parser, and it's deliberately biased toward under-detecting
/// questions: a false "not a question" just costs one harmless extra retain call (Hindsight
/// dedupes it during consolidation), while a false "is a question" would let a real new fact
/// slip through unsaved, which is the exact bug this exists to catch.
/// </summary>
public static class DeclarativeStatementDetector
{
    // Turkish yes/no question particles (mı/mi/mu/mü and their tense/person-suffixed forms)
    // are always written as a separate word, so a whole-token match is reliable.
    private static readonly HashSet<string> QuestionParticles = new(StringComparer.OrdinalIgnoreCase)
    {
        "mı", "mi", "mu", "mü",
        "mıydı", "miydi", "muydu", "müydü",
        "mısın", "misin", "musun", "müsün",
        "mısınız", "misiniz", "musunuz", "müsünüz",
        "mıyım", "miyim", "muyum", "müyüm",
    };

    private static readonly char[] Separators = [' ', '\t', '\n', ',', '.', '!'];

    public static bool LooksLikeQuestion(string message)
    {
        if (message.Contains('?'))
        {
            return true;
        }

        var lower = message.ToLowerInvariant();
        if (lower.Contains("hatırl")) // "hatırlıyor musunuz", "hatırlar mısınız", ...
        {
            return true;
        }

        var tokens = lower.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Any(t => QuestionParticles.Contains(t.TrimEnd('?')));
    }
}
