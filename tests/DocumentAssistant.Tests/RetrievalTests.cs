using DocumentAssistant.Core.Llm;
using DocumentAssistant.Core.Models;
using DocumentAssistant.Core.Retrieval;

namespace DocumentAssistant.Tests;

public class RetrievalTests
{
    private readonly HashingEmbeddingProvider _embeddings = new();

    [Fact]
    public void Chunker_teilt_lange_Texte_und_haelt_Grenze_ein()
    {
        var chunker = new TextChunker(maxChars: 100);
        var text = string.Join("\n\n", Enumerable.Range(1, 10).Select(i => $"Absatz {i} mit etwas Inhalt zum Testen der Zerlegung."));

        var chunks = chunker.Chunk(Guid.NewGuid(), "test.txt", text);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Text.Length <= 100));
        Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select(c => c.Index));
    }

    [Fact]
    public void Chunker_teilt_ueberlangen_Absatz_an_Wortgrenzen()
    {
        var chunker = new TextChunker(maxChars: 60);
        var longParagraph = string.Join(' ', Enumerable.Repeat("Verwaltungsdigitalisierung", 20));

        var chunks = chunker.Chunk(Guid.NewGuid(), "lang.txt", longParagraph);

        Assert.All(chunks, c => Assert.True(c.Text.Length <= 60));
        Assert.All(chunks, c => Assert.DoesNotContain("  ", c.Text));
    }

    [Fact]
    public void Embedding_ist_normiert_und_deterministisch()
    {
        var a = _embeddings.Embed("Antrag auf Wohngeld");
        var b = _embeddings.Embed("Antrag auf Wohngeld");

        Assert.Equal(a, b);
        Assert.Equal(1.0, Math.Sqrt(a.Sum(v => (double)v * v)), precision: 5);
    }

    [Fact]
    public void Aehnliche_Texte_sind_naeher_als_fremde()
    {
        var query = _embeddings.Embed("Wie hoch ist die Miete der Wohnung?");
        var related = _embeddings.Embed("Die Miete der Wohnung beträgt 890 Euro.");
        var unrelated = _embeddings.Embed("Der Stromverbrauch lag bei 312 kWh.");

        Assert.True(HashingEmbeddingProvider.Cosine(query, related) > HashingEmbeddingProvider.Cosine(query, unrelated));
    }

    [Fact]
    public void VectorStore_liefert_relevantesten_Abschnitt_zuerst()
    {
        var store = new InMemoryVectorStore();
        var docId = Guid.NewGuid();
        Add(store, docId, 0, "Die Bruttokaltmiete beträgt 890,00 Euro.");
        Add(store, docId, 1, "Der Gesamtbetrag der Stromrechnung ist zahlbar bis 30.09.2026.");

        var hits = store.Search(_embeddings.Embed("Wie hoch ist die Bruttokaltmiete?"), topK: 2);

        Assert.Equal(0, hits[0].Chunk.Index);
    }

    [Fact]
    public void VectorStore_respektiert_Filter()
    {
        var store = new InMemoryVectorStore();
        var allowed = Guid.NewGuid();
        var other = Guid.NewGuid();
        Add(store, other, 0, "Bruttokaltmiete 890 Euro");
        Add(store, allowed, 0, "Bruttokaltmiete 700 Euro");

        var hits = store.Search(_embeddings.Embed("Bruttokaltmiete"), 5, c => c.DocumentId == allowed);

        Assert.Single(hits);
        Assert.Equal(allowed, hits[0].Chunk.DocumentId);
    }

    [Fact]
    public async Task Rag_ohne_Llm_antwortet_extraktiv_mit_Quelle()
    {
        var store = new InMemoryVectorStore();
        Index(store, Guid.NewGuid(), "01_wohngeldantrag.txt", Samples.Antrag);
        Index(store, Guid.NewGuid(), "03_rechnung.txt", Samples.Rechnung);
        var rag = new RagService(store, _embeddings, new DisabledLlmClient());

        var answer = await rag.AskAsync("Wie hoch ist die Bruttokaltmiete?");

        Assert.False(answer.GeneratedByLlm);
        Assert.Contains("890,00", answer.Text);
        Assert.Contains("01_wohngeldantrag.txt", answer.Text);
    }

    [Fact]
    public async Task Rag_mit_Llm_uebergibt_Quellen_im_Prompt()
    {
        var store = new InMemoryVectorStore();
        Index(store, Guid.NewGuid(), "03_rechnung.txt", Samples.Rechnung);
        var llm = new FakeLlm("Die Rechnung ist bis 30.09.2026 zu zahlen [1].");
        var rag = new RagService(store, _embeddings, llm);

        var answer = await rag.AskAsync("Bis wann ist die Rechnung zahlbar?");

        Assert.True(answer.GeneratedByLlm);
        Assert.Contains("[1] 03_rechnung.txt", llm.LastPrompt);
        Assert.NotEmpty(answer.Sources);
    }

    [Fact]
    public async Task Rag_ohne_Treffer_meldet_das_ehrlich()
    {
        var rag = new RagService(new InMemoryVectorStore(), _embeddings, new DisabledLlmClient());
        var answer = await rag.AskAsync("Wie hoch ist die Miete?");

        Assert.Empty(answer.Sources);
        Assert.Contains("nichts gefunden", answer.Text);
    }

    private void Add(IVectorStore store, Guid docId, int index, string text) =>
        store.Add(new DocumentChunk(docId, "test.txt", index, text), _embeddings.Embed(text));

    private void Index(IVectorStore store, Guid docId, string fileName, string text)
    {
        foreach (var chunk in new TextChunker().Chunk(docId, fileName, text))
        {
            store.Add(chunk, _embeddings.Embed(chunk.Text));
        }
    }
}
