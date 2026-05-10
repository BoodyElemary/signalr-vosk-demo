using System.ComponentModel.DataAnnotations;

namespace AiModelDemo.Core.Models;

/// <summary>
/// Request body for the combined raw-STT → structure → SOAP-note streaming endpoint.
/// </summary>
public class GenerateFromRawRequest
{
    /// <summary>
    /// The raw, unstructured transcription produced by the STT engine (e.g. Vosk).
    /// </summary>
    [Required]
    public string RawTranscription { get; set; } = string.Empty;

    /// <summary>
    /// Optional patient context (name, age, gender, medical history).
    /// </summary>
    public string? PatientContext { get; set; }

    /// <summary>
    /// Optional consultation type (e.g. "annual checkup", "urgent care", "follow-up").
    /// </summary>
    public string? ConsultationType { get; set; }
}
