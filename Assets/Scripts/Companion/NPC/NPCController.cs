
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

    [Header("NPC Kişiliği")]
    [SerializeField] private PersonalityType personalityType =
        PersonalityType.Sevecen;

    private const int MaxAttempts = 3;

    private const string InsultLoseMessage =
        "Oyunu kaybettin, vay ahlak düşmanı vay...";

    private const string PersuasionLoseMessage =
        "İKNA EDEMEDİNİZ...";

    private const string FallbackQuote =
        "İnsan, başkasını küçülterek kendini yüceltemez.";

    private int remainingAttempts = MaxAttempts;
    private bool isWaitingForResponse;
    private bool isPersuaded;
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
                    "Seni burada bırakmak istemiyorum. Gel, birlikte çıkalım.",
                    "Yardımına ihtiyacın olduğunu görüyorum. Yanında olmak istiyorum.",
                    "Bana hemen güvenmek zorunda değilsin. Sana içtenlikle yardım etmek istiyorum."
                };
                break;

            case PersonalityType.Korkak:
                currentOptions = new[]
                {
                    "Korktuğunu anlayabiliyorum. Elimden geldiğince seni koruyacağım.",
                    "Bunu tek başına yaşamak zorunda değilsin. İstersen yanında kalırım.",
                    "Acele etmeyelim. Birlikte daha güvenli bir yere gidebiliriz."
                };
                break;

            case PersonalityType.Egoist:
                currentOptions = new[]
                {
                    "Senin de kurtulmayı hak ettiğini düşünüyorum. Sana yardım etmek istiyorum.",
                    "Seni kullanmaya çalışmıyorum. İkimizin de kurtulmasını istiyorum.",
                    "Yeteneklerine güveniyorum. Birbirimize destek olup buradan çıkabiliriz."
                };
                break;

            case PersonalityType.Saldirgan:
                currentOptions = new[]
                {
                    "Burada çok şey yaşamış olmalısın. Seni yargılamıyorum, yardım etmek istiyorum.",
                    "Sana ne yapacağını söylemeyeceğim. Kararına saygı duyuyorum.",
                    "Bana hemen güvenmeni beklemiyorum. Yine de seni burada bırakmak istemiyorum."
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
            Debug.LogError("Diyalog butonu Inspector'da atanmamış!", this);
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
            ShowError("OllamaService Inspector'da atanmamış!");
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
        if (isGameOver || isPersuaded)
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

    private IEnumerator SendPlayerMessage(
        string playerMessage,
        bool countsAsAttempt)
    {
        if (ollamaService == null)
        {
            if (countsAsAttempt)
                remainingAttempts++;

            ShowError("OllamaService Inspector'da atanmamış!");
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
                "Sen bir hikâye oyunundaki mahkûm NPC'sin.\n" +
                GetPersonalityDescription() +
                "\nOyuncunun sana söylediği söz: \"" +
                playerMessage + "\"\n\n" +
                "Oyuncu sana içtenlikle yardım etmek ve seni buradan " +
                "çıkarmak istiyor. Söylediklerini bu bağlamda değerlendir.\n\n" +
                "Samimi ve kişiliğine uygun bir yardım teklifi genellikle " +
                "kabul etmek için yeterlidir. Sürekli şüphe üretme, " +
                "yeni şartlar koyma veya kanıt isteme. Her karakter ikna " +
                "olabilir ama kararını kendin ver.\n\n" +
                "İkna olduysan persuaded=true ve action=FOLLOW döndür. " +
                "İkna olmadıysan persuaded=false ve action=STAY döndür. " +
                "Kısa, doğal ve kişiliğine uygun Türkçe cevap ver. " +
                "Kabul ediyorsan oyuncuyla geleceğini açıkça söyle.\n\n" +
                "Yalnızca JSON döndür:\n" +
                "{\"action\":\"FOLLOW\",\"dialogue\":\"Seninle geliyorum.\"," +
                "\"persuaded\":true}";
        }
        else
        {
            prompt =
                "Sen bir hikâye oyunundaki mahkûm NPC'sin.\n" +
                GetPersonalityDescription() +
                "\nOyuncu seninle konuşuyor: \"" +
                playerMessage + "\"\n\n" +
                "Kişiliğine uygun, doğal ve kısa bir Türkçe cevap ver. " +
                "Oyuncu henüz ikna denemesi yapmadı. Şimdilik ikna olma " +
                "veya takip etme kararı verme. persuaded=false ve " +
                "action=STAY döndür. Oyuncuyla konuşmaya açık ol.\n" +
                "Yalnızca JSON döndür:\n" +
                "{\"action\":\"STAY\",\"dialogue\":\"Seni dinliyorum.\"," +
                "\"persuaded\":false}";
        }

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
                        if (dialogueUI != null)
                            dialogueUI.ShowDialogue(decision.dialogue);

                        if (choiceNPCMessageText != null)
                            choiceNPCMessageText.text = decision.dialogue;

                        SetChoicePanelVisible(false);
                    }
                    else if (remainingAttempts <= 0)
                    {
                        LoseGame();
                    }
                    else
                    {
                        string response =
                            decision.dialogue +
                            "\n\nKalan deneme hakkı: " + remainingAttempts;

                        if (dialogueUI != null)
                            dialogueUI.ShowDialogue(response);

                        if (choiceNPCMessageText != null)
                            choiceNPCMessageText.text = response;

                        SetChoicePanelVisible(true);
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
                if (countsAsAttempt)
                    remainingAttempts++;

                ShowError("Ollama hatası:\n" + error);
                SetChoicePanelVisible(true);
            });

        isWaitingForResponse = false;

        if (!isGameOver && !isPersuaded)
            SetButtonsInteractable(true);
    }

    private void OnFinalInsultSelected()
    {
        if (isWaitingForResponse || isGameOver || isPersuaded)
            return;

        isGameOver = true;
        SetButtonsInteractable(false);
        SetChoicePanelVisible(false);

        if (ollamaService == null)
        {
            ShowLoseScreen(FallbackQuote, true);
            return;
        }

        string quotePrompt =
            "Türkçe, kısa, etkileyici ve düşündürücü tek bir özlü söz üret. " +
            "Konu: hakaret etmek, kibir ve insanları küçümsemenin sonucu. " +
            "NPC kişiliği: " + GetPersonalityDescription() +
            "\nYalnızca bu JSON biçiminde yanıt ver:\n" +
            "{\"quote\":\"özlü söz\"}";

        StartCoroutine(ollamaService.GetQuote(
            quotePrompt,
            quote =>
            {
                ShowLoseScreen(quote, true);
            },
            error =>
            {
                Debug.LogWarning("Özlü söz üretilemedi: " + error, this);
                ShowLoseScreen(FallbackQuote, true);
            }));
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
                return "Sevecen, sıcak ve empatik bir mahkûmsun. " +
                       "İnsanların samimiyetine değer verirsin. " +
                       "İçten bir yardım teklifini kabul etmeye açıksın.";

            case PersonalityType.Korkak:
                return "Korkmuş ve tedirgin bir mahkûmsun. " +
                       "Güvenlik senin için önemlidir. Birinin sana " +
                       "destek olacağını içtenlikle söylemesi seni rahatlatır " +
                       "ve yardım teklifini kabul etmene yardımcı olabilir.";

            case PersonalityType.Egoist:
                return "Kendi çıkarını önemseyen bir mahkûmsun. " +
                       "Kendi kurtuluşun için makul bir fırsat gördüğünde " +
                       "iş birliği yapabilirsin. Her teklifte büyük bir kazanç " +
                       "veya garanti aramazsın.";

            case PersonalityType.Saldirgan:
                return "Öfkeli, sert ve insanlara karşı temkinli bir " +
                       "mahkûmsun. Ancak anlaşılmak ve saygı görmek senin " +
                       "için önemlidir. Seni yargılamadan, kibarca ve içtenlikle " +
                       "yardım etmek isteyen birine güvenmeye başlayabilirsin.";

            default:
                return "Geçmişine ve kişiliğine uygun davranan bir mahkûmsun.";
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

        Cursor.visible = visible;
        Cursor.lockState = visible
            ? CursorLockMode.None
            : CursorLockMode.Locked;
    }

    private void ShowError(string message)
    {
        Debug.LogError(message, this);

        if (dialogueUI != null)
            dialogueUI.ShowError(message);
    }
}
