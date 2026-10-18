using System.Text;
using System.Text.RegularExpressions;

namespace DocumentAssistant.Core.Retrieval;

public interface IEmbeddingProvider
{
    float[] Embed(string text);
}

/// <summary>
/// Lokale, deterministische Vektorisierung über Feature Hashing (Wörter und Wortpaare).
/// Benötigt keinen externen Dienst und macht die semantische Suche reproduzierbar testbar.
/// </summary>
public sealed partial class HashingEmbeddingProvider : IEmbeddingProvider
{
    private readonly int _dimensions;

    public HashingEmbeddingProvider(int dimensions = 512)
    {
        _dimensions = dimensions;
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex TokenRegex();

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "der", "die", "das", "und", "oder", "ein", "eine", "einer", "eines", "dem", "den", "des",
        "ist", "sind", "wird", "werden", "zu", "zum", "zur", "im", "in", "mit", "von", "für", "auf",
        "an", "am", "als", "auch", "es", "sie", "wir", "ich", "nicht", "bei", "nach", "aus", "wie",
        "welche", "welcher", "welches", "was", "wann", "wo", "wer", "hat", "haben", "bis"
    };

    public static IReadOnlyList<string> Tokenize(string text) =>
        TokenRegex().Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(t => t.Length > 1 && !StopWords.Contains(t))
            .ToList();

    public float[] Embed(string text)
    {
        var vector = new float[_dimensions];
        var tokens = Tokenize(text);

        for (var i = 0; i < tokens.Count; i++)
        {
            Add(vector, tokens[i], 1f);
            if (i + 1 < tokens.Count)
            {
                Add(vector, tokens[i] + "_" + tokens[i + 1], 0.5f);
            }
        }

        Normalize(vector);
        return vector;
    }

    private void Add(float[] vector, string feature, float weight)
    {
        var hash = StableHash(feature);
        var index = (int)(hash % (uint)_dimensions);
        var sign = (hash & 0x80000000) == 0 ? 1f : -1f;
        vector[index] += sign * weight;
    }

    /// <summary>FNV-1a: stabil über Prozesse hinweg, anders als string.GetHashCode.</summary>
    private static uint StableHash(string value)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            hash ^= b;
            hash *= prime;
        }
        return hash;
    }

    private static void Normalize(float[] vector)
    {
        var norm = Math.Sqrt(vector.Sum(v => (double)v * v));
        if (norm == 0)
        {
            return;
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(vector[i] / norm);
        }
    }

    public static double Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("Vektoren müssen gleich lang sein.");
        }

        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        return na == 0 || nb == 0 ? 0 : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }
}
