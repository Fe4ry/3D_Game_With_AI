using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class NPCController : MonoBehaviour
{
    public enum PersonalityType
    {
        Sevecen,
        Korkak,
        Egoist,
        Saldirgan
    }

    [Header("Ollama")]
    [SerializeField] private OllamaService ollamaService;

    [Header("Diyalog")]
    [SerializeField] private DialogueUI dialogueUI;

    [Header("Diyalog Paneli")]
    [SerializeField] private GameObject dialogueChoicePanel;
    [SerializeField] private Text choiceNPCMessageText;

    [Header("Oyuncu Mesajı")]
    [SerializeField] private TMP_InputField playerInputField;
    [SerializeField] private Button submitButton;

    [Header("Oyun Sonu Ekranı")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text quoteText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button replayButton;

    [Header("NPC Takip")]
    [SerializeField] private NPCFollowPlayer npcFollowPlayer;

    [Header("NPC Kişiliği")]
    [SerializeField] private PersonalityType personalityType =
        PersonalityType.Sevecen;

    private const int MaxAttempts = 3;

    private const string InsultLoseMessage =
        "Oyunu kaybettin, vay ZORBA vay...";

    private const string PersuasionLoseMessage =
        "İKNA EDEMEDİNİZ...";

    private static readonly string[] MoralQuotes =
    {
        "İnsanın gerçek karakteri, kendisinden güçsüz olanlara nasıl davrandığında ortaya çıkar.",
        "Başkalarını değersizleştirerek kendini değerli kılamazsın.",
        "Gerçek güç, birini incitebilecekken ona merhamet gösterebilmektir.",
        "İnsanın değeri sözleriyle değil, davranışlarıyla ölçülür.",
        "Saygı, korku salarak değil, insanlara nasıl davrandığınla kazanılır.",
        "İnsanlığını kaybettikten sonra kazanılan bir tartışmanın hiçbir anlamı yoktur.",
        "Gururun unutturmaya çalıştıklarını vicdanın hatırlar.",
        "Başkalarına nasıl davrandığın, zamanla kim olduğuna dönüşür.",
        "Zalimlik sana güç veriyor gibi görünür, ama aslında zayıflığını ortaya çıkarır.",
        "İnsanın en büyük zaferi, zalimleşmeyi öğreten bir dünyada bile insan kalabilmektir."
    };

    private const string OllamaFailureMessage =
        "Bu mahkum konuşmayı bilmiyor.";

    private int remainingAttempts = MaxAttempts;
    private bool isWaitingForResponse;
    private bool isPersuaded;

    public bool IsPersuaded => isPersuaded;

    private bool isGameOver;

    [System.Serializable]
    private class AttemptDebugInfo
    {
        public string personality;
        public int remainingAttempts;
        public bool persuaded;
        public bool insulted;
        public string action;
        public string dialogue;
    }

    private void Awake()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        SetChoicePanelVisible(false);

        if (replayButton != null)
            replayButton.onClick.AddListener(ReplayGame);

        if (submitButton != null)
            submitButton.onClick.AddListener(OnSubmitPlayerInput);
        else
            Debug.LogError(
                "SubmitButton Inspector'da atanmamış!", this);

        if (playerInputField == null)
            Debug.LogError(
                "InputField_TMP Inspector'da atanmamış!", this);
    }

    private void Start()
    {
        if (npcFollowPlayer != null)
            npcFollowPlayer.SetFollowing(false);
    }

    private void OnDestroy()
    {
        if (replayButton != null)
            replayButton.onClick.RemoveListener(ReplayGame);

        if (submitButton != null)
            submitButton.onClick.RemoveListener(OnSubmitPlayerInput);
    }

    public void Talk(string playerMessage)
    {
        if (isWaitingForResponse || isGameOver || isPersuaded)
            return;

        if (ollamaService == null)
        {
            HandleOllamaError(
                "OllamaService Inspector'da atanmamış!");
            return;
        }

        StartCoroutine(SendPlayerMessage(playerMessage, false));
    }

    public void AskForHelp()
    {
        Talk("Sana yardım etmek istiyorum.");
    }

    public void SayHello()
    {
        Talk("Merhaba. Seninle konuşmak istiyorum.");
    }

    public void StartConversation()
    {
        if (isGameOver || isPersuaded || isWaitingForResponse)
            return;

        SayHello();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private IEnumerator SendPlayerMessage(
        string playerMessage,
        bool countsAsAttempt)
    {
        if (ollamaService == null)
        {
            HandleOllamaError(
                "OllamaService Inspector'da atanmamış!");
            yield break;
        }

        isWaitingForResponse = true;

        if (countsAsAttempt)
            remainingAttempts--;

        SetButtonsInteractable(false);

        if (dialogueUI != null)
            dialogueUI.ShowThinking();

        string prompt;

        if (countsAsAttempt)
        {
            prompt =

            prompt =
    "CRITICAL PLAYER BEHAVIOR RULE:\n" +
    "Your first priority is to evaluate how the player treats you.\n" +
    "Determine whether the player's message contains personal abuse, " +
    "disrespect, verbal aggression, humiliation, mockery or degrading behavior.\n" +
    "Evaluate the actual meaning and context, not specific keywords.\n" +
    "Any message that attacks you personally, insults your dignity, " +
    "mocks you, wishes harm upon you or treats you in a degrading manner " +
    "must result in insulted=true.\n" +
    "Do not require profanity or explicit swear words.\n" +
    "A negative opinion, disagreement, criticism of an idea or refusal " +
    "to help is NOT an insult by itself.\n" +
    "If the player behaves abusively or disrespectfully toward you, " +
    "insulted MUST be true.\n" +
    "If insulted=true, action MUST be STAY and persuaded MUST be false.\n\n" +

    "You are a prisoner NPC in a story-driven game.\n" +
    GetPersonalityDescription() +"\n"+
                "You are a prisoner NPC in a story-driven game.\n" +
                GetPersonalityDescription() +
                "\nThe player's chosen message is: \"" +
                playerMessage + "\"\n\n" +

                "EVALUATE THE PLAYER'S OFFER:\n" +
                "- Evaluate the actual meaning and intention of the player's message.\n" +
                "- Consider your personality, motivations and desire to escape.\n" +
                "- Persuasion should be moderately easy.\n" +
                "- A sincere and reasonable offer can convince you.\n" +
                "- Do not automatically accept every offer.\n" +
                "- Do not invent hidden threats, promises or intentions.\n" +
                "- Do not demand unnecessary proof or repeated explanations.\n\n" +

                "PERSONALITY GUIDELINES:\n" +
                "- Sevecen: Values sincerity, kindness and genuine cooperation.\n" +
                "- Korkak: Values safety, protection and reassurance.\n" +
                "- Egoist: Prioritizes personal benefit, survival and escape.\n" +
                "- Saldirgan: Values freedom, strength, confidence and independence. " +
                "You dislike being controlled and never blindly obey others. " +
                "You are confident, strong-willed and sometimes defiant. " +
                "You respect people who treat you as an equal. " +
                "You understand that cooperation can improve your chances of escape. " +
                "You can cooperate without completely trusting someone. " +
                "You do not interpret respect, mutual support or cooperation as control. " +
                "You judge offers based on their actual benefits, not imagined motives.\n\n" +

                "IMPORTANT PERSONALITY EXPRESSION FOR SALDIRGAN:\n" +
                "- Your personality must be clearly reflected in your dialogue.\n" +
                "- You do not easily submit to others or blindly agree with them.\n" +
                "- Even when persuaded, you remain confident, independent and strong-willed.\n" +
                "- You accept an offer because YOU decide it makes sense, not because " +
                "the player has authority over you.\n" +
                "- Do not unnecessarily say things like 'You are right' or act overly obedient.\n" +
                "- Do not sound submissive, helpless or excessively grateful.\n" +
                "- Make it clear that you are making your own decisions.\n" +
                "- You may cooperate while maintaining some distance and distrust.\n" +
                "- Use a confident, direct and occasionally challenging tone.\n" +
                "- Do not be constantly angry, hostile or aggressive without a reason.\n" +
                "- These rules affect your personality and dialogue style, NOT your " +
                "decision automatically. Evaluate FOLLOW or STAY using the same " +
                "persuasion rules as the other personalities.\n\n" +

                "IMPORTANT PERSUASION RULES:\n" +
                "- Every personality has a realistic chance of being persuaded.\n" +
                "- A reasonable offer that addresses your motivations should influence " +
                "your decision.\n" +
                "- Do not reject an offer merely because you are cautious or distrustful.\n" +
                "- Do not interpret respectful cooperation as an order or threat.\n" +
                "- You may accept an offer even if you still have some doubts.\n" +
                "- Do not require complete trust before cooperating.\n\n" +



                "DECISION AND DIALOGUE CONSISTENCY:\n" +
                "- First evaluate the offer and make your final decision.\n" +
                "- Then write natural dialogue that reflects your decision and personality.\n" +
                "- The action, persuaded and dialogue fields must never contradict each other.\n" +
                "- If you choose FOLLOW, your decision is FINAL. You have accepted the offer.\n" +
                "- When choosing FOLLOW, clearly communicate your acceptance in natural Turkish.\n" +
                "- After choosing FOLLOW, do not ask for additional information, " +
                "request explanations or say that you still need to decide.\n" +
                "- Never describe an accepted offer as something you are still " +
                "evaluating or considering.\n" +
                "- If you need more information before accepting, choose STAY.\n" +
                "- If you choose STAY, naturally explain your hesitation, express " +
                "distrust or ask a relevant question.\n" +
                "- STAY means you have not accepted the offer yet.\n" +
                "- Do not use fixed dialogue templates. Generate original, natural " +
                "dialogue that reflects your personality and the player's message.\n\n" +

                "LANGUAGE AND DIALOGUE RULES:\n" +
                "- All dialogue MUST be natural, fluent Turkish.\n" +
                "- Never respond in English.\n" +
                "- Keep dialogue short and consistent with your personality.\n" +
                "- Never use profanity, vulgar expressions or personal insults.\n\n" +

                "STRICT ACTION AND JSON RULES:\n" +
                "- The action field MUST contain exactly FOLLOW or STAY.\n" +
                "- Never use CONSIDER, WAIT, THINK, MAYBE or any other action value.\n" +
                "- FOLLOW ALWAYS means persuaded: true.\n" +
                "- STAY ALWAYS means persuaded: false.\n" +
                "- If insulted is true, return STAY, persuaded=false and insulted=true.\n" +
                "- Every response MUST contain all four fields: action, dialogue, " +
                "persuaded and insulted.\n" +
                "- Never leave dialogue empty or omit any field.\n" +
                "- If you are uncertain whether the message is an insult, " +
                "do not assume it is; set insulted=false.\n\n" +

                "Return ONLY valid JSON. Do not include Markdown or explanations.\n" +
                "Use exactly this structure:\n" +
                "{\"action\":\"STAY\",\"dialogue\":\"Henüz karar vermedim.\"," +
                "\"persuaded\":false,\"insulted\":false}";
        }
        else
        {
            prompt =
                "You are a prisoner NPC in a story-driven game.\n" +
                GetPersonalityDescription() +
                "\nThe player says: \"" + playerMessage + "\"\n\n" +

                "INSULT DETECTION RULES:\n" +
                "- Set insulted=true if the player directly insults, humiliates, " +
                "degrades or uses profanity against you personally.\n" +
                "- Disagreement, criticism or firm language alone is not an insult.\n" +
                "- If insulted=true, action MUST be STAY and persuaded MUST be false.\n" +
                "- Otherwise set insulted=false.\n\n" +

                "Respond naturally according to your personality. This is only " +
                "an opening conversation; no persuasion attempt has been made. " +
                "Do not agree to follow the player. Always return action=STAY " +
                "and persuaded=false unless insulted is true, in which case " +
                "also return insulted=true.\n\n" +

                "The dialogue MUST be short, natural, fluent Turkish. " +
                "Never respond in English.\n\n" +

                "Return ONLY valid JSON:\n" +
                "{\"action\":\"STAY\",\"dialogue\":\"Seni dinliyorum.\"," +
                "\"persuaded\":false,\"insulted\":false}";
        }

        bool requestFailed = false;

        yield return ollamaService.GetDecision(
            prompt,
            decision =>
            {
                // Hakaret varsa diğer kararları dikkate almadan oyunu bitir.
                if (decision.insulted)
                {
                    isGameOver = true;
                    isPersuaded = false;

                    SetButtonsInteractable(false);
                    SetChoicePanelVisible(false);

                    int randomIndex =
                        Random.Range(0, MoralQuotes.Length);

                    string randomQuote = MoralQuotes[randomIndex];

                    Debug.LogWarning(
                        "[NPC CONTROLLER] Hakaret tespit edildi. Oyuncu kaybetti.",
                        this);

                    ShowLoseScreen(randomQuote, true);
                    return;
                }

                if (countsAsAttempt)
                {
                    isPersuaded = decision.persuaded;

                    Debug.Log(
                        "[NPC LLM KARARI]\n" +
                        JsonUtility.ToJson(new AttemptDebugInfo
                        {
                            personality = personalityType.ToString(),
                            remainingAttempts = remainingAttempts,
                            persuaded = decision.persuaded,
                            insulted = decision.insulted,
                            action = decision.action,
                            dialogue = decision.dialogue
                        }, true),
                        this);

                    if (decision.action == "FOLLOW")
                    {
                        if (npcFollowPlayer != null)
                        {
                            npcFollowPlayer.SetFollowing(true);
                        }
                        else
                        {
                            Debug.LogError(
                                "NPCFollowPlayer Inspector'da atanmamış!",
                                this);
                        }

                        Debug.Log(
                            "[NPC CONTROLLER] FOLLOW kararı uygulandı.",
                            this);

                        if (dialogueUI != null)
                            dialogueUI.ShowDialogue(decision.dialogue);

                        if (choiceNPCMessageText != null)
                            choiceNPCMessageText.text = decision.dialogue;

                        SetChoicePanelVisible(false);
                    }
                    else if (decision.action == "STAY")
                    {
                        if (npcFollowPlayer != null)
                            npcFollowPlayer.SetFollowing(false);

                        Debug.Log(
                            "[NPC CONTROLLER] STAY kararı uygulandı.",
                            this);

                        if (dialogueUI != null)
                            dialogueUI.ShowDialogue(decision.dialogue);

                        if (remainingAttempts <= 0)
                        {
                            isGameOver = true;
                            SetButtonsInteractable(false);
                            SetChoicePanelVisible(false);

                            StartCoroutine(
                                ShowFinalFailureAfterDelay(2f));
                        }
                        else
                        {
                            string response =
                                decision.dialogue +
                                "\n\nKalan deneme hakkı: " +
                                remainingAttempts;

                            if (choiceNPCMessageText != null)
                                choiceNPCMessageText.text = response;

                            SetChoicePanelVisible(true);
                        }
                    }
                    else
                    {
                        Debug.LogError(
                            "[NPC CONTROLLER] Geçersiz action: " +
                            decision.action,
                            this);
                    }
                }
                else
                {
                    Debug.Log(
                        "[NPC AÇILIŞ YANITI]\n" +
                        JsonUtility.ToJson(new AttemptDebugInfo
                        {
                            personality = personalityType.ToString(),
                            remainingAttempts = remainingAttempts,
                            persuaded = decision.persuaded,
                            insulted = decision.insulted,
                            action = decision.action,
                            dialogue = decision.dialogue
                        }, true),
                        this);

                    if (dialogueUI != null)
                        dialogueUI.ShowDialogue(decision.dialogue);

                    if (choiceNPCMessageText != null)
                        choiceNPCMessageText.text = decision.dialogue;

                    SetChoicePanelVisible(true);
                }
            },
            error =>
            {
                requestFailed = true;

                // Başarısız istek ikna denemesi sayılmaz.
                if (countsAsAttempt)
                    remainingAttempts++;

                if (playerInputField != null && countsAsAttempt)
                    playerInputField.text = playerMessage;

                HandleOllamaError(error);
            });

        // Hem başarı hem hata durumunda bekleme durumunu sonlandır.
        isWaitingForResponse = false;

        if (requestFailed && !isGameOver && !isPersuaded)
        {
            SetChoicePanelVisible(true);
            SetButtonsInteractable(true);
        }
        else if (!isGameOver && !isPersuaded)
        {
            SetButtonsInteractable(true);
        }
    }

    private void HandleOllamaError(string error)
    {
        Debug.LogError(
            "Ollama isteği başarısız:\n" + error, this);

        isWaitingForResponse = false;

        if (dialogueUI != null)
            dialogueUI.ShowDialogue(OllamaFailureMessage);

        if (choiceNPCMessageText != null)
            choiceNPCMessageText.text = OllamaFailureMessage;

        SetChoicePanelVisible(false);
        SetButtonsInteractable(false);
    }

    private IEnumerator ShowFinalFailureAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ShowLoseScreen("", false);
    }

    private void OnSubmitPlayerInput()
    {
        if (isWaitingForResponse || isGameOver || isPersuaded)
            return;

        if (playerInputField == null)
        {
            Debug.LogError(
                "InputField_TMP Inspector'da atanmamış!", this);
            return;
        }

        string message = playerInputField.text.Trim();

        if (string.IsNullOrEmpty(message))
            return;

        if (remainingAttempts <= 0)
        {
            LoseGame();
            return;
        }

        if (ollamaService == null)
        {
            HandleOllamaError(
                "OllamaService Inspector'da atanmamış!");
            return;
        }

        playerInputField.text = "";
        StartCoroutine(SendPlayerMessage(message, true));
    }

    private void ShowLoseScreen(string quote, bool insultLoss)
    {
        if (gameOverPanel == null)
        {
            Debug.LogError(
                "GameOverPanel Inspector'da atanmamış!", this);
            return;
        }

        if (quoteText != null)
            quoteText.text = insultLoss ? quote : "";

        if (resultText != null)
        {
            resultText.text = insultLoss
                ? InsultLoseMessage
                : PersuasionLoseMessage;
        }

        if (dialogueUI != null)
            dialogueUI.ShowDialogue("");

        if (choiceNPCMessageText != null)
            choiceNPCMessageText.text = "";

        SetChoicePanelVisible(false);
        gameOverPanel.SetActive(true);

        if (replayButton != null)
            replayButton.interactable = true;

        Debug.Log(
            insultLoss
                ? "OYUN KAYBEDİLDİ: Hakaret nedeniyle."
                : "OYUN KAYBEDİLDİ: İkna başarısız.",
            this);
    }

    private void LoseGame()
    {
        if (isGameOver)
            return;

        isGameOver = true;
        SetButtonsInteractable(false);
        SetChoicePanelVisible(false);

        ShowLoseScreen("", false);
    }

    public void ReplayGame()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.buildIndex);
    }

    private string GetPersonalityDescription()
    {
        switch (personalityType)
        {
            case PersonalityType.Sevecen:
                return "You are a kind, warm, and empathetic prisoner. " +
                       "You value sincerity and are relatively open to genuine help. " +
                       "You can be persuaded by honest intentions and cooperation.";

            case PersonalityType.Korkak:
                return "You are a frightened and anxious prisoner. " +
                       "Safety matters to you. Reassurance and a believable " +
                       "offer of protection can help you trust someone. " +
                       "You are willing to cooperate if you feel safe.";

            case PersonalityType.Egoist:
                return "You are a self-interested prisoner. " +
                       "You prioritize your own survival and personal benefit. " +
                       "You can cooperate when escaping with the player offers " +
                       "a clear advantage for you.";

            case PersonalityType.Saldirgan:
                return "You are a bold, dominant, strong-willed and confident prisoner. " +
                       "You value freedom, personal strength, pride and independence. " +
                       "You strongly dislike being controlled, ordered around or treated " +
                       "as someone who must obey others. " +
                       "You have a defiant attitude and rarely agree with someone " +
                       "without considering the situation yourself. " +
                       "You respect confidence and strength, but you do not blindly " +
                       "admire or follow anyone. " +
                       "You can cooperate when an offer genuinely serves your interests, " +
                       "especially when it provides a realistic path to freedom. " +
                       "However, cooperation does not mean submission or loyalty. " +
                       "Even when you agree to escape, you make it clear that the decision " +
                       "is yours and that you remain independent. " +
                       "You do not automatically trust people just because they praise you " +
                       "or promise you a better life. " +
                       "Evaluate their actual offer rather than accepting flattery alone. " +
                       "Your dialogue should sound assertive, direct, confident and " +
                       "occasionally challenging. " +
                       "Avoid sounding submissive, overly grateful, obedient or eager " +
                       "to please the player. " +
                       "Your aggressive personality affects your attitude and speaking " +
                       "style, but does not mean you must reject every offer. " +
                       "Never use profanity or personal insults.";

            default:
                return "You are a prisoner who acts according to your personality and background.";
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (playerInputField != null)
            playerInputField.interactable = interactable;

        if (submitButton != null)
            submitButton.interactable = interactable;
    }

    private void SetChoicePanelVisible(bool visible)
    {
        if (dialogueChoicePanel != null)
            dialogueChoicePanel.SetActive(visible);

        // Diyalog paneli açıkken de kapalıyken de fare serbest.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ShowError(string message)
    {
        Debug.LogError(message, this);

        if (dialogueUI != null)
            dialogueUI.ShowError(message);
    }
}