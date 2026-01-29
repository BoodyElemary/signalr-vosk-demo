using AiModelDemo.Core.Interfaces;
using Vosk;

namespace AiModelDemo.Infrastructure.Speech;

public class VoskSpeechRecognitionService : ISpeechRecognitionService
{
    private readonly Dictionary<string, Model> _models = new();
    private readonly string _modelsPath;
    private const float SampleRate = 16000f;

    public VoskSpeechRecognitionService(IConfiguration configuration)
    {
        _modelsPath = configuration["Vosk:ModelsPath"] ?? "Models/Vosk";
        Vosk.Vosk.SetLogLevel(0);
    }

    public VoskRecognizer CreateRecognizer(string language)
    {
        var model = GetOrLoadModel(language);
        return new VoskRecognizer(model, SampleRate);
    }

    private Model GetOrLoadModel(string language)
    {
        if (_models.TryGetValue(language, out var existingModel))
        {
            return existingModel;
        }

        var modelPath = Path.Combine(_modelsPath, language);
        
        if (!Directory.Exists(modelPath))
        {
            throw new InvalidOperationException($"Model not found for language: {language}. Expected path: {modelPath}");
        }

        var model = new Model(modelPath);
        _models[language] = model;
        return model;
    }
}