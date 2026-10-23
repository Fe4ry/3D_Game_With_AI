
using UnityEngine;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour
{
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private Text dialogueText;

    private void Start()
    {
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    public void ShowDialogue(string message)
    {
        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        if (dialogueText != null)
            dialogueText.text = message;
        else
            Debug.Log("NPC: " + message);
    }

    public void HideDialogue()
    {
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }
}
