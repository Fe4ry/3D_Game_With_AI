using UnityEngine;
using TMPro;

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

    private bool hasWarnedMissingReferences = false;

    private void Awake()
    {
        // Başlangıçta hedef kamera atanmadıysa ana kamerayı bul
        ResolveCamera();

        // Başlangıçta balon gizli olmalıdır
        Hide();
    }

    private void LateUpdate()
    {
        UpdateBillboardRotation();
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
    /// Balonun aktiflik durumunu ayarlar.
    private void SetVisible(bool visible)
    {
        // Prefer explicit bubbleRoot if assigned; otherwise fall back to the Canvas GameObject.
        if (bubbleRoot != null)
        {
            bubbleRoot.SetActive(visible);
        }
        else if (bubbleCanvas != null)
        {
            bubbleCanvas.gameObject.SetActive(visible);
        }
    }
    /// </summary>
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
