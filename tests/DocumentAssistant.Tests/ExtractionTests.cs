using DocumentAssistant.Core.Extraction;
using Xunit;

namespace DocumentAssistant.Tests;

public class ExtractionTests
{
    private readonly MetadataExtractor _extractor = new();

    [Fact]
    public void Kerndaten_aus_Antrag()
    {
        var metadata = _extractor.Extract(Samples.Antrag);

        Assert.Equal(new DateOnly(2026, 9, 2), metadata.Datum);
        Assert.Equal("WG-2026-0815", metadata.Aktenzeichen);
        Assert.StartsWith("Max Mustermann", metadata.Absender);
    }

    [Fact]
    public void Kerndaten_aus_Rechnung()
    {
        var metadata = _extractor.Extract(Samples.Rechnung);

        Assert.Equal(new DateOnly(2026, 9, 15), metadata.Datum);
        Assert.Equal(112.40m, metadata.Betrag);
        Assert.Equal("Stadtwerke Beispielstadt GmbH", metadata.Absender);
    }

    [Theory]
    [InlineData("Gesamtbetrag: 1.234,56 €", 1234.56)]
    [InlineData("Summe EUR 99,90", 99.90)]
    public void Betrag_im_deutschen_Format(string text, double expected)
    {
        Assert.Equal((decimal)expected, MetadataExtractor.ExtractAmount(text));
    }

    [Fact]
    public void Ungueltiges_Datum_wird_uebersprungen()
    {
        Assert.Equal(new DateOnly(2026, 2, 28), MetadataExtractor.ExtractDate("31.02.2026 und 28.02.2026"));
    }

    [Fact]
    public void Leerer_Text_liefert_leere_Kerndaten()
    {
        var metadata = _extractor.Extract("   ");
        Assert.Null(metadata.Datum);
        Assert.Null(metadata.Aktenzeichen);
        Assert.Null(metadata.Absender);
        Assert.Null(metadata.Betrag);
    }
}
