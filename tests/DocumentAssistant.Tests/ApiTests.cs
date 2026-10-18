using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DocumentAssistant.Tests;

/// <summary>
/// End-to-End-Test der Web-API: Hochladen, Abfragen, Fragen stellen, Vollständigkeit prüfen.
/// </summary>
public class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Kompletter_Ablauf_ueber_die_API()
    {
        var caseId = $"AKTE-{Guid.NewGuid():N}";

        var upload = await Upload(caseId, "01_wohngeldantrag.txt", Samples.Antrag);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var created = await upload.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Antrag", created.GetProperty("type").GetString());

        await Upload(caseId, "02_verdienstbescheinigung.txt", Samples.Nachweis);

        var list = await _client.GetFromJsonAsync<JsonElement>($"/api/cases/{caseId}/documents");
        Assert.Equal(2, list.GetArrayLength());

        var ask = await _client.PostAsJsonAsync("/api/ask", new { question = "Wie hoch ist die Bruttokaltmiete?", caseId });
        ask.EnsureSuccessStatusCode();
        var answer = await ask.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("890,00", answer.GetProperty("text").GetString());

        var report = await _client.GetFromJsonAsync<JsonElement>($"/api/cases/{caseId}/completeness?caseType=Wohngeld");
        Assert.False(report.GetProperty("isComplete").GetBoolean());
        Assert.Equal("Vertrag", report.GetProperty("missing")[0].GetString());
    }

    [Fact]
    public async Task Nicht_unterstuetzter_Dateityp_wird_abgelehnt()
    {
        var response = await Upload("AKTE-X", "bild.png", "kein text");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Leere_Frage_wird_abgelehnt()
    {
        var response = await _client.PostAsJsonAsync("/api/ask", new { question = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private Task<HttpResponseMessage> Upload(string caseId, string fileName, string content)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        form.Add(file, "file", fileName);
        return _client.PostAsync($"/api/cases/{caseId}/documents", form);
    }
}
