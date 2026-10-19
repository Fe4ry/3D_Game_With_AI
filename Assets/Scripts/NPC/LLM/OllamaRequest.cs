using System;

/// <summary>
/// Ollama /api/generate uç noktasına gönderilecek JSON istek veri modeli (DTO).
/// </summary>
[Serializable]
public class OllamaRequest
{
    public string model;
    public string prompt;
    public string system;
    public bool stream;

    public OllamaRequest()
    {
        stream = false;
    }

    public OllamaRequest(string model, string prompt, string system = null, bool stream = false)
    {
        this.model = model;
        this.prompt = prompt;
        this.system = system;
        this.stream = stream;
    }
}

