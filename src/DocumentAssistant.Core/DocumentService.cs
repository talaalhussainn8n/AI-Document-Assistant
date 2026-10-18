using System.Collections.Concurrent;
using DocumentAssistant.Core.Classification;
using DocumentAssistant.Core.Completeness;
using DocumentAssistant.Core.Extraction;
using DocumentAssistant.Core.Models;
using DocumentAssistant.Core.Retrieval;

namespace DocumentAssistant.Core;

/// <summary>
/// Verarbeitungs-Pipeline: Klassifizieren, Kerndaten extrahieren, in Abschnitte zerlegen und indizieren.
/// </summary>
public sealed class DocumentService
{
    private readonly ConcurrentDictionary<Guid, Document> _documents = new();
    private readonly IDocumentClassifier _classifier;
    private readonly MetadataExtractor _extractor;
    private readonly TextChunker _chunker;
    private readonly IEmbeddingProvider _embeddings;
    private readonly IVectorStore _store;
    private readonly RagService _rag;
    private readonly CompletenessChecker _completeness;

    public DocumentService(
        IDocumentClassifier classifier,
        MetadataExtractor extractor,
        TextChunker chunker,
        IEmbeddingProvider embeddings,
        IVectorStore store,
        RagService rag,
        CompletenessChecker completeness)
    {
        _classifier = classifier;
        _extractor = extractor;
        _chunker = chunker;
        _embeddings = embeddings;
        _store = store;
        _rag = rag;
        _completeness = completeness;
    }

    public async Task<Document> IngestAsync(string caseId, string fileName, string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(caseId))
        {
            throw new ArgumentException("Die Akten-ID darf nicht leer sein.", nameof(caseId));
        }

        var classification = await _classifier.ClassifyAsync(text, cancellationToken);
        var document = new Document
        {
            CaseId = caseId.Trim(),
            FileName = fileName,
            Text = text,
            Classification = classification,
            Metadata = _extractor.Extract(text)
        };

        foreach (var chunk in _chunker.Chunk(document.Id, fileName, text))
        {
            _store.Add(chunk, _embeddings.Embed(chunk.Text));
        }

        _documents[document.Id] = document;
        return document;
    }

    public IReadOnlyList<Document> GetAll(string? caseId = null) =>
        _documents.Values
            .Where(d => caseId is null || string.Equals(d.CaseId, caseId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.UploadedAt)
            .ToList();

    public Document? Get(Guid id) => _documents.GetValueOrDefault(id);

    public Task<Answer> AskAsync(string question, string? caseId = null, CancellationToken cancellationToken = default)
    {
        Func<DocumentChunk, bool>? filter = null;
        if (!string.IsNullOrWhiteSpace(caseId))
        {
            var ids = GetAll(caseId).Select(d => d.Id).ToHashSet();
            filter = chunk => ids.Contains(chunk.DocumentId);
        }

        return _rag.AskAsync(question, filter, cancellationToken: cancellationToken);
    }

    public CompletenessReport CheckCompleteness(string caseId, string caseType) =>
        _completeness.Check(caseType, GetAll(caseId));
}
