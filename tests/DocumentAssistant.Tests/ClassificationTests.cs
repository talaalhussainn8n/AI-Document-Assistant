using DocumentAssistant.Core.Classification;
using DocumentAssistant.Core.Llm;
using DocumentAssistant.Core.Models;
using Xunit;

namespace DocumentAssistant.Tests;

public class ClassificationTests
{
    private readonly KeywordClassifier _classifier = new();

    [Fact]
    public void Wohngeldantrag_wird_als_Antrag_erkannt()
    {
        var result = _classifier.Classify(Samples.Antrag);
        Assert.Equal(DocumentType.Antrag, result.Type);
        Assert.InRange(result.Confidence, 0.01, 1.0);
    }

    [Fact]
    public void Verdienstbescheinigung_wird_als_Nachweis_erkannt()
    {
        Assert.Equal(DocumentType.Nachweis, _classifier.Classify(Samples.Nachweis).Type);
    }

    [Fact]
    public void Stromrechnung_wird_als_Rechnung_erkannt()
    {
        Assert.Equal(DocumentType.Rechnung, _classifier.Classify(Samples.Rechnung).Type);
    }

    [Fact]
    public void Text_ohne_Schluesselbegriffe_ist_Sonstiges()
    {
        var result = _classifier.Classify("Das Wetter ist heute sonnig.");
        Assert.Equal(DocumentType.Sonstiges, result.Type);
        Assert.Equal(0.0, result.Confidence);
    }

    [Theory]
    [InlineData("Bescheid", DocumentType.Bescheid)]
    [InlineData(" rechnung. ", DocumentType.Rechnung)]
    [InlineData("\"Vertrag\"", DocumentType.Vertrag)]
    public void Llm_Antwort_wird_robust_gelesen(string raw, DocumentType expected)
    {
        Assert.True(LlmClassifier.TryParse(raw, out var type));
        Assert.Equal(expected, type);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ich weiß es nicht")]
    [InlineData("42")]
    public void Unbrauchbare_Llm_Antwort_wird_abgelehnt(string raw)
    {
        Assert.False(LlmClassifier.TryParse(raw, out _));
    }

    [Fact]
    public async Task Ohne_Llm_wird_regelbasiert_klassifiziert()
    {
        var classifier = new LlmClassifier(new DisabledLlmClient(), _classifier);
        var result = await classifier.ClassifyAsync(Samples.Rechnung);
        Assert.Equal(DocumentType.Rechnung, result.Type);
    }

    [Fact]
    public async Task Mit_Llm_wird_dessen_Antwort_genutzt()
    {
        var classifier = new LlmClassifier(new FakeLlm("Vertrag"), _classifier);
        var result = await classifier.ClassifyAsync(Samples.Rechnung);
        Assert.Equal(DocumentType.Vertrag, result.Type);
    }
}

internal sealed class FakeLlm : ILlmClient
{
    private readonly string _answer;
    public FakeLlm(string answer) => _answer = answer;
    public bool IsConfigured => true;
    public string? LastPrompt { get; private set; }

    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        LastPrompt = userPrompt;
        return Task.FromResult(_answer);
    }
}
