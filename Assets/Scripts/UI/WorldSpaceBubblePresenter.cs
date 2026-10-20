using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Oyuncunun başının üzerinde beliren World Space konuşma/düşünce balonunu yöneten bileşen.
/// Kameraya dönük kalmasını (Billboard) sağlar; düşünme, cevap ve hata durumlarını TextMeshProUGUI üzerinden gösterir.
/// </summary>
public class WorldSpaceBubblePresenter : MonoBehaviour
{
    [Header("UI Referansları")]
    [Tooltip("World Space modunda çalışan Canvas bileşeni.")]
    [SerializeField] private Canvas bubbleCanvas;

    [Tooltip("Metnin yazdırılacağı TextMeshProUGUI bileşeni.")]
    [SerializeField] private TextMeshProUGUI messageText;

    [Tooltip("Açılıp kapatılacak kök GameObject (boş bırakılırsa Canvas'ın GameObject'i kullanılır).")]
    [SerializeField] private GameObject bubbleRoot;

    [Header("Kamera Ayarları")]
    [Tooltip("Balonun dönük kalacağı kamera (boş bırakılırsa Camera.main otomatik bulunur).")]
    [SerializeField] private Camera targetCamera;

    [Header("Metin ve Renk Ayarları")]
    [Tooltip("Düşünme anında gösterilecek varsayılan metin.")]
    [SerializeField] private string thinkingText = "Düşünüyor...";

    [Tooltip("Normal yanıt metni rengi.")]
    [SerializeField] private Color normalTextColor = Color.white;

    [Tooltip("Düşünme durumu metin rengi.")]
    [SerializeField] private Color thinkingTextColor = new Color(1f, 0.92f, 0.45f, 1f);

    [Tooltip("Hata durumu metin rengi.")]
    [SerializeField] private Color errorTextColor = new Color(1f, 0.4f, 0.4f, 1f);

    [Header("Balon Boyutu")]
    [SerializeField] private Vector2 minimumBubbleSize = new Vector2(96f, 44f);
    [SerializeField] private Vector2 maximumBubbleSize = new Vector2(320f, 180f);
    [SerializeField] private Vector2 bubblePadding = new Vector2(24f, 16f);

    private bool hasWarnedMissingReferences = false;
    private bool activationRequestedBySetVisible = false;
    private bool hasLoggedRuntimeDiagnostics = false;
    private Transform headTransform;
    private RectTransform bubbleRect;
    private RectTransform backgroundRect;
    private RectTransform messageRect;
    private ScrollRect bubbleScrollRect;
    private RectMask2D bubbleMask;

    private void Awake()
    {
        // Başlangıçta hedef kamera atanmadıysa ana kamerayı bul
        ResolveCamera();
        CacheLayoutReferences();

        // Başlangıçta balon gizli olmalıdır
        if (!activationRequestedBySetVisible)
        {
            Hide();
        }
    }

    private void LateUpdate()
    {
        bool hasHeadAnchor = UpdateHeadAnchorPosition();
        UpdateBillboardRotation();

        if (!hasLoggedRuntimeDiagnostics && hasHeadAnchor)
        {
            LogRuntimeDiagnostics("LateUpdate");
            hasLoggedRuntimeDiagnostics = true;
        }
    }

    /// <summary>
    /// LLM yanıtı beklenirken 'Düşünüyor...' durumunu görüntüler.
    /// </summary>
    public void ShowThinking()
    {
        Debug.Log("[BubbleTest] ShowThinking çağrıldı", this);
        if (!ValidateReferences()) return;

        messageText.color = thinkingTextColor;
        messageText.text = thinkingText;
        SetVisible(true);
        LogDebugInfo("ShowThinking");
    }

    /// <summary>
    /// LLM'den başarılı yanıt geldiğinde metni görüntüler.
    /// </summary>
    /// <param name="response">Görüntülenecek cevap metni.</param>
    public void ShowResponse(string response)
    {
        Debug.Log($"[BubbleTest] ShowResponse çağrıldı: {response}", this);
        if (!ValidateReferences()) return;

        messageText.color = normalTextColor;
        messageText.text = response;
        SetVisible(true);
        LogDebugInfo("ShowResponse");
    }

