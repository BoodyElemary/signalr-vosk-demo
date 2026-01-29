namespace AiModelDemo.Core.Models;

public class VoskPartialResult
{
    [System.Text.Json.Serialization.JsonPropertyName("partial")]
    public string Partial { get; set; } = string.Empty;
}