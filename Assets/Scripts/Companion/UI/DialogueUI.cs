
using UnityEngine;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour
{
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private Text dialogueText;

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
        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        if (dialogueText != null)
            dialogueText.text = message;
        else
            Debug.LogWarning("DialogueUI: Dialogue Text atanmamış.");
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
        // Konuşma balonu kameraya dönük kalsın.
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