    /// <summary>
    /// Bir bağlantı veya model hatası oluştuğunda hata mesajını görüntüler.
    /// </summary>
    /// <param name="errorMessage">Görüntülenecek hata mesajı.</param>
    public void ShowError(string errorMessage)
    {
        if (!ValidateReferences()) return;

        messageText.color = errorTextColor;
        messageText.text = errorMessage;
        SetVisible(true);
    }

    /// <summary>
    /// Balonu tamamen gizler.
    /// </summary>
    public void Hide()
    {
        SetVisible(false);
    }

    /// <summary>
    /// Balonun görünürlüğünü ayarlar ve Canvas'ı HeadBubble anchor'ına göre konumlandırır.
    /// </summary>
    private void SetVisible(bool visible)
    {
        GameObject target = bubbleRoot != null ? bubbleRoot : (bubbleCanvas != null ? bubbleCanvas.gameObject : null);
        GameObject canvasObject = bubbleCanvas != null ? bubbleCanvas.gameObject : null;

        Debug.Log($"[BubbleDebug] SetVisible({visible}) references: bubbleRoot={DescribeObject(bubbleRoot)}, bubbleCanvas={DescribeObject(canvasObject)}, target={DescribeObject(target)}", this);

        if (target == null) return;
        activationRequestedBySetVisible = visible;
        target.SetActive(visible);

        if (visible)
        {
            ResizeBubbleToText();
        }

        activationRequestedBySetVisible = false;

        if (visible && bubbleCanvas != null)
        {
            bubbleCanvas.transform.localPosition = Vector3.zero;
        }

        Debug.Log($"[BubbleDebug] SetVisible({visible}) after SetActive: target={DescribeObject(target)}, bubbleCanvas={DescribeObject(canvasObject)}", this);
    }

    private void CacheLayoutReferences()
    {
        if (bubbleCanvas != null)
        {
            bubbleRect = bubbleCanvas.transform as RectTransform;
        }

        if (messageText == null) return;

        messageRect = messageText.rectTransform;
        backgroundRect = messageRect.parent as RectTransform;
        if (backgroundRect == null) return;

        bubbleScrollRect = backgroundRect.GetComponent<ScrollRect>();
        bubbleMask = backgroundRect.GetComponent<RectMask2D>();
    }

    private void ResizeBubbleToText()
    {
        CacheLayoutReferences();
        if (messageText == null || bubbleRect == null || backgroundRect == null ||
            bubbleScrollRect == null || bubbleMask == null)
        {
            Debug.LogError("[WorldSpaceBubblePresenter] Otomatik balon boyutlandırma için BubbleCanvas, Background, ScrollRect ve RectMask2D referansları/hiyerarşisi eksik.", this);
            return;
        }

        float maxWidth = Mathf.Max(minimumBubbleSize.x, maximumBubbleSize.x);
        float maxHeight = Mathf.Max(minimumBubbleSize.y, maximumBubbleSize.y);
        Vector2 padding = new Vector2(Mathf.Max(0f, bubblePadding.x), Mathf.Max(0f, bubblePadding.y));
        string text = messageText.text ?? string.Empty;

        messageText.enableWordWrapping = true;
        messageText.overflowMode = TextOverflowModes.Overflow;
        bubbleScrollRect.content = messageRect;
        bubbleScrollRect.viewport = backgroundRect;

        float preferredWidth = messageText.GetPreferredValues(text, Mathf.Infinity, Mathf.Infinity).x;
        float width = Mathf.Clamp(preferredWidth + padding.x, minimumBubbleSize.x, maxWidth);
        float textWidth = Mathf.Max(1f, width - padding.x);
        float preferredHeight = messageText.GetPreferredValues(text, textWidth, float.MaxValue).y;
        float height = Mathf.Clamp(preferredHeight + padding.y, minimumBubbleSize.y, maxHeight);
        float contentHeight = Mathf.Max(preferredHeight, height - padding.y) + padding.y;

        bubbleRect.sizeDelta = new Vector2(width, height);
        messageRect.anchorMin = new Vector2(0f, 1f);
        messageRect.anchorMax = new Vector2(1f, 1f);
        messageRect.pivot = new Vector2(0.5f, 1f);
        messageRect.anchoredPosition = new Vector2(0f, -padding.y * 0.5f);
        messageRect.sizeDelta = new Vector2(-padding.x, contentHeight);

        Canvas.ForceUpdateCanvases();
        messageText.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        bubbleScrollRect.StopMovement();
        bubbleScrollRect.verticalNormalizedPosition = 1f;
    }

