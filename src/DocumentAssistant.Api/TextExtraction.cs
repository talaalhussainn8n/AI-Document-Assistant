using System.Text;
using UglyToad.PdfPig;

namespace DocumentAssistant.Api;

/// <summary>
/// Liest den Text aus hochgeladenen Dateien (PDF oder reiner Text).
/// </summary>
public static class TextExtraction
{
    public static readonly string[] SupportedExtensions = { ".pdf", ".txt", ".md" };

    public static async Task<string> ExtractAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        return extension switch
        {
            ".pdf" => ExtractPdf(buffer),
            ".txt" or ".md" => Encoding.UTF8.GetString(buffer.ToArray()),
            _ => throw new NotSupportedException($"Dateityp {extension} wird nicht unterstützt.")
        };
    }

    private static string ExtractPdf(Stream stream)
    {
        using var pdf = PdfDocument.Open(stream);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
        {
            sb.AppendLine(page.Text);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
