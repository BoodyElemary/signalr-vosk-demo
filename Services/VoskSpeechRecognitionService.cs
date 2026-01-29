using Vosk;

namespace AiModelDemo.Services;
public interface ISpeechRecognitionService
{
    Model GetOrCreateModel(string language);
    VoskRecognizer CreateRecognizer(string language, float sampleRate = 16000.0f);
    IEnumerable<string> GetAvailableLanguages();
}

public class VoskSpeechRecognitionService : ISpeechRecognitionService, IDisposable
{
    private readonly Dictionary<string, Model> _models = new();
    private readonly string _modelsBasePath;
    private readonly object _lock = new();

    public VoskSpeechRecognitionService(IConfiguration configuration)
    {
        _modelsBasePath = configuration.GetValue<string>("VoskModelsPath") 
            ?? Path.Combine(Directory.GetCurrentDirectory(), "models");
    }

    public Model GetOrCreateModel(string language)
    {
        lock (_lock)
        {
            if (_models.TryGetValue(language, out var existingModel))
            {
                return existingModel;
            }

            string modelPath = Path.Combine(_modelsBasePath, language);
            
            if (!Directory.Exists(modelPath))
            {
                throw new DirectoryNotFoundException($"Model not found for language: {language}. Path: {modelPath}");
            }

            var model = new Model(modelPath);
            _models[language] = model;
            return model;
        }
    }

    public VoskRecognizer CreateRecognizer(string language, float sampleRate = 16000.0f)
    {
        var model = GetOrCreateModel(language);
        var recognizer = new VoskRecognizer(model, sampleRate);
        recognizer.SetMaxAlternatives(0);
        recognizer.SetWords(true);
        return recognizer;
    }

    public IEnumerable<string> GetAvailableLanguages()
    {
        if (!Directory.Exists(_modelsBasePath))
        {
            return Enumerable.Empty<string>();
        }

        return Directory.GetDirectories(_modelsBasePath)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Cast<string>();
    }

    public void Dispose()
    {
        foreach (var model in _models.Values)
        {
            model.Dispose();
        }
        _models.Clear();
    }
}