    private static string DescribeObject(GameObject target)
    {
        if (target == null) return "null";

        Transform parent = target.transform.parent;
        string path = target.name;
        for (Transform current = parent; current != null; current = current.parent)
        {
            path = $"{current.name}/{path}";
        }

        string parentName = parent != null ? parent.name : "null";
        return $"name='{target.name}', instanceID={target.GetInstanceID()}, path='{path}', parent='{parentName}', activeSelf={target.activeSelf}, activeInHierarchy={target.activeInHierarchy}";
    }

    private bool UpdateHeadAnchorPosition()
    {
        if (bubbleCanvas == null) return false;

        Transform headBubble = bubbleCanvas.transform.parent;
        Transform mahkum1 = headBubble != null ? headBubble.parent : null;
        if (headBubble == null || mahkum1 == null || !headBubble.IsChildOf(mahkum1))
        {
            return false;
        }

        if (headTransform == null || !headTransform.IsChildOf(mahkum1))
        {
            headTransform = FindHeadTransform(mahkum1);
        }

        if (headTransform == null)
        {
            return false;
        }

        headBubble.position = headTransform.position;
        return true;
    }

    private static Transform FindHeadTransform(Transform npc)
    {
        Animator[] animators = npc.GetComponentsInChildren<Animator>(true);
        foreach (Animator animator in animators)
        {
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
            {
                return head;
            }
        }

        Transform[] children = npc.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (string.Equals(child.name, "head", System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private void LogRuntimeDiagnostics(string context)
    {
        Transform headBubble = bubbleCanvas != null ? bubbleCanvas.transform.parent : null;
        Transform mahkum1 = headBubble != null ? headBubble.parent : null;
        Transform canvasTransform = bubbleCanvas != null ? bubbleCanvas.transform : null;

        LogTransform(context, "Mahkum1", mahkum1);
        LogTransform(context, "HeadBubble", headBubble);
        LogTransform(context, "BubbleCanvas", canvasTransform);

        bool headBubbleUnderNpc = mahkum1 != null && headBubble != null && headBubble.IsChildOf(mahkum1);
        float headDistance = headTransform != null && headBubble != null
            ? Vector3.Distance(headBubble.position, headTransform.position)
            : float.NaN;
        Debug.Log($"[BubbleDebug] {context} - HeadBubble parent: {(headBubble != null && headBubble.parent != null ? headBubble.parent.name : "null")}, under Mahkum1: {headBubbleUnderNpc}, head transform: {(headTransform != null ? headTransform.name : "null")}, distance to head: {headDistance:F3}", this);

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning($"[BubbleDebug] {context} - Camera.main bulunamadı.", this);
            return;
        }

        Vector3 viewportPosition = canvasTransform != null
            ? mainCamera.WorldToViewportPoint(canvasTransform.position)
            : Vector3.zero;
        bool inView = canvasTransform != null &&
                      viewportPosition.x >= 0f && viewportPosition.x <= 1f &&
                      viewportPosition.y >= 0f && viewportPosition.y <= 1f &&
                      viewportPosition.z > 0f;
        Debug.Log($"[BubbleDebug] {context} - Camera.main: {mainCamera.name}, position: {mainCamera.transform.position}, forward: {mainCamera.transform.forward}, WorldToViewportPoint: {viewportPosition}, inView: {inView}", this);
    }

    private void LogTransform(string context, string objectName, Transform target)
    {
        if (target == null)
        {
            Debug.Log($"[BubbleDebug] {context} - {objectName}: null", this);
            return;
        }

        Debug.Log($"[BubbleDebug] {context} - {objectName}: position={target.position}, localPosition={target.localPosition}, rotation={target.rotation.eulerAngles}, localRotation={target.localRotation.eulerAngles}, localScale={target.localScale}", this);
    }

    private void LogDebugInfo(string context)
    {
        // Canvas info
        var canvas = bubbleCanvas;
        var cam = Camera.main;
        string canvasInfo = $"[BubbleDebug] {context} - Canvas renderMode: {canvas?.renderMode}, pos: {canvas?.transform.position}, scale: {canvas?.transform.lossyScale}, activeInHierarchy: {canvas?.gameObject.activeInHierarchy}";
        if (canvas != null)
        {
            var rect = ((RectTransform)canvas.transform).rect;
            canvasInfo += $", rect: {rect}";
        }
        Debug.Log(canvasInfo);

        // MessageText info
        var txt = messageText;
        string textInfo = $"[BubbleDebug] {context} - MessageText text: \"{txt?.text}\", color: {txt?.color}, fontSize: {txt?.fontSize}, activeInHierarchy: {txt?.gameObject.activeInHierarchy}";
        Debug.Log(textInfo);

        // Camera info
        string camInfo = $"[BubbleDebug] {context} - Camera.main: {(cam != null ? cam.name : "null")}";
        Debug.Log(camInfo);

        // Visibility in camera view
        if (cam != null && canvas != null)
        {
            var vp = cam.WorldToViewportPoint(canvas.transform.position);
            bool inView = vp.x >= 0 && vp.x <= 1 && vp.y >= 0 && vp.y <= 1 && vp.z > 0;
            Debug.Log($"[BubbleDebug] {context} - Canvas viewportPos: {vp}, inView: {inView}");
        }
    }

    /// <summary>
    /// Balonun her karede kameranın bakış açısına dik (Billboard) kalmasını sağlar.
    /// Kamera hareketleri genellikle Update'te işlendiğinden LateUpdate içerisinde çalıştırılır.
    /// </summary>
    private void UpdateBillboardRotation()
    {
        if (targetCamera == null)
        {
            ResolveCamera();
            if (targetCamera == null) return;
        }

        // World Space Canvas'ın kameraya paralel ve okunabilir durması için kameranın rotasyonunu takip eder
        transform.rotation = targetCamera.transform.rotation;
    }

    /// <summary>
    /// Hedef kamera atanmamışsa sahnedeki ana kamerayı bulmayı dener.
    /// </summary>
    private void ResolveCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = FindFirstObjectByType<Camera>();
            }
        }
    }

    /// <summary>
    /// Gerekli Inspector referanslarının eksiksiz olduğunu doğrular, eksikse anlaşılır bir uyarı üretir.
    /// </summary>
    private bool ValidateReferences()
    {
        if (messageText == null)
        {
            if (!hasWarnedMissingReferences)
            {
                Debug.LogWarning("[WorldSpaceBubblePresenter] 'Message Text' (TextMeshProUGUI) referansı atanmamış! Lütfen Inspector üzerinden metin bileşenini bağlayın.", this);
                hasWarnedMissingReferences = true;
            }
            return false;
        }

        if (bubbleCanvas == null && bubbleRoot == null)
        {
            if (!hasWarnedMissingReferences)
            {
                Debug.LogWarning("[WorldSpaceBubblePresenter] 'Bubble Canvas' veya 'Bubble Root' referansı atanmamış! Lütfen Inspector üzerinden ilgili nesneyi bağlayın.", this);
                hasWarnedMissingReferences = true;
            }
            return false;
        }

        return true;
    }
}
