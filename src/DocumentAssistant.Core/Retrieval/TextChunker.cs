using System.Text;
using DocumentAssistant.Core.Models;

namespace DocumentAssistant.Core.Retrieval;

/// <summary>
/// Zerlegt Dokumenttext entlang von Absätzen in Abschnitte begrenzter Länge,
/// damit sie einzeln eingebettet und durchsucht werden können.
/// </summary>
public sealed class TextChunker
{
    private readonly int _maxChars;

    public TextChunker(int maxChars = 600)
    {
        if (maxChars < 50)
        {
            throw new ArgumentOutOfRangeException(nameof(maxChars), "Mindestens 50 Zeichen pro Abschnitt.");
        }

        _maxChars = maxChars;
    }

    public IReadOnlyList<DocumentChunk> Chunk(Guid documentId, string fileName, string text)
    {
        var chunks = new List<DocumentChunk>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return chunks;
        }

        var paragraphs = text
            .Replace("\r\n", "\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var current = new StringBuilder();
        foreach (var paragraph in paragraphs)
        {
            foreach (var piece in SplitLong(paragraph))
            {
                if (current.Length > 0 && current.Length + piece.Length + 1 > _maxChars)
                {
                    chunks.Add(new DocumentChunk(documentId, fileName, chunks.Count, current.ToString()));
                    current.Clear();
                }

                if (current.Length > 0)
                {
                    current.Append('\n');
                }
                current.Append(piece);
            }
        }

        if (current.Length > 0)
        {
            chunks.Add(new DocumentChunk(documentId, fileName, chunks.Count, current.ToString()));
        }

        return chunks;
    }

    /// <summary>Teilt einen zu langen Absatz an Wortgrenzen.</summary>
    private IEnumerable<string> SplitLong(string paragraph)
    {
        if (paragraph.Length <= _maxChars)
        {
            yield return paragraph;
            yield break;
        }

        var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var buffer = new StringBuilder();
        foreach (var word in words)
        {
            if (buffer.Length > 0 && buffer.Length + word.Length + 1 > _maxChars)
            {
                yield return buffer.ToString();
                buffer.Clear();
            }

            if (buffer.Length > 0)
            {
                buffer.Append(' ');
            }
            buffer.Append(word);
        }

        if (buffer.Length > 0)
        {
            yield return buffer.ToString();
        }
    }
}
