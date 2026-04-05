using System.ComponentModel.DataAnnotations;

namespace AiModelDemo.Core.Models;

public class GenerateSOAPNoteRequest
{
    /// <summary>
    /// The transcription text from the doctor-patient conversation
    /// </summary>
    [Required]
    public string Transcription { get; set; } = string.Empty;

    /// <summary>
    /// Optional patient context (name, age, gender, medical history)
    /// </summary>
    public string? PatientContext { get; set; }

    /// <summary>
    /// Optional consultation type (e.g., "annual checkup", "urgent care", "follow-up")
    /// </summary>
    public string? ConsultationType { get; set; }
}
