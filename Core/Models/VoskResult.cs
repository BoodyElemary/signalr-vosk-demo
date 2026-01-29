namespace AiModelDemo.Core.Models;

public class VoskResult
{
    [System.Text.Json.Serialization.JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}
