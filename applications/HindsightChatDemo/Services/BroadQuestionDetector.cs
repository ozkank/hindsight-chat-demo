using System.Globalization;

namespace HindsightChatDemo.Services;

/// <summary>
/// Heuristic check for "broad/general" questions -- the kind system_message.txt already
/// says should trigger reflect, not recall (e.g. "genel olarak müşteri deneyimim nasıldı?").
///
/// Found by testing (2026-08-26): "Merhaba, benim hakkımda ne biliyorsunuz?" reliably got
/// routed to recall instead of reflect. recall only ever returns ONE fact, so the result was
/// wildly inconsistent for a broad question -- one run returned a rich, multi-fact answer
/// (recall got lucky with a large chunk), another returned just the customer's name and
/// nothing else. Same "prompt alone isn't enough" pattern already documented for
/// GreetingDetector -- this is the code-level guard, used by HindsightAgentService to
/// redirect a recall call to reflect when the user's raw message looks like this.
/// </summary>
public static class BroadQuestionDetector
{
    private static readonly string[] Keywords =
    [
        "hakkımda", "hakkımızda", "genel olarak", "genel bir", "genel değerlendirme",
        "özetle", "özetler misiniz", "özetleyebilir", "değerlendirme yapar",
        "deneyimim nasıl", "ne biliyorsun", "ne biliyorsunuz", "öneride bulunur",
    ];

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static bool LooksBroad(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = message.ToLower(Turkish);
        return Keywords.Any(k => normalized.Contains(k, StringComparison.Ordinal));
    }
}
