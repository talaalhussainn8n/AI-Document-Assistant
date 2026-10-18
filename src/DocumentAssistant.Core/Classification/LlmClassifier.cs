using DocumentAssistant.Core.Llm;
using DocumentAssistant.Core.Models;

namespace DocumentAssistant.Core.Classification;

/// <summary>
/// Klassifikation mit einem Sprachmodell. Fällt auf die regelbasierte Klassifikation zurück,
/// wenn kein Modell konfiguriert ist, der Aufruf fehlschlägt oder die Antwort nicht auswertbar ist.
/// </summary>
public sealed class LlmClassifier : IDocumentClassifier
{
    private const int MaxCharacters = 6000;

    private readonly ILlmClient _llm;
    private readonly KeywordClassifier _fallback;

    public LlmClassifier(ILlmClient llm, KeywordClassifier fallback)
    {
        _llm = llm;
        _fallback = fallback;
    }

    public async Task<ClassificationResult> ClassifyAsync(string text, CancellationToken cancellationToken = default)
    {
        if (!_llm.IsConfigured)
        {
            return _fallback.Classify(text);
        }

        var allowed = string.Join(", ", Enum.GetNames<DocumentType>());
        var system = "Du klassifizierst Dokumente aus Verwaltungsakten. " +
                     $"Antworte ausschließlich mit genau einem dieser Werte: {allowed}.";
        var excerpt = text.Length > MaxCharacters ? text[..MaxCharacters] : text;

        try
        {
            var raw = await _llm.CompleteAsync(system, excerpt, cancellationToken);
            if (TryParse(raw, out var type))
            {
                return new ClassificationResult(type, 0.9, "Vom Sprachmodell klassifiziert.");
            }
        }
        catch (HttpRequestException)
        {
            // Netzwerk- oder API-Fehler: regelbasiert weiterarbeiten.
        }

        return _fallback.Classify(text);
    }

    public static bool TryParse(string? raw, out DocumentType type)
    {
        type = DocumentType.Sonstiges;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var cleaned = raw.Trim().Trim('.', '"', '\'', '*').Trim();
        return Enum.TryParse(cleaned, ignoreCase: true, out type) && Enum.IsDefined(type);
    }
}
