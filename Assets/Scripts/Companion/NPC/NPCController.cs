using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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

    [Header("Kişiliğe Özel 3 Seçenek")]
    [SerializeField] private Button helpChoiceButton;
    [SerializeField] private Button ignoreChoiceButton;
    [SerializeField] private Button insultChoiceButton;

    [Header("En Alttaki Hakaret Butonu")]
    [SerializeField] private Button finalInsultButton;

    [Header("Oyun Sonu Ekranı")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMPro.TMP_Text quoteText;
    [SerializeField] private TMPro.TMP_Text resultText;
    [SerializeField] private Button replayButton;

    [Header("NPC Takip")]
    [SerializeField] private NPCFollowPlayer npcFollowPlayer;

    [Header("NPC Kişiliği")]
    [SerializeField] private PersonalityType personalityType =
        PersonalityType.Sevecen;

    private const int MaxAttempts = 3;

    private const string InsultLoseMessage =
        "Oyunu kaybettin, vay ahlak düşmanı vay...";

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

    private string[] currentOptions = new string[3];

    [System.Serializable]
    private class AttemptDebugInfo
    {
        public string personality;
        public int remainingAttempts;
        public bool persuaded;
        public string action;
    }

    private void Awake()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        SetChoicePanelVisible(false);
        ConfigurePersonalityOptions();

        if (helpChoiceButton != null)
            helpChoiceButton.onClick.AddListener(OnOption1Selected);

        if (ignoreChoiceButton != null)
            ignoreChoiceButton.onClick.AddListener(OnOption2Selected);

        if (insultChoiceButton != null)
            insultChoiceButton.onClick.AddListener(OnOption3Selected);

        if (finalInsultButton != null)
            finalInsultButton.onClick.AddListener(OnFinalInsultSelected);

        if (replayButton != null)
            replayButton.onClick.AddListener(ReplayGame);
    }

    private void Start()
    {
        if (npcFollowPlayer != null)
            npcFollowPlayer.SetFollowing(false);
    }

    private void OnDestroy()
    {
        if (helpChoiceButton != null)
            helpChoiceButton.onClick.RemoveListener(OnOption1Selected);

        if (ignoreChoiceButton != null)
            ignoreChoiceButton.onClick.RemoveListener(OnOption2Selected);

        if (insultChoiceButton != null)
            insultChoiceButton.onClick.RemoveListener(OnOption3Selected);

        if (finalInsultButton != null)
            finalInsultButton.onClick.RemoveListener(OnFinalInsultSelected);

        if (replayButton != null)
            replayButton.onClick.RemoveListener(ReplayGame);
    }

    private void ConfigurePersonalityOptions()
    {
        switch (personalityType)
        {
            case PersonalityType.Sevecen:
                currentOptions = new[]
                {
                    "Gülümsemek de bir sadakadır, karakterine saygı duyuyorum, benimle gelmelisin...",
                    "Sevecen insanlar neye güveneceğini bilir, bana güven sana yardım etmek istiyorum.",
                    "Kendine iyilik et ve benimle birlikte gel."
                };
                break;

            case PersonalityType.Korkak:
                currentOptions = new[]
                {
                    "İnsan zihninden kaçtığı şeylerden korkar, zihninden kaçmazsan korkunu yenebilirsin...",
                    "Sana güveniyorum, el ele verirsek güçlenebiliriz.",
                    "İnsanların yardım isteklerini kabul etmek de o insana bir yardımdır..."
                };
                break;

            case PersonalityType.Egoist:
                currentOptions = new[]
                {
                    "Küçük şeyler BÜYÜK detaylarda saklıdır, bazen küçük olmak bir inceliktir.",
                    "Kurtulmak istersen eğer sana gerçek dünyayı tanıtabilirim.",
                    "Egoyu bir kenara bırakabilmek BÜYÜKLÜKTÜR. Sana yardım edebilirim..."
                };
                break;

            case PersonalityType.Saldirgan:
                currentOptions = new[]
                {
                    "Buranın yerlisiyim, seni gördüm belki açsındır, yemek ister misin?",
                    "Yardım etmeye çalisiyorum, sana zarar veremem çünkü yaşlıyım.",
                    "Çıkarcı biri olsaydım sana boş yere yemek vermek istemezdim... Ne işime yarardın ki?"
                };
                break;
        }

        SetButtonLabel(helpChoiceButton, currentOptions[0]);
        SetButtonLabel(ignoreChoiceButton, currentOptions[1]);
        SetButtonLabel(insultChoiceButton, currentOptions[2]);
        SetButtonLabel(finalInsultButton, "Hakaret Et");
    }

    private void SetButtonLabel(Button button, string label)
    {
        if (button == null)
        {
            Debug.LogError(
                "Diyalog butonu Inspector'da atanmamış!", this);
            return;
        }

        Text labelText = button.GetComponentInChildren<Text>(true);

        if (labelText != null)
            labelText.text = label;
        else
            Debug.LogWarning(
                button.name + " altında UI Text bileşeni bulunamadı.",
                button);
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

    private void OnOption1Selected()
    {
        SelectOption(0);
    }

    private void OnOption2Selected()
    {
        SelectOption(1);
    }

    private void OnOption3Selected()
    {
        SelectOption(2);
    }

    private void SelectOption(int optionIndex)
    {
        if (isWaitingForResponse || isGameOver || isPersuaded)
            return;

        if (remainingAttempts <= 0)
        {
            LoseGame();
            return;
        }

        if (optionIndex < 0 || optionIndex >= currentOptions.Length)
            return;

        remainingAttempts--;

        StartCoroutine(SendPlayerMessage(
            currentOptions[optionIndex], true));
    }
private void OnApplicationFocus(bool hasFocus)
{
    if (!hasFocus)
        return;

    // Unity Game penceresine geri dönüldüğünde
    // imleç tekrar görünür ve serbest olsun.
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
        SetButtonsInteractable(false);

        if (dialogueUI != null)
            dialogueUI.ShowThinking();

        string prompt;

        if (countsAsAttempt)
        {
            prompt =
                "You are a prisoner NPC in a story-driven game.\n" +
                GetPersonalityDescription() +
                "\nThe player's chosen message is: \"" +
                playerMessage + "\"\n\n" +
                "Evaluate the offer based on your personality and the meaning " +
                "of the player's words. Persuasion should be moderately easy. " +
                "A sincere and reasonable offer of help will often be convincing. " +
                "Do not demand proof or invent unnecessary conditions, but do " +
                "not automatically accept every option.\n\n" +
                "Personality guidelines:\n" +
                "- Sevecen: Trusts sincere people relatively easily.\n" +
                "- Korkak: Values safety and responds well to reassurance.\n" +
                "- Egoist: Considers personal benefit and the chance to escape.\n" +
                "- Saldirgan: Is cautious but can accept respectful, sincere help.If he approaches with sincerity, he may become more polite and might accept.\n\n" +
                "If persuaded, return persuaded=true and action=FOLLOW. " +
                "Otherwise, return persuaded=false and action=STAY. " +
                "All dialogue shown to the player MUST be natural Turkish, " +
                "never English. Keep it short and consistent with your personality. " +
                "If persuaded, clearly agree to leave with the player. " +
                "If not persuaded, briefly explain your hesitation.\n\n" +
                "Return ONLY valid JSON in this format:\n" +
                "{\"action\":\"STAY\",\"dialogue\":\"Henüz karar vermedim.\"," +
                "\"persuaded\":false}";
        }
        else
        {
            prompt =
                "You are a prisoner NPC in a story-driven game.\n" +
                GetPersonalityDescription() +
                "\nThe player says: \"" + playerMessage + "\"\n\n" +
                "Respond naturally according to your personality. This is only " +
                "an opening conversation; no persuasion attempt has been made. " +
                "Do not agree to follow the player. Return persuaded=false and " +
                "action=STAY.\n\n" +
                "The dialogue MUST be short, natural Turkish, never English.\n" +
                "Return ONLY valid JSON:\n" +
                "{\"action\":\"STAY\",\"dialogue\":\"Seni dinliyorum.\"," +
                "\"persuaded\":false}";
        }

        bool requestFailed = false;

        yield return ollamaService.GetDecision(
            prompt,
            decision =>
            {
                if (countsAsAttempt)
                {
                    isPersuaded = decision.persuaded;
                    decision.action = isPersuaded ? "FOLLOW" : "STAY";

                    if (isPersuaded)
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

                        if (dialogueUI != null)
                            dialogueUI.ShowDialogue(decision.dialogue);

                        if (choiceNPCMessageText != null)
                            choiceNPCMessageText.text = decision.dialogue;

                        SetChoicePanelVisible(false);
                    }
                    else
                    {
                        if (dialogueUI != null)
                            dialogueUI.ShowDialogue(decision.dialogue);

                        if (choiceNPCMessageText != null)
                            choiceNPCMessageText.text = decision.dialogue;

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
                }
                else
                {
                    if (dialogueUI != null)
                        dialogueUI.ShowDialogue(decision.dialogue);

                    if (choiceNPCMessageText != null)
                        choiceNPCMessageText.text = decision.dialogue;

                    SetChoicePanelVisible(true);
                }

                Debug.Log(JsonUtility.ToJson(new AttemptDebugInfo
                {
                    personality = personalityType.ToString(),
                    remainingAttempts = remainingAttempts,
                    persuaded = decision.persuaded,
                    action = decision.action
                }), this);
            },
            error =>
            {
                requestFailed = true;

                // Başarısız istek ikna denemesi sayılmaz.
                if (countsAsAttempt)
                    remainingAttempts++;

                HandleOllamaError(error);
            });

        // Hem başarı hem hata durumunda bekleme durumu sonlandırılır.
        isWaitingForResponse = false;

        // Hata sonrasında butonları etkinleştirme.
        // Başarılı yanıt geldiyse normal akış devam eder.
        if (!requestFailed && !isGameOver && !isPersuaded)
            SetButtonsInteractable(true);
    }

    private void HandleOllamaError(string error)
    {
        // Teknik ayrıntı Console'a yazılır, balonda gösterilmez.
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

   private void OnFinalInsultSelected()
{
    if (isWaitingForResponse || isGameOver || isPersuaded)
        return;

    isGameOver = true;
    SetButtonsInteractable(false);
    SetChoicePanelVisible(false);

    // Ahlak sözlerinden rastgele birini seç.
    int randomIndex = Random.Range(0, MoralQuotes.Length);
    string randomQuote = MoralQuotes[randomIndex];

    // Kaybetme ekranında üst mesaj olarak göster.
    ShowLoseScreen(randomQuote, true);
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
                       "You value sincerity and are relatively open to genuine help.";

            case PersonalityType.Korkak:
                return "You are a frightened and anxious prisoner. " +
                       "Safety matters to you. Reassurance and a believable " +
                       "offer of protection can help you trust someone.";

            case PersonalityType.Egoist:
                return "You are a self-interested prisoner. " +
                       "You consider your chance of escaping and can cooperate " +
                       "when the offer seems beneficial.";

            case PersonalityType.Saldirgan:
                return "You are an angry, tough, and cautious prisoner. " +
                       "You do not trust people easily, but respectful and " +
                       "sincere help can convince you.";

            default:
                return "You are a prisoner who acts according to your personality and background.";
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (helpChoiceButton != null)
            helpChoiceButton.interactable = interactable;

        if (ignoreChoiceButton != null)
            ignoreChoiceButton.interactable = interactable;

        if (insultChoiceButton != null)
            insultChoiceButton.interactable = interactable;

        if (finalInsultButton != null)
            finalInsultButton.interactable = interactable;
    }

    private void SetChoicePanelVisible(bool visible)
{
    if (dialogueChoicePanel != null)
        dialogueChoicePanel.SetActive(visible);

    // Diyalog seçenekleri açıkken fare serbest olsun.
    if (visible)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    else
    {
        // Oyun penceresi dışına tıklayınca imleci
        // zorla kilitleme; oyuncu UI ile etkileşebilsin.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}

    private void ShowError(string message)
    {
        Debug.LogError(message, this);

        if (dialogueUI != null)
            dialogueUI.ShowError(message);
    }
}
