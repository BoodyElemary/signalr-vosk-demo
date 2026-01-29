using Vosk;

namespace AiModelDemo.Core.Interfaces;

public interface ISpeechRecognitionService
{
    VoskRecognizer CreateRecognizer(string language);
}
