using AiModelDemo.Core.Interfaces;
using AiModelDemo.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace AiModelDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SOAPNotesController : ControllerBase
{
    private readonly ILLMService _llmService;
    private readonly ILogger<SOAPNotesController> _logger;

    public SOAPNotesController(ILLMService llmService, ILogger<SOAPNotesController> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    /// <summary>
    /// Generate a structured SOAP note from transcription text
    /// </summary>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(SOAPNote), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SOAPNote>> GenerateSOAPNote(
        [FromBody] GenerateSOAPNoteRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Transcription))
        {
            return BadRequest(new { message = "Transcription is required" });
        }

        // Check if Ollama is available
        var isAvailable = await _llmService.IsAvailableAsync(cancellationToken);
        if (!isAvailable)
        {
            return StatusCode(503, new { message = "Ollama service is not available. Please ensure Ollama is running." });
        }

        try
        {
            _logger.LogInformation("Generating SOAP note from transcription");

            var soapNote = await _llmService.GenerateSOAPNoteAsync(
                request.Transcription,
                request.PatientContext,
                request.ConsultationType,
                cancellationToken);

            return Ok(soapNote);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating SOAP note");
            return StatusCode(500, new { message = "Failed to generate SOAP note", error = ex.Message });
        }
    }

    /// <summary>
    /// Check if the Ollama LLM service is available
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> CheckHealth(CancellationToken cancellationToken)
    {
        var isAvailable = await _llmService.IsAvailableAsync(cancellationToken);
        return Ok(isAvailable);
    }
}
