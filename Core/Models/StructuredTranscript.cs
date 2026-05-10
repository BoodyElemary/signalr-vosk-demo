namespace AiModelDemo.Core.Models;
public class StructuredTranscript
{
    public List<TranscriptLine> Lines { get; set; } = new();
}

public class TranscriptLine
{
    public string Speaker { get; set; } = string.Empty; // "Doctor" or "Patient"
    public string Text { get; set; } = string.Empty;
}