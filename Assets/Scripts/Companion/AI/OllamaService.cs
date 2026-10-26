
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class OllamaService : MonoBehaviour
{
    [Header("Ollama Ayarları")]
    [SerializeField] private string ollamaUrl =
        "http://localhost:11434/api/generate";

    [SerializeField] private string modelName = "gemma3:4b";

    [Serializable]
    public class NPCDecision
    {
        public string action;
        public string dialogue;
        public bool persuaded;
    }

    [Serializable]
    public class QuoteResponse
    {
        public string quote;
    }

    [Serializable]
    private class OllamaRequest
    {
        public string model;
        public string prompt;
        public bool stream;
        public string format;
    }

    [Serializable]
    private class OllamaResponse
    {
        public string response;
        public string error;
    }

    // Normal NPC diyalogları ve kararları için kullanılır.
    public IEnumerator GetDecision(
        string prompt,
        Action<NPCDecision> onSuccess,
        Action<string> onError)
    {
        OllamaRequest requestData = new OllamaRequest
        {
            model = modelName,
            prompt = prompt,
            stream = false,
            format = "json"
        };

        string json = JsonUtility.ToJson(requestData);

        using (UnityWebRequest request =
               new UnityWebRequest(ollamaUrl, "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);

            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader(
                "Content-Type", "application/json");
            request.timeout = 60;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(
                    $"Ollama bağlantı hatası: {request.error}\n" +
                    request.downloadHandler.text);
                yield break;
            }

            OllamaResponse ollamaResponse;

            try
            {
                ollamaResponse = JsonUtility.FromJson<OllamaResponse>(
                    request.downloadHandler.text);
            }
            catch (Exception e)
            {
                onError?.Invoke(
                    $"Ollama yanıtı okunamadı: {e.Message}");
                yield break;
            }

           
if (ollamaResponse == null)
{
    onError?.Invoke(
        "Ollama yanıtı okunamadı. Ham yanıt:\n" +
        request.downloadHandler.text);
    yield break;
}

if (!string.IsNullOrWhiteSpace(ollamaResponse.error))
{
    onError?.Invoke(
        "Ollama model hatası: " + ollamaResponse.error +
        "\nHam yanıt:\n" + request.downloadHandler.text);
    yield break;
}

if (string.IsNullOrWhiteSpace(ollamaResponse.response))
{
    onError?.Invoke(
        "Ollama response alanı boş. Ham yanıt:\n" +
        request.downloadHandler.text);
    yield break;
}


            NPCDecision decision;

            try
            {
                decision = JsonUtility.FromJson<NPCDecision>(
                    ollamaResponse.response);
            }
            catch (Exception e)
            {
                onError?.Invoke(
                    $"NPC kararı okunamadı: {e.Message}");
                yield break;
            }

            if (decision == null ||
                string.IsNullOrWhiteSpace(decision.action) ||
                string.IsNullOrWhiteSpace(decision.dialogue))
            {
                onError?.Invoke(
                    "NPC kararı eksik. action ve dialogue gerekli.");
                yield break;
            }

            decision.action = decision.action.Trim().ToUpperInvariant();

            if (decision.action != "FOLLOW" &&
                decision.action != "STAY")
            {
                onError?.Invoke(
                    $"Geçersiz NPC kararı: {decision.action}");
                yield break;
            }

            onSuccess?.Invoke(decision);
        }
    }

    // Hakaret durumunda yalnızca özlü söz üretmek için kullanılır.
    public IEnumerator GetQuote(
        string prompt,
        Action<string> onSuccess,
        Action<string> onError)
    {
        OllamaRequest requestData = new OllamaRequest
        {
            model = modelName,
            prompt = prompt,
            stream = false,
            format = "json"
        };

        string json = JsonUtility.ToJson(requestData);

        using (UnityWebRequest request =
               new UnityWebRequest(ollamaUrl, "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);

            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader(
                "Content-Type", "application/json");
            request.timeout = 60;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(
                    $"Özlü söz isteği başarısız: {request.error}\n" +
                    request.downloadHandler.text);
                yield break;
            }

            OllamaResponse ollamaResponse;

            try
            {
                ollamaResponse = JsonUtility.FromJson<OllamaResponse>(
                    request.downloadHandler.text);
            }
            catch (Exception e)
            {
                onError?.Invoke(
                    $"Ollama yanıtı okunamadı: {e.Message}");
                yield break;
            }

            if (ollamaResponse == null ||
                string.IsNullOrWhiteSpace(ollamaResponse.response))
            {
                onError?.Invoke(
                    "Ollama boş özlü söz yanıtı döndürdü.");
                yield break;
            }

            QuoteResponse quoteResponse;

            try
            {
                quoteResponse = JsonUtility.FromJson<QuoteResponse>(
                    ollamaResponse.response);
            }
            catch (Exception e)
            {
                onError?.Invoke(
                    $"Özlü söz yanıtı okunamadı: {e.Message}");
                yield break;
            }

            if (quoteResponse == null ||
                string.IsNullOrWhiteSpace(quoteResponse.quote))
            {
                onError?.Invoke(
                    "Özlü söz yanıtı geçersiz veya boş.");
                yield break;
            }

            onSuccess?.Invoke(quoteResponse.quote.Trim());
        }
    }
}
