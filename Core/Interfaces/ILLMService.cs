using AiModelDemo.Core.Models;

namespace AiModelDemo.Core.Interfaces;

public interface ILLMService
{
    /// <summary>
    /// Generate a structured SOAP note from transcription text
    /// </summary>
    Task<SOAPNote> GenerateSOAPNoteAsync(
        string transcription,
        string? patientContext = null,
        string? consultationType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the LLM service is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
