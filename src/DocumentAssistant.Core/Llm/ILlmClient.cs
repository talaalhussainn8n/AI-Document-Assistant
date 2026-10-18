namespace DocumentAssistant.Core.Llm;

/// <summary>
/// Abstraktion für ein Sprachmodell (LLM). Ermöglicht den Austausch des Anbieters
/// (OpenAI, Azure OpenAI über kompatiblen Endpunkt, lokal mit Ollama) und einfache Tests.
/// </summary>
public interface ILlmClient
{
    /// <summary>True, wenn ein Endpunkt konfiguriert ist und das Modell genutzt werden kann.</summary>
    bool IsConfigured { get; }

    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
}

/// <summary>
/// Platzhalter, wenn kein LLM konfiguriert ist. Die Anwendung nutzt dann die regelbasierten Verfahren.
/// </summary>
public sealed class DisabledLlmClient : ILlmClient
{
    public bool IsConfigured => false;

    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Es ist kein LLM-Endpunkt konfiguriert.");
}
