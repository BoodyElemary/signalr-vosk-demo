namespace AiModelDemo.Core.Models;

/// <summary>
/// Combined result returned by the generate-from-raw endpoint.
/// Contains both the structured transcript (labeled speaker turns)
/// and the SOAP note — produced from a single Ollama prompt.
/// </summary>
public class GenerateFromRawResult
{
    /// <summary>Doctor/Patient labeled conversation turns.</summary>
    public StructuredTranscript StructuredTranscript { get; set; } = new();

    /// <summary>SOAP note extracted from the conversation.</summary>
    public SOAPNote SoapNote { get; set; } = new();
}
