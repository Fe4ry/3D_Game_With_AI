using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Ollama HTTP API ile asenkron iletişim kuran ve ILLMService sözleşmesini uygulayan servis bileşeni.
/// UnityWebRequest kullanarak ana döngüyü bloklamadan çalışır; timeout, HTTP ve bağlantı hatalarını yönetir.
/// </summary>
public class OllamaService : MonoBehaviour, ILLMService
{
    [Header("Ollama Bağlantı Ayarları")]
    [Tooltip("Ollama API generate uç noktası URL'i.")]
    [SerializeField] private string apiUrl = "http://localhost:11434/api/generate";

    [Tooltip("Kullanılacak Ollama model adı (örn: llama3.2).")]
    [SerializeField] private string modelName = "llama3.2";

    [Tooltip("İstek zaman aşımı süresi (saniye).")]
    [SerializeField] private float timeoutSeconds = 15f;

    [Header("Karakter / Sistem Rolü")]
    [TextArea(3, 6)]
    [Tooltip("Karakterin rolünü ve konuşma tarzını belirleyen sistem prompt'u.")]
    [SerializeField] private string systemPrompt = "Sen bir hapishane mahkûmusun. Adın Mahkum 1. Oyuncuyla Türkçe konuşuyorsun. Cevapların kısa, doğal, biraz temkinli ve en fazla 2 cümle olsun.";

    private bool isBusy = false;

    /// <summary>
    /// Servisin şu anda aktif bir istek yürütüp yürütmediği.
    /// </summary>
    public bool IsBusy => isBusy;

    /// <summary>
    /// Doğrudan bir coroutine başlatarak prompt gönderir.
    /// </summary>
    public void RequestResponse(string prompt, Action<string> onSuccess, Action<string> onError)
    {
        if (isBusy)
        {
            SafeInvokeError(onError, "Zaten devam eden bir düşünme/istek süreci var.");
            return;
        }

        StartCoroutine(GenerateResponse(prompt, onSuccess, onError));
    }

    /// <summary>
    /// LLM'e asenkron istek gönderen Unity coroutine metodu.
    /// </summary>
    public IEnumerator GenerateResponse(string prompt, Action<string> onSuccess, Action<string> onError)
    {
        if (isBusy)
        {
            SafeInvokeError(onError, "Zaten devam eden bir düşünme/istek süreci var.");
            yield break;
        }

        isBusy = true;

        try
        {
            OllamaRequest requestDto = new OllamaRequest(
                model: modelName,
                prompt: prompt,
                system: string.IsNullOrEmpty(systemPrompt) ? null : systemPrompt,
                stream: false
            );

            string jsonPayload = JsonUtility.ToJson(requestDto);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

            using (UnityWebRequest request = new UnityWebRequest(apiUrl, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = Mathf.Max(1, Mathf.RoundToInt(timeoutSeconds));

                yield return request.SendWebRequest();

                HandleResponse(request, onSuccess, onError);
            }
        }
        finally
        {
            // İstek hangi nedenle sonlanırsa sonlansın isBusy durumunu daima sıfırla
            isBusy = false;
        }
    }

    /// <summary>
    /// UnityWebRequest tamamlandığında yanıtı ve olası hata senaryolarını ayrıştırır.
    /// </summary>
    private void HandleResponse(UnityWebRequest request, Action<string> onSuccess, Action<string> onError)
    {
        switch (request.result)
        {
            case UnityWebRequest.Result.ConnectionError:
                if (request.error != null && request.error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    SafeInvokeError(onError, "Ollama yanıt vermedi (Zaman aşımı oluştu).");
                }
                else
                {
                    SafeInvokeError(onError, "Ollama servisine bağlanılamadı. Lütfen Ollama uygulamasının açık olduğundan emin olun.");
                }
                break;

            case UnityWebRequest.Result.ProtocolError:
                if (request.responseCode == 404)
                {
                    SafeInvokeError(onError, $"'{modelName}' modeli Ollama üzerinde bulunamadı. Lütfen modeli indirin (ollama pull {modelName}).");
                }
                else
                {
                    SafeInvokeError(onError, $"Ollama sunucu hatası (HTTP {request.responseCode}): {request.error}");
                }
                break;

            case UnityWebRequest.Result.DataProcessingError:
                SafeInvokeError(onError, $"Ollama veri işleme hatası: {request.error}");
                break;

            case UnityWebRequest.Result.Success:
                string rawText = request.downloadHandler != null ? request.downloadHandler.text : null;

                if (string.IsNullOrWhiteSpace(rawText))
                {
                    SafeInvokeError(onError, "Ollama boş bir yanıt döndürdü.");
                    return;
                }

                // 1. JSON ayrıştırma işlemi izole edilir
                OllamaResponse responseDto = null;
                try
                {
                    responseDto = JsonUtility.FromJson<OllamaResponse>(rawText);
                }
                catch (Exception ex)
                {
                    SafeInvokeError(onError, $"Yanıt ayrıştırılamadı (JSON Hatası): {ex.Message}");
                    return;
                }

                // 2. Yanıt alanlarının doluluğu kontrol edilir
                if (responseDto == null || string.IsNullOrWhiteSpace(responseDto.response))
                {
                    SafeInvokeError(onError, "Ollama geçerli bir metin yanıtı üretemedi.");
                    return;
                }

                // 3. Başarı callback'i JSON bloğunun dışına alınarak izole edilir
                // onSuccess hata fırlatsa bile bu durum JSON hatası olarak onError'a yönlendirilmez
                try
                {
                    onSuccess?.Invoke(responseDto.response.Trim());
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, this);
                }
                break;

            default:
                SafeInvokeError(onError, "Bilinmeyen bir istek hatası oluştu.");
                break;
        }
    }

    /// <summary>
    /// Hata callback'ini güvenli bir şekilde çağırır; callback içinde exception oluşsa bile servisi korur.
    /// </summary>
    private void SafeInvokeError(Action<string> onError, string errorMessage)
    {
        try
        {
            onError?.Invoke(errorMessage);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
        }
    }
}
