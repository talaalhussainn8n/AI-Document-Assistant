namespace DocumentAssistant.Core.Models;

/// <summary>
/// Aus einem Dokument automatisch ermittelte Kerndaten.
/// </summary>
public sealed record DocumentMetadata(
    DateOnly? Datum,
    string? Aktenzeichen,
    string? Absender,
    decimal? Betrag);

/// <summary>
/// Ergebnis der Klassifikation inklusive Sicherheit (0..1) und Begründung.
/// </summary>
public sealed record ClassificationResult(DocumentType Type, double Confidence, string Reason);

/// <summary>
/// Ein verarbeitetes Dokument in einer Akte.
/// </summary>
public sealed class Document
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string FileName { get; init; }
    public required string CaseId { get; init; }
    public required string Text { get; init; }
    public required ClassificationResult Classification { get; init; }
    public required DocumentMetadata Metadata { get; init; }
    public DateTimeOffset UploadedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Ein Textabschnitt eines Dokuments, der für die semantische Suche indiziert wird.
/// </summary>
public sealed record DocumentChunk(Guid DocumentId, string FileName, int Index, string Text);

/// <summary>
/// Ein Suchtreffer mit Ähnlichkeitswert.
/// </summary>
public sealed record SearchHit(DocumentChunk Chunk, double Score);

/// <summary>
/// Antwort auf eine Frage inklusive der verwendeten Quellen.
/// </summary>
public sealed record Answer(string Text, IReadOnlyList<SearchHit> Sources, bool GeneratedByLlm);
