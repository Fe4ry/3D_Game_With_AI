
using UnityEngine;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour
{
    [Header("UI Referansları")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private Text dialogueText;

    [Header("Panel Boyutları")]
    [SerializeField] private float horizontalPadding = 40f;
    [SerializeField] private float verticalPadding = 30f;
    [SerializeField] private float minWidth = 250f;
    [SerializeField] private float maxWidth = 500f;
    [SerializeField] private float minHeight = 100f;
    [SerializeField] private float maxHeight = 350f;

    private RectTransform panelRect;
    private RectTransform textRect;
    private RectTransform canvasRect;

    private void Awake()
    {
        if (dialoguePanel != null)
            panelRect = dialoguePanel.GetComponent<RectTransform>();

        if (dialogueText != null)
            textRect = dialogueText.GetComponent<RectTransform>();

        if (transform.parent != null)
            canvasRect = transform.parent.GetComponent<RectTransform>();

        if (dialogueText != null)
        {
            dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            dialogueText.verticalOverflow = VerticalWrapMode.Overflow;
            dialogueText.resizeTextForBestFit = false;
        }
    }

    private void Start()
    {
        HideDialogue();
    }

    public void ShowThinking()
    {
        ShowDialogue("Düşünüyor...");
    }

    public void ShowDialogue(string message)
    {
        if (dialoguePanel == null || dialogueText == null)
        {
            Debug.LogError("DialogueUI: Panel veya DialogueText atanmamış!", this);
            return;
        }

        dialoguePanel.SetActive(true);
        dialogueText.text = message;

        // Önce metnin gerçek yüksekliğini hesapla.
        Canvas.ForceUpdateCanvases();

        float preferredWidth = Mathf.Clamp(
            dialogueText.preferredWidth + horizontalPadding,
            minWidth,
            maxWidth
        );

        // Metin panelin içine sığacak şekilde satırlara bölünsün.
        float textWidth = preferredWidth - horizontalPadding;
        dialogueText.rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            textWidth
        );

        Canvas.ForceUpdateCanvases();

        float preferredHeight = Mathf.Clamp(
            dialogueText.preferredHeight + verticalPadding,
            minHeight,
            maxHeight
        );

        // Panel ve metin alanını güncelle.
        if (panelRect != null)
        {
            panelRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal, preferredWidth
            );

            panelRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical, preferredHeight
            );
        }

        if (textRect != null)
        {
            textRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                preferredHeight - verticalPadding
            );
        }

        // World Space Canvas da panelle birlikte büyüsün.
        if (canvasRect != null)
        {
            canvasRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal, preferredWidth
            );

            canvasRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical, preferredHeight
            );
        }
    }

    public void ShowError(string message)
    {
        ShowDialogue(message);
    }

    public void HideDialogue()
    {
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    private void LateUpdate()
    {
        if (dialoguePanel == null || !dialoguePanel.activeSelf)
            return;

        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            transform.rotation = Quaternion.LookRotation(
                transform.position - mainCamera.transform.position
            );
        }
    }
}
