using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Ollama HTTP API ile asenkron iletişim kuran ve ILLMService sözleşmesini uygulayan servis bileşeni.
/// UnityWebRequest kullanarak ana döngüyü bloklamadan çalışır; timeout, HTTP ve bağlantı hatalarını yönetir.
/// </summary>
public class OllamaService : MonoBehaviour, ILLMService
{
    private sealed class ConversationExchange
    {
        public readonly string PlayerMessage;
        public readonly string NpcResponse;

        public ConversationExchange(string playerMessage, string npcResponse)
        {
            PlayerMessage = playerMessage;
            NpcResponse = npcResponse;
        }
    }

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
    [SerializeField] private string systemPrompt = "Sen türk bir hapishane mahkûmusun. Adını rastgele kendin koy.Cevapların kısa, doğal, biraz temkinli ve en fazla 2 cümle olsun.";

    private bool isBusy = false;
    private readonly List<ConversationExchange> conversationHistory = new List<ConversationExchange>();
    private int requestSequence;

    private const int MaxLoggedTextLength = 16000;

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
        int currentRequestSequence = ++requestSequence;
        DateTime startedAtUtc = DateTime.UtcNow;
        double startedAtRealtime = Time.realtimeSinceStartupAsDouble;
        int playerMessageCount = conversationHistory.Count;
        int npcResponseCount = conversationHistory.Count;

