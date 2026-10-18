using DocumentAssistant.Api;
using DocumentAssistant.Core;
using DocumentAssistant.Core.Classification;
using DocumentAssistant.Core.Completeness;
using DocumentAssistant.Core.Extraction;
using DocumentAssistant.Core.Llm;
using DocumentAssistant.Core.Retrieval;

var builder = WebApplication.CreateBuilder(args);

// Sprachmodell: über appsettings.json oder Umgebungsvariablen (Llm__BaseUrl, Llm__ApiKey, ...).
var llmOptions = builder.Configuration.GetSection("Llm").Get<LlmOptions>() ?? new LlmOptions();
builder.Services.AddSingleton(llmOptions);
builder.Services.AddHttpClient<ILlmClient, OpenAiCompatibleClient>(client => client.Timeout = TimeSpan.FromSeconds(60));

builder.Services.AddSingleton<KeywordClassifier>();
builder.Services.AddSingleton<IDocumentClassifier>(sp =>
    new LlmClassifier(sp.GetRequiredService<ILlmClient>(), sp.GetRequiredService<KeywordClassifier>()));
builder.Services.AddSingleton<MetadataExtractor>();
builder.Services.AddSingleton(new TextChunker(maxChars: 600));
builder.Services.AddSingleton<IEmbeddingProvider>(new HashingEmbeddingProvider());
builder.Services.AddSingleton<IVectorStore, InMemoryVectorStore>();
builder.Services.AddSingleton<RagService>();
builder.Services.AddSingleton<CompletenessChecker>(_ => new CompletenessChecker());
builder.Services.AddSingleton<DocumentService>();

// Enums als Text ausgeben (z. B. "Antrag" statt 1).
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

api.MapPost("/cases/{caseId}/documents", async (string caseId, IFormFile file, DocumentService service, CancellationToken ct) =>
{
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!TextExtraction.SupportedExtensions.Contains(extension))
    {
        return Results.BadRequest(new { error = $"Nur {string.Join(", ", TextExtraction.SupportedExtensions)} werden unterstützt." });
    }

    var text = await TextExtraction.ExtractAsync(file, ct);
    var document = await service.IngestAsync(caseId, file.FileName, text, ct);
    return Results.Created($"/api/documents/{document.Id}", ToDto(document));
})
.DisableAntiforgery();

api.MapGet("/cases/{caseId}/documents", (string caseId, DocumentService service) =>
    Results.Ok(service.GetAll(caseId).Select(ToDto)));

api.MapGet("/documents/{id:guid}", (Guid id, DocumentService service) =>
    service.Get(id) is { } document ? Results.Ok(ToDto(document)) : Results.NotFound());

api.MapPost("/ask", async (AskRequest request, DocumentService service, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
    {
        return Results.BadRequest(new { error = "Bitte eine Frage angeben." });
    }

    var answer = await service.AskAsync(request.Question, request.CaseId, ct);
    return Results.Ok(new
    {
        answer.Text,
        answer.GeneratedByLlm,
        Sources = answer.Sources.Select(s => new { s.Chunk.FileName, s.Chunk.Text, Score = Math.Round(s.Score, 3) })
    });
});

api.MapGet("/cases/{caseId}/completeness", (string caseId, string caseType, DocumentService service) =>
{
    try
    {
        return Results.Ok(service.CheckCompleteness(caseId, caseType));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

api.MapGet("/case-types", (CompletenessChecker checker) => Results.Ok(checker.CaseTypes));

app.Run();

static object ToDto(DocumentAssistant.Core.Models.Document d) => new
{
    d.Id,
    d.CaseId,
    d.FileName,
    Type = d.Classification.Type.ToString(),
    d.Classification.Confidence,
    d.Classification.Reason,
    d.Metadata,
    d.UploadedAt
};

public sealed record AskRequest(string Question, string? CaseId);

// Für Integrationstests mit WebApplicationFactory.
public partial class Program;
