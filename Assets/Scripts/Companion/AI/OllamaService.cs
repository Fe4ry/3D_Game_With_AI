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

    [Serializable]
    public class OllamaErrorInfo
    {
        public string category;
        public string message;
        public string details;
        public string model;
        public string url;
    }

    private string CreateErrorJson(
        string category,
        string message,
        string details)
    {
        OllamaErrorInfo info = new OllamaErrorInfo
        {
            category = category,
            message = message,
            details = details,
            model = modelName,
            url = ollamaUrl
        };

        string errorJson = JsonUtility.ToJson(info);

        // Teknik ayrıntılar Unity Console'da JSON olarak görünür.
        Debug.LogError(errorJson, this);

        return errorJson;
    }

    private string DetectRequestError(
        string requestError,
        string responseBody)
    {
        string combined =
            (requestError + " " + responseBody).ToLowerInvariant();

        if (combined.Contains("timed out") ||
            combined.Contains("timeout") ||
            combined.Contains("time out"))
        {
            return "TIMEOUT";
        }

        if (combined.Contains("out of memory") ||
            combined.Contains("memory allocation") ||
            combined.Contains("failed to load model") ||
            combined.Contains("model runner has unexpectedly stopped") ||
            combined.Contains("not enough memory") ||
            combined.Contains("cuda error"))
        {
            return "MODEL_MEMORY";
        }

        return "CONNECTION";
    }

    private string GetRequestErrorMessage(string category)
    {
        switch (category)
        {
            case "TIMEOUT":
                return "Ollama isteği zaman aşımına uğradı.";

            case "MODEL_MEMORY":
                return "Ollama model veya bellek hatası verdi.";

            default:
                return "Ollama sunucusuna bağlanılamadı.";
        }
    }

    private string CreateRequestError(
        string requestError,
        string responseBody)
    {
        string category =
            DetectRequestError(requestError, responseBody);

        return CreateErrorJson(
            category,
            GetRequestErrorMessage(category),
            $"RequestError: {requestError}\nResponse: {responseBody}"
        );
    }

    private string CreateModelError(string details)
    {
        string lowerDetails = details.ToLowerInvariant();

        bool memoryRelated =
            lowerDetails.Contains("memory") ||
            lowerDetails.Contains("out of memory") ||
            lowerDetails.Contains("failed to load model") ||
            lowerDetails.Contains("model runner has unexpectedly stopped") ||
            lowerDetails.Contains("cuda error");

        string category = memoryRelated
            ? "MODEL_MEMORY"
            : "MODEL_ERROR";

        return CreateErrorJson(
            category,
            memoryRelated
                ? "Ollama model veya bellek hatası verdi."
                : "Ollama model hatası verdi.",
            details
        );
    }

    // Normal NPC diyalogları ve kararları.
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

            string responseBody =
                request.downloadHandler != null
                    ? request.downloadHandler.text
                    : "";

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(CreateRequestError(
                    request.error,
                    responseBody));

                yield break;
            }

            OllamaResponse ollamaResponse;

            try
            {
                ollamaResponse =
                    JsonUtility.FromJson<OllamaResponse>(responseBody);
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
                    responseBody);
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(ollamaResponse.error))
            {
                onError?.Invoke(
                    CreateModelError(ollamaResponse.error));
                yield break;
            }

            if (string.IsNullOrWhiteSpace(ollamaResponse.response))
            {
                onError?.Invoke(
                    "Ollama response alanı boş. Ham yanıt:\n" +
                    responseBody);
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

            decision.action =
                decision.action.Trim().ToUpperInvariant();

            if (decision.action == "PERSUADED")
            {
                decision.action = decision.persuaded
                    ? "FOLLOW"
                    : "STAY";
            }

            if (decision.action == "FOLLOWING")
                decision.action = "FOLLOW";

            if (decision.action == "WAIT" ||
                decision.action == "REMAIN" ||
                decision.action == "NOT_PERSUADED")
            {
                decision.action = "STAY";
            }

            if (decision.action != "FOLLOW" &&
                decision.action != "STAY")
            {
                onError?.Invoke(
                    $"Geçersiz NPC kararı: {decision.action}\n" +
                    "Beklenen değerler: FOLLOW veya STAY.");
                yield break;
            }

            onSuccess?.Invoke(decision);
        }
    }

    // Hakaret durumunda özlü söz üretimi.
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

            string responseBody =
                request.downloadHandler != null
                    ? request.downloadHandler.text
                    : "";

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(CreateRequestError(
                    request.error,
                    responseBody));

                yield break;
            }

            OllamaResponse ollamaResponse;

            try
            {
                ollamaResponse =
                    JsonUtility.FromJson<OllamaResponse>(responseBody);
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
                    responseBody);
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(ollamaResponse.error))
            {
                onError?.Invoke(
                    CreateModelError(ollamaResponse.error));
                yield break;
            }

            if (string.IsNullOrWhiteSpace(ollamaResponse.response))
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