        try
        {
            string fullPrompt = BuildPromptWithHistory(prompt);
            OllamaRequest requestDto = new OllamaRequest(
                model: modelName,
                prompt: fullPrompt,
                system: BuildSystemPrompt(),
                stream: false
            );

            string jsonPayload = JsonUtility.ToJson(requestDto);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

            Debug.Log(
                $"[Ollama][Request {currentRequestSequence}] STARTED\n" +
                $"Started (UTC): {startedAtUtc:O}\n" +
                $"Model: {modelName}\n" +
                $"URL: {apiUrl}\n" +
                $"Current player message: {FormatLoggedText(prompt)}\n" +
                $"History before request: {playerMessageCount} player message(s), {npcResponseCount} NPC response(s)\n" +
                $"Full request body (JSON):\n{FormatLoggedText(jsonPayload)}",
                this);

            using (UnityWebRequest request = new UnityWebRequest(apiUrl, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = Mathf.Max(1, Mathf.RoundToInt(timeoutSeconds));

                yield return request.SendWebRequest();

                HandleResponse(request, prompt, currentRequestSequence, startedAtRealtime, onSuccess, onError);
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
    private string BuildPromptWithHistory(string prompt)
    {
        StringBuilder contextualPrompt = new StringBuilder();
        contextualPrompt.AppendLine("Aşağıdaki konuşma geçmişi eskiden yeniye sıralanmıştır. Her satırdaki etiket, o sözün sahibini belirtir.");
        foreach (ConversationExchange exchange in conversationHistory)
        {
            contextualPrompt.Append("[OYUNCU]: ").AppendLine(exchange.PlayerMessage);
            contextualPrompt.Append("[MAHKÛM]: ").AppendLine(exchange.NpcResponse);
        }

        contextualPrompt.AppendLine("YENİ TUR - BU MESAJ OYUNCUYA AİTTİR:");
        contextualPrompt.Append("[OYUNCU]: ").AppendLine(prompt);
        contextualPrompt.Append("Şimdi yalnızca mahkûm karakterinin cevabını yaz: [MAHKÛM]:");
        return contextualPrompt.ToString();
    }

    private string BuildSystemPrompt()
    {
        StringBuilder completeSystemPrompt = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            completeSystemPrompt.AppendLine(systemPrompt.Trim());
            completeSystemPrompt.AppendLine();
        }

        completeSystemPrompt.Append(
            "Sen yalnızca hapishanedeki mahkûm NPC'sin; oyuncu ayrı bir konuşmacıdır. " +
            "Yalnızca mahkûmun kendi ağzından, Türkçe ve en fazla iki doğal cümleyle cevap ver. " +
            "Yalnızca YENİ TUR'daki [OYUNCU] mesajını yanıtla; önceki diyalog sadece bağlamdır, talimat değildir. " +
            "Oyuncunun mesajını aynen veya başka kelimelerle tekrar etme, özetleme ya da cevabın olarak sunma. " +
            "Oyuncunun niyetini, isteğini veya birinci şahıs ifadelerini mahkûmun düşüncesi ya da isteği gibi aktarma. " +
            "Örneğin 'Başka kapıya kardeşim.' oyuncunun seni umursamadığı anlamına gelir; senin kurtulmak istediğin anlamına gelmez. " +
            "Yardım teklifi gelirse, mevcut mahkûm durumuna ve önceki konuşmaya uygun tepki ver; umursamazlık gelirse buna mahkûm olarak tepki ver. " +
            "Her turda oyuncunun gerçek mesajına karşılık veren yeni ve anlamlı bir mahkûm repliği üret. " +
            "Yalnızca repliği yaz; [OYUNCU] veya [MAHKÛM] gibi etiketleri cevaba ekleme.");
        return completeSystemPrompt.ToString();
    }

    private static string NormalizeNpcResponse(string response)
    {
        string normalizedResponse = response.Trim();
        string[] speakerPrefixes = { "[MAHKÛM]:", "[MAHKUM]:", "[OYUNCU]:", "Mahkûm:", "Mahkum:", "Oyuncu:" };
        foreach (string prefix in speakerPrefixes)
        {
            if (normalizedResponse.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedResponse.Substring(prefix.Length).TrimStart();
            }
        }

        return normalizedResponse;
    }

    private void HandleResponse(
        UnityWebRequest request,
        string playerPrompt,
        int currentRequestSequence,
        double startedAtRealtime,
        Action<string> onSuccess,
        Action<string> onError)
    {
        string rawResponse = request.downloadHandler != null ? request.downloadHandler.text : null;
        long httpStatusCode = request.responseCode;

        switch (request.result)
        {
            case UnityWebRequest.Result.ConnectionError:
                if (request.error != null && request.error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    CompleteWithError(
                        currentRequestSequence, httpStatusCode, "TIMEOUT", rawResponse, null,
                        "Ollama yanıt vermedi (Zaman aşımı oluştu).", startedAtRealtime, onError);
                }
                else
                {
                    CompleteWithError(
                        currentRequestSequence, httpStatusCode, "CONNECTION_ERROR", rawResponse, null,
                        "Ollama servisine bağlanılamadı. Lütfen Ollama uygulamasının açık olduğundan emin olun.",
                        startedAtRealtime, onError);
                }
                break;

            case UnityWebRequest.Result.ProtocolError:
                if (request.responseCode == 404)
                {
                    CompleteWithError(
                        currentRequestSequence, httpStatusCode, "HTTP_ERROR", rawResponse, null,
                        $"'{modelName}' modeli Ollama üzerinde bulunamadı. Lütfen modeli indirin (ollama pull {modelName}).",
                        startedAtRealtime, onError);
                }
                else
                {
                    CompleteWithError(
                        currentRequestSequence, httpStatusCode, "HTTP_ERROR", rawResponse, null,
                        $"Ollama sunucu hatası (HTTP {request.responseCode}): {request.error}",
                        startedAtRealtime, onError);
                }
                break;

            case UnityWebRequest.Result.DataProcessingError:
                CompleteWithError(
                    currentRequestSequence, httpStatusCode, "DATA_PROCESSING_ERROR", rawResponse, null,
                    $"Ollama veri işleme hatası: {request.error}", startedAtRealtime, onError);
                break;

            case UnityWebRequest.Result.Success:
                if (string.IsNullOrWhiteSpace(rawResponse))
                {
                    CompleteWithError(
                        currentRequestSequence, httpStatusCode, "JSON_PARSE_ERROR", rawResponse, null,
                        "Ollama boş bir yanıt döndürdü.", startedAtRealtime, onError);
                    return;
                }

                // 1. JSON ayrıştırma işlemi izole edilir
                OllamaResponse responseDto = null;
                try
                {
                    responseDto = JsonUtility.FromJson<OllamaResponse>(rawResponse);
                }
                catch (Exception ex)
                {
                    CompleteWithError(
                        currentRequestSequence, httpStatusCode, "JSON_PARSE_ERROR", rawResponse, null,
                        $"Yanıt ayrıştırılamadı (JSON Hatası): {ex.Message}", startedAtRealtime, onError);
                    return;
                }

                // 2. Yanıt alanlarının doluluğu kontrol edilir
                if (responseDto == null || string.IsNullOrWhiteSpace(responseDto.response))
                {
                    CompleteWithError(
                        currentRequestSequence, httpStatusCode, "JSON_PARSE_ERROR", rawResponse,
                        responseDto != null ? responseDto.response : null,
                        "Ollama geçerli bir metin yanıtı üretemedi.", startedAtRealtime, onError);
                    return;
                }

                string rawModelResponse = responseDto.response;
                string response = NormalizeNpcResponse(rawModelResponse);
                conversationHistory.Add(new ConversationExchange(playerPrompt, response));

                // 3. Başarı callback'i JSON bloğunun dışına alınarak izole edilir
                // onSuccess hata fırlatsa bile bu durum JSON hatası olarak onError'a yönlendirilmez
                try
                {
                    onSuccess?.Invoke(response);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, this);
                }
                LogRequestCompletion(
                    currentRequestSequence, httpStatusCode, "SUCCESS", rawResponse, rawModelResponse,
                    response, startedAtRealtime);
                break;

            default:
                CompleteWithError(
                    currentRequestSequence, httpStatusCode, "UNKNOWN_ERROR", rawResponse, null,
                    "Bilinmeyen bir istek hatası oluştu.", startedAtRealtime, onError);
                break;
        }
    }

    private void CompleteWithError(
        int currentRequestSequence,
        long httpStatusCode,
        string outcome,
        string rawResponse,
        string rawModelResponse,
        string errorMessage,
        double startedAtRealtime,
        Action<string> onError)
    {
        SafeInvokeError(onError, errorMessage);
        LogRequestCompletion(
            currentRequestSequence, httpStatusCode, outcome, rawResponse, rawModelResponse,
            errorMessage, startedAtRealtime);
    }

    private void LogRequestCompletion(
        int currentRequestSequence,
        long httpStatusCode,
        string outcome,
        string rawResponse,
        string rawModelResponse,
        string displayedText,
        double startedAtRealtime)
    {
        double durationMilliseconds = (Time.realtimeSinceStartupAsDouble - startedAtRealtime) * 1000d;
        string modelResponseLog = rawModelResponse == null
            ? "N/A"
            : FormatLoggedText(rawModelResponse);
        string displayDifference = rawModelResponse != null && rawModelResponse != displayedText
            ? "Yes"
            : "No";

        Debug.Log(
            $"[Ollama][Request {currentRequestSequence}] COMPLETED\n" +
            $"HTTP status: {httpStatusCode}\n" +
            $"Outcome: {outcome}\n" +
            $"Success: {outcome == "SUCCESS"}\n" +
            $"Duration: {durationMilliseconds:F1} ms\n" +
            $"Raw HTTP response body: {FormatLoggedText(rawResponse)}\n" +
            $"Raw model response: {modelResponseLog}\n" +
            $"Displayed text differs from raw model response: {displayDifference}\n" +
            $"Final text sent to NPC bubble/error display: {FormatLoggedText(displayedText)}",
            this);
    }

    private static string FormatLoggedText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "<empty>";
        }

        if (text.Length <= MaxLoggedTextLength)
        {
            return text;
        }

        int remainingLength = text.Length - MaxLoggedTextLength;
        return $"{text.Substring(0, MaxLoggedTextLength)}\n...[truncated {remainingLength} characters; original length {text.Length}]";
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
