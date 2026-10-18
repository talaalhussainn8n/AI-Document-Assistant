using DocumentAssistant.Core.Models;

namespace DocumentAssistant.Core.Completeness;

public sealed record CompletenessReport(
    string CaseType,
    IReadOnlyList<DocumentType> Required,
    IReadOnlyList<DocumentType> Present,
    IReadOnlyList<DocumentType> Missing)
{
    public bool IsComplete => Missing.Count == 0;
}

/// <summary>
/// Prüft, ob eine Akte alle für ihren Vorgangstyp erforderlichen Unterlagen enthält.
/// </summary>
public sealed class CompletenessChecker
{
    private readonly IReadOnlyDictionary<string, DocumentType[]> _requirements;

    public CompletenessChecker(IReadOnlyDictionary<string, DocumentType[]>? requirements = null)
    {
        _requirements = requirements ?? DefaultRequirements;
    }

    public static readonly IReadOnlyDictionary<string, DocumentType[]> DefaultRequirements =
        new Dictionary<string, DocumentType[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Wohngeld"] = new[] { DocumentType.Antrag, DocumentType.Nachweis, DocumentType.Vertrag },
            ["Förderung"] = new[] { DocumentType.Antrag, DocumentType.Nachweis, DocumentType.Rechnung },
            ["Widerspruch"] = new[] { DocumentType.Bescheid, DocumentType.Antrag }
        };

    public IEnumerable<string> CaseTypes => _requirements.Keys;

    public CompletenessReport Check(string caseType, IEnumerable<Document> documents)
    {
        if (!_requirements.TryGetValue(caseType, out var required))
        {
            throw new ArgumentException($"Unbekannter Vorgangstyp: {caseType}", nameof(caseType));
        }

        var present = documents
            .Select(d => d.Classification.Type)
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        var missing = required.Where(r => !present.Contains(r)).ToList();
        return new CompletenessReport(caseType, required, present, missing);
    }
}
