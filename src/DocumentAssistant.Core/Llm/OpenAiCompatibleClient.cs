using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DocumentAssistant.Core.Llm;

/// <summary>
/// Konfiguration für einen OpenAI-kompatiblen Endpunkt.
/// Beispiele: https://api.openai.com/v1 oder lokal mit Ollama http://localhost:11434/v1
/// </summary>
public sealed class LlmOptions
{
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string ChatModel { get; set; } = "gpt-4o-mini";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
}

/// <summary>
/// Schlanker Client für die Chat-Completions-API, ohne zusätzliche SDK-Abhängigkeit.
/// </summary>
public sealed class OpenAiCompatibleClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly LlmOptions _options;

    public OpenAiCompatibleClient(HttpClient http, LlmOptions options)
    {
        _http = http;
        _options = options;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.BaseUrl);

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Es ist kein LLM-Endpunkt konfiguriert.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl("chat/completions"));
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        request.Content = JsonContent.Create(new ChatRequest(
            _options.ChatModel,
            new[]
            {
                new ChatMessage("system", systemPrompt),
                new ChatMessage("user", userPrompt)
            },
            Temperature: 0.1));

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: cancellationToken);
        return body?.Choices.FirstOrDefault()?.Message.Content?.Trim() ?? string.Empty;
    }

    private string BuildUrl(string path) => $"{_options.BaseUrl!.TrimEnd('/')}/{path}";

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string? Content);

    private sealed record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
        [property: JsonPropertyName("temperature")] double Temperature);

    private sealed record ChatChoice([property: JsonPropertyName("message")] ChatMessage Message);

    private sealed record ChatResponse([property: JsonPropertyName("choices")] IReadOnlyList<ChatChoice> Choices);
}
