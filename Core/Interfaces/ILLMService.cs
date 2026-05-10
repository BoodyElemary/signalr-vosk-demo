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
    /// Stream raw tokens for SOAP note generation (SSE-friendly)
    /// </summary>
    IAsyncEnumerable<string> StreamGenerateSOAPNoteAsync(
        string transcription,
        string? patientContext = null,
        string? consultationType = null,
        CancellationToken cancellationToken = default);

    Task<StructuredTranscript> StructureTranscriptAsync(
        string transcription,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stream raw tokens for transcript structuring (SSE-friendly)
    /// </summary>
    IAsyncEnumerable<string> StreamStructureTranscriptAsync(
        string transcription,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Single-prompt pipeline: takes raw/fuzzy STT text and streams a SOAP note
    /// in one Ollama call. The model infers speaker roles internally.
    /// </summary>
    IAsyncEnumerable<string> StreamGenerateSOAPNoteFromRawAsync(
        string rawTranscription,
        string? patientContext = null,
        string? consultationType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the LLM service is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
