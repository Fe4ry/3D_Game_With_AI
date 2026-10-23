
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueUIBuilder : MonoBehaviour
{
    [ContextMenu("Diyalog Aray�z�n� Olu�tur")]
    public void CreateDialogueUI()
    {
        Canvas canvas = null;
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas candidate in canvases)
        {
            if (candidate.renderMode != RenderMode.WorldSpace)
            {
                canvas = candidate;
                break;
            }
        }

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("DialogueCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        Transform existing = canvas.transform.Find("DialoguePanel");

        if (existing != null)
        {
            Debug.Log("DialoguePanel zaten mevcut.");
            return;
        }

        GameObject panel = new GameObject("DialoguePanel");
        panel.transform.SetParent(canvas.transform, false);

        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.15f, 0.05f);
        rect.anchorMax = new Vector2(0.85f, 0.40f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image background = panel.AddComponent<Image>();
        background.color = new Color(0.08f, 0.08f, 0.08f, 0.92f);

        CreateText(panel.transform, "NPCResponseText",
            "Mahkûmla ne yapmak istiyorsun?",
            new Vector2(0.05f, 0.62f),
            new Vector2(0.95f, 0.95f));

        string[] messages =
        {
            "Yardım et",
            "Umursama",
            "İkna et",
            "Egosunu zedele"
        };

        for (int i = 0; i < messages.Length; i++)
        {
            float top = 0.77f - i * 0.125f;

            GameObject buttonObject = new GameObject(
                "DialogueButton" + (i + 1));

            buttonObject.transform.SetParent(panel.transform, false);

            RectTransform buttonRect =
                buttonObject.AddComponent<RectTransform>();

            buttonRect.anchorMin = new Vector2(0.05f, top - 0.105f);
            buttonRect.anchorMax = new Vector2(0.95f, top);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            Image buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = new Color(0.22f, 0.30f, 0.38f, 1f);

            Button button = buttonObject.AddComponent<Button>();

            CreateText(buttonObject.transform, "Text",
                messages[i], Vector2.zero, Vector2.one);
        }

        panel.SetActive(false);
        Debug.Log("Diyalog aray�z� olu�turuldu.");
    }

    private void CreateText(
        Transform parent, string objectName, string content,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(8, 4);
        rect.offsetMax = new Vector2(-8, -4);

        TextMeshProUGUI text =
            textObject.AddComponent<TextMeshProUGUI>();

        text.text = content;
        text.fontSize = 22;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
    }
}
