using DocumentAssistant.Core.Models;

namespace DocumentAssistant.Core.Classification;

public interface IDocumentClassifier
{
    Task<ClassificationResult> ClassifyAsync(string text, CancellationToken cancellationToken = default);
}
