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

    [Tooltip("Kullanılacak Ollama model adı (örn: gemma3:4b).")]
    [SerializeField] private string modelName = "gemma3:4b"; 

    [Tooltip("İstek zaman aşımı süresi (saniye).")]
    [SerializeField] private float timeoutSeconds = 30f;

    [Header("Karakter / Sistem Rolü")]
    [TextArea(3, 6)]
    [Tooltip("Karakterin rolünü ve konuşma tarzını belirleyen sistem prompt'u.")]
    [SerializeField] private string systemPrompt = "NPC kişiliği, geçmişi ve bu diyalog turunun kuralları kullanıcı mesajında verilir. Bunları tutarlı biçimde uygula.";

    private bool isBusy = false;
    private readonly List<ConversationExchange> conversationHistory = new List<ConversationExchange>();
    private int requestSequence;
    private int activeRequestSequence;
    private UnityWebRequest activeRequest;

    private const int MaxLoggedTextLength = 2000;
    private const int MaxPromptSummaryLength = 120;

    /// <summary>
    /// Servisin şu anda aktif bir istek yürütüp yürütmediği.
    /// </summary>
    public bool IsBusy => isBusy;

    /// <summary>
    /// Doğrudan bir coroutine başlatarak prompt gönderir.
    /// </summary>
    public void RequestResponse(string prompt, Action<NpcDecision> onSuccess, Action<string> onError)
    {
        if (isBusy)
        {
            SafeInvokeError(onError, "Zaten devam eden bir düşünme/istek süreci var.");
            return;
        }

        StartCoroutine(GenerateResponseInternal(prompt, onSuccess, onError));
    }

    public void CancelCurrentRequest()
    {
        if (!isBusy)
        {
            return;
        }

        int cancelledRequestSequence = activeRequestSequence;
        requestSequence++;
        activeRequestSequence = 0;
        isBusy = false;

        UnityWebRequest requestToCancel = activeRequest;
        activeRequest = null;
        if (requestToCancel != null && !requestToCancel.isDone)
        {
            try
            {
                requestToCancel.Abort();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Ollama] Request {cancelledRequestSequence} could not be aborted cleanly: {exception.Message}", this);
            }
        }

        Debug.Log($"[Ollama] Request {cancelledRequestSequence} cancelled.", this);
    }

    /// <summary>
    /// LLM'e asenkron istek gönderen Unity coroutine metodu.
    /// </summary>
    public IEnumerator GenerateResponse(string prompt, Action<NpcDecision> onSuccess, Action<string> onError)
    {
        return GenerateResponseInternal(prompt, onSuccess, onError);
    }

    private IEnumerator GenerateResponseInternal(
        string prompt,
        Action<NpcDecision> onSuccess,
        Action<string> onError)
    {
        if (isBusy)
        {
            SafeInvokeError(onError, "Zaten devam eden bir düşünme/istek süreci var.");
            yield break;
        }

        isBusy = true;
        int currentRequestSequence = ++requestSequence;
        activeRequestSequence = currentRequestSequence;
        DateTime startedAtUtc = DateTime.UtcNow;
        double startedAtRealtime = Time.realtimeSinceStartupAsDouble;
        int playerMessageCount = conversationHistory.Count;
        int npcResponseCount = conversationHistory.Count;
        bool callbackInvoked = false;
        Action<NpcDecision> reportSuccess = decision =>
        {
            if (callbackInvoked)
            {
                return;
            }

            callbackInvoked = true;
            try
            {
                onSuccess?.Invoke(decision);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        };
        Action<string> reportError = errorMessage =>
        {
            if (callbackInvoked)
            {
                return;
            }

            callbackInvoked = true;
            SafeInvokeError(onError, errorMessage);
        };
        try
        {
            string jsonPayload = null;
            byte[] bodyRaw = null;
            UnityWebRequest request = null;
            Exception setupException = null;
            try
            {
                string fullPrompt = BuildPromptWithHistory(prompt);
                OllamaRequest requestDto = new OllamaRequest(
                    model: modelName,
                    prompt: fullPrompt,
                    system: BuildSystemPrompt(),
                    stream: false
                );
                jsonPayload = JsonUtility.ToJson(requestDto);
                bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                request = new UnityWebRequest(apiUrl, UnityWebRequest.kHttpVerbPOST);
            }
            catch (Exception exception)
            {
                setupException = exception;
            }

            if (setupException != null)
            {
                CompleteWithError(
                    currentRequestSequence, 0, "REQUEST_SETUP_ERROR", null, null,
                    "Ollama isteği hazırlanamadı.", startedAtRealtime, reportError);
                Debug.LogException(setupException, this);
            }
            else
            {
                Debug.Log(
                    $"[Ollama][Request {currentRequestSequence}] STARTED\n" +
                    $"Started (UTC): {startedAtUtc:O}\n" +
                    $"Model: {modelName}\n" +
                    $"URL: {GetSafeRequestUrl(apiUrl)}\n" +
                    $"Method: POST\n" +
                    "Content-Type: application/json\n" +
                    $"Request body bytes: {bodyRaw.Length}\n" +
                    $"Player message summary: {SummarizePrompt(prompt)}\n" +
                    $"History before request: {playerMessageCount} player message(s), {npcResponseCount} NPC response(s)\n" +
                    $"Timeout: {Mathf.Max(1, Mathf.RoundToInt(timeoutSeconds))} seconds",
                    this);

                using (request)
                {
                    activeRequest = request;
                    UnityWebRequestAsyncOperation operation = null;
                    Exception requestStartException = null;
                    try
                    {
                        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                        request.downloadHandler = new DownloadHandlerBuffer();
                        request.SetRequestHeader("Content-Type", "application/json");
                        request.timeout = Mathf.Max(1, Mathf.RoundToInt(timeoutSeconds));
                        operation = request.SendWebRequest();
                    }
                    catch (Exception exception)
                    {
                        requestStartException = exception;
                    }

                    if (requestStartException != null)
                    {
                        CompleteWithError(
                            currentRequestSequence, 0, "REQUEST_START_ERROR", null, null,
                            "Ollama isteği başlatılamadı.", startedAtRealtime, reportError);
                        Debug.LogException(requestStartException, this);
                    }
                    else if (operation != null)
                    {
                        yield return operation;
                        if (currentRequestSequence != requestSequence)
                        {
                            yield break;
                        }

                        try
                        {
                            HandleResponse(
                                request,
                                prompt,
                                currentRequestSequence,
                                startedAtRealtime,
                                reportSuccess,
                                reportError);
                        }
                        catch (Exception exception)
                        {
                            CompleteWithError(
                                currentRequestSequence, request.responseCode, "REQUEST_PROCESSING_ERROR",
                                null, null,
                                "Ollama yanıtı işlenemedi.", startedAtRealtime, reportError);
                            Debug.LogException(exception, this);
                        }
                    }
                    else
                    {
                        CompleteWithError(
                            currentRequestSequence, 0, "REQUEST_START_ERROR", null, null,
                            "Ollama isteği başlatılamadı.", startedAtRealtime, reportError);
                    }
                }
            }
        }
        finally
        {
            if (activeRequestSequence == currentRequestSequence)
            {
                activeRequest = null;
                activeRequestSequence = 0;
                isBusy = false;
            }
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
        contextualPrompt.Append("Şimdi karar görevinin JSON şemasına uygun yanıt ver.");
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
            "Sen Türkçe konuşan bir NPC'sin. Kişiliğin, geçmişin ve bu turun kuralları en yeni [OYUNCU] mesajında verilir; bunlara uy. " +
            "Yalnızca en yeni [OYUNCU] mesajına yanıt ver; önceki konuşma bağlamdır. En fazla iki kısa ve doğal cümle kur. " +
            "İkna sonucunu yalnızca bu turun kurallarına ve gerçek tutumuna göre değerlendir; olumlu bir diyalog tek başına ikna sayılmaz. " +
            "Yalnızca ikna olduysan persuaded=true ve action=FOLLOW, aksi hâlde persuaded=false ve action=STAY ver. " +
            "Her yanıt action, dialogue ve persuaded alanlarını içermelidir. action yalnızca FOLLOW veya STAY; dialogue kısa doğal Türkçe; persuaded JSON boolean olmalıdır. " +
            "Aşağıdaki nesne yalnızca alan ve tür örneğidir; metni aynen tekrarlama: " +
            "{\"action\":\"STAY\",\"dialogue\":\"Bu kez gelemem.\",\"persuaded\":false}. " +
            "Yalnızca bu üç alanı içeren tek bir JSON nesnesi döndür; ek alan veya Markdown kullanma.");

        return completeSystemPrompt.ToString();
    }

    private void HandleResponse(
        UnityWebRequest request,
        string playerPrompt,
        int currentRequestSequence,
        double startedAtRealtime,
        Action<NpcDecision> onSuccess,
        Action<string> onError)
    {
        string rawResponse = request.downloadHandler != null ? request.downloadHandler.text : null;
        long httpStatusCode = request.responseCode;

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning(
                $"[Ollama][Request {currentRequestSequence}] REQUEST FAILED\n" +
                $"Result: {request.result}\n" +
                $"Error: {request.error ?? "<none>"}\n" +
                $"HTTP status: {httpStatusCode}\n" +
                $"URL: {GetSafeRequestUrl(request.url)}\n" +
                $"Method: {request.method}\n" +
                $"Response body: {FormatLoggedText(rawResponse)}",
                this);
        }

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
                if (!OllamaResponseParser.TryParseNpcDecision(
                        rawModelResponse,
                        out NpcDecision decision,
                        out string decisionError))
                {
                    CompleteWithError(
                        currentRequestSequence, httpStatusCode, "DECISION_PARSE_ERROR", rawResponse, rawModelResponse,
                        decisionError, startedAtRealtime, onError);
                    return;
                }

                conversationHistory.Add(new ConversationExchange(playerPrompt, decision.Dialogue));

                // 3. Başarı callback'i JSON bloğunun dışına alınarak izole edilir
                // onSuccess hata fırlatsa bile bu durum JSON hatası olarak onError'a yönlendirilmez
                try
                {
                    onSuccess?.Invoke(decision);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, this);
                }
                LogRequestCompletion(
                    currentRequestSequence, httpStatusCode, "SUCCESS", rawResponse, rawModelResponse,
                    decision.Dialogue, startedAtRealtime, decision.Dialogue);
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
        Debug.LogWarning(
            $"[Ollama][Request {currentRequestSequence}] Dispatching error callback; outcome: {outcome}.",
            this);
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
        double startedAtRealtime,
        string rawDialogueText = null)
    {
        double durationMilliseconds = (Time.realtimeSinceStartupAsDouble - startedAtRealtime) * 1000d;

        Debug.Log(
            $"[Ollama][Request {currentRequestSequence}] COMPLETED\n" +
            $"HTTP status: {httpStatusCode}\n" +
            $"Outcome: {outcome}\n" +
            $"Success: {outcome == "SUCCESS"}\n" +
            $"Duration: {durationMilliseconds:F1} ms\n" +
            $"HTTP response body length: {(rawResponse == null ? 0 : rawResponse.Length)} characters\n" +
            $"Raw model response length: {(rawModelResponse == null ? 0 : rawModelResponse.Length)} characters\n" +
            $"Raw model response: {FormatLoggedText(rawModelResponse)}\n" +
            $"UI text matches response.dialogue: {rawDialogueText != null && string.Equals(rawDialogueText, displayedText, StringComparison.Ordinal)}\n" +
            $"Displayed text length: {(displayedText == null ? 0 : displayedText.Length)} characters",
            this);
    }

    private static string SummarizePrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return "<empty>";
        }

        string summary = prompt.Trim().Replace('\r', ' ').Replace('\n', ' ');
        return summary.Length <= MaxPromptSummaryLength
            ? summary
            : summary.Substring(0, MaxPromptSummaryLength) + "...";
    }

    private static string GetSafeRequestUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
        {
            string authority = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
            return $"{uri.Scheme}://{authority}{uri.AbsolutePath}";
        }

        return "<invalid URL>";
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