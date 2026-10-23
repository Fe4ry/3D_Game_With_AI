using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Ollama API bağlantısını test eden yardımcı bileşen.
/// HTTP 500 veya bağlantı hatalarını normal akış içinde yakalar;
/// oyunu duraklatmadan anlaşılır uyarı mesajları üretir ve sahnedeki konuşma balonuna iletir.
/// </summary>
public class OllamaConnectionTest : MonoBehaviour
{
    [Header("Test Ayarları")]
    [Tooltip("Ollama API generate uç noktası URL'i.")]
    [SerializeField] private string url = "http://localhost:11434/api/generate";

    [Tooltip("Test edilecek model adı.")]
    [SerializeField] private string model = "llama3.2";

    [Tooltip("Test prompt metni.")]
    [SerializeField] private string prompt = "Say hello to Unity in one short sentence.";

    [Tooltip("İstek zaman aşımı süresi (saniye).")]
    [SerializeField] private float timeoutSeconds = 10f;

    [System.Serializable]
    private class OllamaErrorResponse
    {
        public string error;
    }

    private IEnumerator Start()
    {
        string json = $"{{\"model\":\"{model}\",\"prompt\":\"{prompt}\",\"stream\":false}}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);

            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = Mathf.Max(1, Mathf.RoundToInt(timeoutSeconds));

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler != null ? request.downloadHandler.text : null;

                try
                {
                    OllamaResponse result = JsonUtility.FromJson<OllamaResponse>(responseText);
                    Debug.Log($"[OllamaConnectionTest] Gemm3:4b: {result?.response}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[OllamaConnectionTest] Yanıt JSON ayrıştırma uyarısı: {ex.Message}\nHam Yanıt: {responseText}");
                }
            }
            else
            {
                // HTTP 500, 404 veya ağ hatalarını oyun akışını kesmeden normal hata durumu olarak ele al.
                // Debug.LogError yerine Debug.LogWarning kullanılır; böylece Console 'Error Pause' aktif olsa bile oyun duraklamaz.
                string responseBody = request.downloadHandler != null ? request.downloadHandler.text : null;
                string detailedError = ExtractOllamaErrorMessage(responseBody, request.error);

                string warningMessage =
                    $"[OllamaConnectionTest] Ollama isteği başarısız oldu\n" +
                    $"Result: {request.result}\n" +
                    $"Error: {request.error ?? "<none>"}\n" +
                    $"HTTP status: {request.responseCode}\n" +
                    $"URL: {GetSafeRequestUrl(request.url)}\n" +
                    $"Method: {request.method}\n" +
                    $"Model: {model}\n" +
                    $"Content-Type: application/json\n" +
                    $"Timeout: {request.timeout} seconds\n" +
                    $"Request body bytes: {body.Length}\n" +
                    $"Response body: {FormatLoggedText(responseBody)}";
                Debug.LogWarning(warningMessage, this);

                // Hatayı görsel olarak göstermek için sahnedeki mevcut konuşma balonunu kullan
                WorldSpaceBubblePresenter bubble = FindFirstObjectByType<WorldSpaceBubblePresenter>();
                if (bubble != null)
                {
                    bubble.ShowError($"Ollama (HTTP {request.responseCode}): {detailedError}");
                }
            }
        }
    }

    private static string GetSafeRequestUrl(string requestUrl)
    {
        if (Uri.TryCreate(requestUrl, UriKind.Absolute, out Uri uri))
        {
            string authority = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
            return $"{uri.Scheme}://{authority}{uri.AbsolutePath}";
        }

        return "<invalid URL>";
    }

    private static string FormatLoggedText(string text)
    {
        const int maxLength = 2000;

        if (string.IsNullOrEmpty(text))
        {
            return "<empty>";
        }

        if (text.Length <= maxLength)
        {
            return text;
        }

        return $"{text.Substring(0, maxLength)}...[truncated; original length {text.Length}]";
    }

    /// <summary>
    /// downloadHandler.text üzerinden Ollama'nın döndürdüğü gerçek JSON hata mesajını ayıklar.
    /// </summary>
    private string ExtractOllamaErrorMessage(string responseBody, string fallbackError)
    {
        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            try
            {
                OllamaErrorResponse errObj = JsonUtility.FromJson<OllamaErrorResponse>(responseBody);
                if (errObj != null && !string.IsNullOrWhiteSpace(errObj.error))
                {
                    return errObj.error;
                }
            }
            catch
            {
                // JSON ayrıştırılamazsa ham gövde metnini kullan
            }

            return responseBody.Trim();
        }

        return !string.IsNullOrWhiteSpace(fallbackError) ? fallbackError : "Bilinmeyen sunucu hatası.";
    }
}
