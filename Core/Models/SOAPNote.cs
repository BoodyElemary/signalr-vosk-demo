namespace AiModelDemo.Core.Models;

public class SOAPNote
{
    /// <summary>
    /// Subjective - Patient's symptoms, complaints, and history
    /// </summary>
    public string Subjective { get; set; } = string.Empty;

    /// <summary>
    /// Objective - Physical exam findings, vital signs, test results
    /// </summary>
    public string Objective { get; set; } = string.Empty;

    /// <summary>
    /// Assessment - Diagnosis and clinical interpretation
    /// </summary>
    public string Assessment { get; set; } = string.Empty;

    /// <summary>
    /// Plan - Treatment plan, medications, follow-up instructions
    /// </summary>
    public string Plan { get; set; } = string.Empty;

    /// <summary>
    /// Generated timestamp
    /// </summary>
    public long GeneratedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// Model used for generation
    /// </summary>
    public string Model { get; set; } = string.Empty;
}
