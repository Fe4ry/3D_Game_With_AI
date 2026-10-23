using UnityEngine;
using TMPro;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class NPCDialogueView : MonoBehaviour
{
    private const int ChoiceButtonCount = 4;

    private GameObject dialoguePanel;
    private TextMeshProUGUI dialogueText;
    private TextMeshProUGUI interactionPrompt;
    private readonly Button[] choiceButtons = new Button[ChoiceButtonCount];
    private readonly TextMeshProUGUI[] choiceButtonTexts = new TextMeshProUGUI[ChoiceButtonCount];
    private readonly UnityAction[] boundButtonActions = new UnityAction[ChoiceButtonCount];
    private bool initialized;

    public void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        Canvas canvas = GetScreenCanvas();
        if (canvas == null)
        {
            Debug.LogError("[NPCDialogueView] Screen Space Canvas bulunamadı; diyalog seçenekleri bağlanamadı.", this);
            return;
        }

        Transform panelTransform = canvas.transform.Find("DialoguePanel");
        if (panelTransform == null)
        {
            Debug.LogError("[NPCDialogueView] DialogueCanvas altında DialoguePanel bulunamadı.", this);
            return;
        }

        dialoguePanel = panelTransform.gameObject;
        GraphicRaycaster graphicRaycaster = canvas.GetComponent<GraphicRaycaster>();
        if (graphicRaycaster == null || !graphicRaycaster.isActiveAndEnabled)
        {
            Debug.LogError("[NPCDialogueView] DialogueCanvas üzerinde etkin GraphicRaycaster bulunamadı; buton tıklamaları alınamaz.", this);
        }

        if (EventSystem.current == null || !EventSystem.current.isActiveAndEnabled)
        {
            Debug.LogError("[NPCDialogueView] Etkin EventSystem bulunamadı; buton tıklamaları alınamaz.", this);
        }

        dialogueText = panelTransform.Find("NPCResponseText")?.GetComponent<TextMeshProUGUI>();
        if (dialogueText == null)
        {
            Debug.LogError("[NPCDialogueView] DialoguePanel altında NPCResponseText bulunamadı.", this);
        }

        RectTransform headingRect = dialogueText != null ? dialogueText.rectTransform : null;
        if (headingRect != null)
        {
            headingRect.anchorMin = new Vector2(0.05f, 0.80f);
            headingRect.anchorMax = new Vector2(0.95f, 0.96f);
            headingRect.offsetMin = new Vector2(8f, 4f);
            headingRect.offsetMax = new Vector2(-8f, -4f);
        }

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            Transform buttonTransform = panelTransform.Find($"DialogueButton{i + 1}");
            if (buttonTransform == null)
            {
                Debug.LogError($"[NPCDialogueView] DialogueButton{i + 1} bulunamadı.", this);
                continue;
            }

            Button button = buttonTransform.GetComponent<Button>();
            TextMeshProUGUI buttonText = buttonTransform.GetComponentInChildren<TextMeshProUGUI>(true);
            if (button == null || buttonText == null)
            {
                Debug.LogError($"[NPCDialogueView] DialogueButton{i + 1} için Button veya TextMeshProUGUI eksik.", this);
                continue;
            }

            choiceButtons[i] = button;
            choiceButtonTexts[i] = buttonText;
            RectTransform buttonRect = buttonTransform.GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                float top = 0.77f - i * 0.125f;
                buttonRect.anchorMin = new Vector2(0.05f, top - 0.105f);
                buttonRect.anchorMax = new Vector2(0.95f, top);
                buttonRect.offsetMin = Vector2.zero;
                buttonRect.offsetMax = Vector2.zero;
            }
        }
    }

    public void SetPanelVisible(bool visible)
    {
        if (dialoguePanel != null && dialoguePanel.activeSelf != visible)
        {
            dialoguePanel.SetActive(visible);
        }
    }

    public void InitializeInteractionPrompt(string promptMessage, TextMeshProUGUI assignedPrompt)
    {
        interactionPrompt = assignedPrompt;
        if (interactionPrompt == null)
        {
            Canvas canvas = GetScreenCanvas();
            if (canvas != null)
            {
                Transform promptTransform = canvas.transform.Find("InteractionPrompt");
                if (promptTransform != null)
                {
                    interactionPrompt = promptTransform.GetComponent<TextMeshProUGUI>();
                }
            }

            if (interactionPrompt == null)
            {
                GameObject promptObject = GameObject.Find("InteractionPrompt");
                if (promptObject != null)
                {
                    Canvas parentCanvas = promptObject.GetComponentInParent<Canvas>();
                    if (parentCanvas != null && parentCanvas.renderMode != RenderMode.WorldSpace)
                    {
                        interactionPrompt = promptObject.GetComponent<TextMeshProUGUI>();
                    }
                }
            }

            if (interactionPrompt == null)
            {
                interactionPrompt = CreateInteractionPrompt(promptMessage);
            }
        }

        if (interactionPrompt != null)
        {
            interactionPrompt.text = promptMessage;
        }
    }

    public void SetInteractionPromptVisible(bool visible)
    {
        if (interactionPrompt != null &&
            interactionPrompt.gameObject != null &&
            interactionPrompt.gameObject.activeSelf != visible)
        {
            interactionPrompt.gameObject.SetActive(visible);
        }
    }

    public void SetChoicesInteractable(bool interactable)
    {
        foreach (Button button in choiceButtons)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }
    }

    public void ShowFirstStageChoices(UnityAction onHelp, UnityAction onIgnore)
    {
        SetChoiceButton(0, "Yardım et", onHelp, true);
        SetChoiceButton(1, "Umursama", onIgnore, true);
        HideUnusedChoices(2);
    }

    public void ShowSecondStageChoice(UnityAction onPersuade)
    {
        SetChoiceButton(0, "İkna et", onPersuade, true);
        HideUnusedChoices(1);
    }

    public void ShowFinalStageChoices(UnityAction onPersuade, UnityAction onIgnore)
    {
        SetChoiceButton(0, "İkna et", onPersuade, true);
        SetChoiceButton(1, "Umursama", onIgnore, true);
        HideUnusedChoices(2);
    }

    public void ShowEgoistFinalChoice(UnityAction onDamageEgo)
    {
        SetChoiceButton(0, "Egosunu zedele", onDamageEgo, true);
        HideUnusedChoices(1);
    }

    public void HideChoices()
    {
        HideUnusedChoices(0);
    }

    private void HideUnusedChoices(int firstUnusedIndex)
    {
        for (int i = firstUnusedIndex; i < choiceButtons.Length; i++)
        {
            SetChoiceButton(i, string.Empty, null, false);
        }
    }

    public void SetDialogueText(string text)
    {
        if (dialogueText != null)
        {
            dialogueText.text = text;
        }
    }

    private void SetChoiceButton(int index, string label, UnityAction action, bool visible)
    {
        if (index < 0 || index >= choiceButtons.Length || choiceButtons[index] == null)
        {
            return;
        }

        if (boundButtonActions[index] != null)
        {
            choiceButtons[index].onClick.RemoveListener(boundButtonActions[index]);
        }

        boundButtonActions[index] = action;
        if (action != null)
        {
            choiceButtons[index].onClick.AddListener(action);
        }

        if (choiceButtonTexts[index] != null)
        {
            choiceButtonTexts[index].text = label;
        }

        choiceButtons[index].gameObject.SetActive(visible);
        choiceButtons[index].interactable = visible;
    }

    private TextMeshProUGUI CreateInteractionPrompt(string promptMessage)
    {
        Canvas canvas = GetScreenCanvas();

        if (canvas == null)
        {
            Debug.LogWarning("[NPCDialogueView] Sahnede Screen Space Canvas bulunamadı! 'DialogueCanvas' (ScreenSpaceOverlay) oluşturuluyor.", this);
            GameObject canvasObject = new GameObject("DialogueCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        Transform existing = canvas.transform.Find("InteractionPrompt");
        if (existing != null)
        {
            return existing.GetComponent<TextMeshProUGUI>();
        }

        GameObject promptObject = new GameObject("InteractionPrompt");
        promptObject.transform.SetParent(canvas.transform, false);

        RectTransform rect = promptObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.20f);
        rect.anchorMax = new Vector2(0.5f, 0.20f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(300, 50);

        TextMeshProUGUI text = promptObject.AddComponent<TextMeshProUGUI>();
        text.text = promptMessage;
        text.fontSize = 28;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;

        promptObject.SetActive(false);
        return text;
    }

    private static Canvas GetScreenCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
            {
                return canvas;
            }
        }

        return null;
    }
}
