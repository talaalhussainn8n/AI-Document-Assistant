using DocumentAssistant.Core.Completeness;
using DocumentAssistant.Core.Models;

namespace DocumentAssistant.Tests;

public class CompletenessTests
{
    private readonly CompletenessChecker _checker = new();

    [Fact]
    public void Fehlender_Mietvertrag_wird_gemeldet()
    {
        var documents = new[] { Doc(DocumentType.Antrag), Doc(DocumentType.Nachweis) };

        var report = _checker.Check("Wohngeld", documents);

        Assert.False(report.IsComplete);
        Assert.Equal(new[] { DocumentType.Vertrag }, report.Missing);
    }

    [Fact]
    public void Vollstaendige_Akte_wird_erkannt()
    {
        var documents = new[] { Doc(DocumentType.Antrag), Doc(DocumentType.Nachweis), Doc(DocumentType.Vertrag) };

        Assert.True(_checker.Check("wohngeld", documents).IsComplete);
    }

    [Fact]
    public void Unbekannter_Vorgangstyp_wirft_Fehler()
    {
        Assert.Throws<ArgumentException>(() => _checker.Check("Unbekannt", Array.Empty<Document>()));
    }

    private static Document Doc(DocumentType type) => new()
    {
        CaseId = "AKTE-1",
        FileName = $"{type}.txt",
        Text = string.Empty,
        Classification = new ClassificationResult(type, 1, "Test"),
        Metadata = new DocumentMetadata(null, null, null, null)
    };
}
