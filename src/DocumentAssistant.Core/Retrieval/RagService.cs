using System.Text;
using System.Text.RegularExpressions;
using DocumentAssistant.Core.Llm;
using DocumentAssistant.Core.Models;

namespace DocumentAssistant.Core.Retrieval;

/// <summary>
/// Retrieval-Augmented Generation: sucht passende Abschnitte und beantwortet die Frage
/// ausschließlich auf deren Grundlage. Ohne LLM wird der relevanteste Satz zurückgegeben.
/// </summary>
public sealed partial class RagService
{
    private readonly IVectorStore _store;
    private readonly IEmbeddingProvider _embeddings;
    private readonly ILlmClient _llm;

    public RagService(IVectorStore store, IEmbeddingProvider embeddings, ILlmClient llm)
    {
        _store = store;
        _embeddings = embeddings;
        _llm = llm;
    }

    [GeneratedRegex(@"(?<=[\.\!\?])\s+|\n+")]
    private static partial Regex SentenceSplitRegex();

    public async Task<Answer> AskAsync(
        string question,
        Func<DocumentChunk, bool>? filter = null,
        int topK = 4,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Die Frage darf nicht leer sein.", nameof(question));
        }

        var hits = _store.Search(_embeddings.Embed(question), topK, filter);
        if (hits.Count == 0)
        {
            return new Answer("In den vorhandenen Dokumenten wurde dazu nichts gefunden.", hits, false);
        }

        if (_llm.IsConfigured)
        {
            try
            {
                var answer = await _llm.CompleteAsync(SystemPrompt, BuildPrompt(question, hits), cancellationToken);
                if (!string.IsNullOrWhiteSpace(answer))
                {
                    return new Answer(answer, hits, true);
                }
            }
            catch (HttpRequestException)
            {
                // Rückfall auf die extraktive Antwort.
            }
        }

        return new Answer(ExtractBestSentence(question, hits), hits, false);
    }

    private const string SystemPrompt =
        "Du bist ein Assistent für Sachbearbeitung in der öffentlichen Verwaltung. " +
        "Beantworte die Frage nur mit Informationen aus den Quellen. " +
        "Nenne die Quelle in eckigen Klammern, z. B. [1]. " +
        "Wenn die Quellen die Antwort nicht enthalten, sage das ausdrücklich.";

    public static string BuildPrompt(string question, IReadOnlyList<SearchHit> hits)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Quellen:");
        for (var i = 0; i < hits.Count; i++)
        {
            sb.AppendLine($"[{i + 1}] {hits[i].Chunk.FileName}: {hits[i].Chunk.Text}");
        }
        sb.AppendLine();
        sb.AppendLine($"Frage: {question}");
        return sb.ToString();
    }

    /// <summary>Extraktive Antwort: der Satz mit der größten Überschneidung zur Frage.</summary>
    public static string ExtractBestSentence(string question, IReadOnlyList<SearchHit> hits)
    {
        var questionTokens = HashingEmbeddingProvider.Tokenize(question).ToHashSet();

        var best = hits
            .SelectMany(h => SentenceSplitRegex().Split(h.Chunk.Text)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => (Sentence: s.Trim(), Hit: h)))
            .Select(x => (x.Sentence, x.Hit,
                Overlap: HashingEmbeddingProvider.Tokenize(x.Sentence).Count(questionTokens.Contains)))
            .OrderByDescending(x => x.Overlap)
            .ThenByDescending(x => x.Hit.Score)
            .First();

        return $"{best.Sentence} (Quelle: {best.Hit.Chunk.FileName})";
    }
}
