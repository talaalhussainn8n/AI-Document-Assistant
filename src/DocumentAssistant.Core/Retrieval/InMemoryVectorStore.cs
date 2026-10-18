using System.Collections.Concurrent;
using DocumentAssistant.Core.Models;

namespace DocumentAssistant.Core.Retrieval;

public interface IVectorStore
{
    void Add(DocumentChunk chunk, float[] vector);
    IReadOnlyList<SearchHit> Search(float[] query, int topK, Func<DocumentChunk, bool>? filter = null);
    int Count { get; }
}

/// <summary>
/// Einfacher, threadsicherer Vektorspeicher im Arbeitsspeicher mit Kosinus-Ähnlichkeit.
/// Über das Interface IVectorStore kann er durch eine Datenbank (z. B. PostgreSQL mit pgvector) ersetzt werden.
/// </summary>
public sealed class InMemoryVectorStore : IVectorStore
{
    private readonly ConcurrentBag<(DocumentChunk Chunk, float[] Vector)> _entries = new();

    public int Count => _entries.Count;

    public void Add(DocumentChunk chunk, float[] vector) => _entries.Add((chunk, vector));

    public IReadOnlyList<SearchHit> Search(float[] query, int topK, Func<DocumentChunk, bool>? filter = null)
    {
        return _entries
            .Where(e => filter is null || filter(e.Chunk))
            .Select(e => new SearchHit(e.Chunk, HashingEmbeddingProvider.Cosine(query, e.Vector)))
            .Where(h => h.Score > 0)
            .OrderByDescending(h => h.Score)
            .Take(topK)
            .ToList();
    }
}
