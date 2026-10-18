using System.Globalization;
using System.Text.RegularExpressions;
using DocumentAssistant.Core.Models;

namespace DocumentAssistant.Core.Extraction;

/// <summary>
/// Liest Kerndaten (Datum, Aktenzeichen, Absender, Betrag) aus deutschem Verwaltungstext.
/// </summary>
public sealed partial class MetadataExtractor
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    [GeneratedRegex(@"\b(\d{1,2})\.(\d{1,2})\.(\d{4})\b")]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"\b(?:Aktenzeichen|Az\.?|Geschäftszeichen)\s*:?\s*([A-Za-z0-9][A-Za-z0-9\-/\.]{2,40})", RegexOptions.IgnoreCase)]
    private static partial Regex CaseNumberRegex();

    [GeneratedRegex(@"(?:Absender|Von)\s*:\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex SenderRegex();

    [GeneratedRegex(@"(?:Gesamtbetrag|Betrag|Summe)\s*:?\s*(?:EUR|€)?\s*(\d{1,3}(?:\.\d{3})*,\d{2})\s*(?:EUR|€)?", RegexOptions.IgnoreCase)]
    private static partial Regex AmountRegex();

    public DocumentMetadata Extract(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new DocumentMetadata(null, null, null, null);
        }

        return new DocumentMetadata(
            ExtractDate(text),
            ExtractCaseNumber(text),
            ExtractSender(text),
            ExtractAmount(text));
    }

    /// <summary>Erstes gültiges Datum im Format TT.MM.JJJJ.</summary>
    public static DateOnly? ExtractDate(string text)
    {
        foreach (Match match in DateRegex().Matches(text))
        {
            var day = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var year = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);

            if (month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month))
            {
                return new DateOnly(year, month, day);
            }
        }

        return null;
    }

    public static string? ExtractCaseNumber(string text)
    {
        var match = CaseNumberRegex().Match(text);
        return match.Success ? match.Groups[1].Value.TrimEnd('.') : null;
    }

    /// <summary>
    /// Absender aus einer Zeile "Absender:" oder "Von:"; sonst die erste nicht leere Zeile.
    /// </summary>
    public static string? ExtractSender(string text)
    {
        var match = SenderRegex().Match(text);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        var firstLine = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstLine) ? null : firstLine;
    }

    public static decimal? ExtractAmount(string text)
    {
        var match = AmountRegex().Match(text);
        if (!match.Success)
        {
            return null;
        }

        return decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, German, out var amount)
            ? amount
            : null;
    }
}
