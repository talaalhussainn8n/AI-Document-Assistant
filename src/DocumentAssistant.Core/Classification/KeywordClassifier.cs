using System.Text.RegularExpressions;
using DocumentAssistant.Core.Models;

namespace DocumentAssistant.Core.Classification;

/// <summary>
/// Regelbasierte Klassifikation über gewichtete Schlüsselbegriffe.
/// Dient als nachvollziehbare Basis und als Rückfallebene, wenn kein LLM verfügbar ist.
/// </summary>
public sealed class KeywordClassifier : IDocumentClassifier
{
    private static readonly IReadOnlyDictionary<DocumentType, (string Term, double Weight)[]> Rules =
        new Dictionary<DocumentType, (string, double)[]>
        {
            [DocumentType.Antrag] = new[]
            {
                ("antrag", 3.0), ("beantrage", 3.0), ("hiermit beantragen", 4.0), ("antragsteller", 2.0), ("unterschrift", 1.0)
            },
            [DocumentType.Bescheid] = new[]
            {
                ("bescheid", 4.0), ("rechtsbehelfsbelehrung", 4.0), ("widerspruch", 2.0), ("wird bewilligt", 3.0), ("wird abgelehnt", 3.0)
            },
            [DocumentType.Rechnung] = new[]
            {
                ("rechnung", 4.0), ("rechnungsnummer", 3.0), ("zahlbar bis", 3.0), ("mehrwertsteuer", 2.0), ("iban", 1.5), ("gesamtbetrag", 2.0)
            },
            [DocumentType.Nachweis] = new[]
            {
                ("nachweis", 3.0), ("bescheinigung", 3.0), ("verdienstbescheinigung", 4.0), ("gehaltsabrechnung", 4.0), ("bestätigen hiermit", 2.0)
            },
            [DocumentType.Vertrag] = new[]
            {
                ("vertrag", 3.0), ("mietvertrag", 4.0), ("vertragsparteien", 3.0), ("kündigungsfrist", 2.5), ("vermieter", 2.0), ("mieter", 1.5)
            }
        };

    public Task<ClassificationResult> ClassifyAsync(string text, CancellationToken cancellationToken = default)
        => Task.FromResult(Classify(text));

    public ClassificationResult Classify(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new ClassificationResult(DocumentType.Sonstiges, 0, "Kein Text vorhanden.");
        }

        var normalized = text.ToLowerInvariant();
        var scores = new Dictionary<DocumentType, double>();
        var matches = new Dictionary<DocumentType, List<string>>();

        foreach (var (type, terms) in Rules)
        {
            foreach (var (term, weight) in terms)
            {
                var count = Regex.Matches(normalized, Regex.Escape(term)).Count;
                if (count == 0)
                {
                    continue;
                }

                // Mehrfache Treffer zählen, aber abgeschwächt, damit ein Wort nicht dominiert.
                scores[type] = scores.GetValueOrDefault(type) + weight * (1 + Math.Log(count));
                if (!matches.TryGetValue(type, out var list))
                {
                    list = new List<string>();
                    matches[type] = list;
                }
                list.Add(term);
            }
        }

        if (scores.Count == 0)
        {
            return new ClassificationResult(DocumentType.Sonstiges, 0, "Keine typischen Begriffe gefunden.");
        }

        var ranked = scores.OrderByDescending(s => s.Value).ToList();
        var best = ranked[0];
        var total = scores.Values.Sum();
        var confidence = Math.Round(best.Value / total, 2);

        return new ClassificationResult(
            best.Key,
            confidence,
            $"Gefundene Begriffe: {string.Join(", ", matches[best.Key])}");
    }
}
