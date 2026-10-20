using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// NPC yakınlık kontrolünü, ekran prompt'unu ("E - Konuş") ve E tuşuna basıldığında
/// Ollama servisi ile Mahkum1 üzerindeki WorldSpaceBubblePresenter arasındaki etkileşimi yönetir.
/// </summary>
public class NPCInteractionPrompt : MonoBehaviour
{
    [Header("Hedef ve Oyuncu Ayarları")]
    [Tooltip("Mahkum1 veya etkileşime geçilecek NPC Transform'u. Boş bırakılırsa bu GameObject kullanılır.")]
    [SerializeField] private Transform targetNPC;

    [Tooltip("Oyuncu nesnesinin Transform'u. Boş bırakılırsa sahnede otomatik aranır ('Player' Tag veya Controller).")]
    [SerializeField] private Transform playerTransform;

    [Header("Mesafe Ayarı")]
    [Tooltip("Etkileşim yazısının belireceği mesafe (varsayılan: 3 birim).")]
    [SerializeField] private float interactionDistance = 3.0f;

    [Header("UI Ayarları")]
    [Tooltip("Ekranda gösterilecek UI TextMeshProUGUI bileşeni. Boş bırakılırsa sahnede aranır veya otomatik oluşturulur.")]
    [SerializeField] private TextMeshProUGUI promptTextUI;

    [Tooltip("Ekranda görünecek etkileşim metni.")]
    [SerializeField] private string promptMessage = "E - Konuş";

    [Header("AI & Konuşma Balonu Ayarları")]
    [Tooltip("Ollama ile iletişim kuracak servis bileşeni (boş bırakılırsa sahnede otomatik aranır).")]
    [SerializeField] private OllamaService ollamaService;

    [Tooltip("Mahkum1 üzerindeki konuşma balonu bileşeni (boş bırakılırsa sahnede otomatik aranır).")]
    [SerializeField] private WorldSpaceBubblePresenter bubblePresenter;

    [Tooltip("E tuşuna basıldığında karaktere iletilecek başlangıç konuşma promptu.")]
    [SerializeField] private string interactionPrompt = "Merhaba, sana yardım etmek istiyorum. Benimle güvenli alana gelebilirsin, eğer istersen.";

    private const string HelpMessage = "Seni bu hayattan kurtarmak, güvenli bir alana götürmek istiyorum. Benimle gelir misin?";
    private const string IgnoreMessage = "Başka kapıya kardeşim.";
    private const string InsultMessage = "Ne zorba bir insansın lan! Düzgün oyna şu oyunu";

    private bool isPlayerClose = false;
    private bool hasLoggedPlayerError = false;
    private bool hasLoggedTargetError = false;
    private bool hasLoggedServiceWarning = false;
    private bool hasLoggedBubbleWarning = false;
    private Transform interactionTarget;
    private GameObject dialoguePanel;
    private Button[] choiceButtons = new Button[3];
    private StarterAssets.StarterAssetsInputs playerInputs;
    private bool isDialogueRequestInProgress;
    private bool hasStartedConversation;
    private bool isGameLost;

    private void Awake()
    {
        InitializeReferences();
        InitializeDialogueUI();
        SetPromptVisible(false);
        SetDialoguePanelVisible(false);
    }

    private void Start()
    {
        // Başlangıçta yazının gizli olmasını garanti et
        SetPromptVisible(false);
    }

    private void OnDisable()
    {
        isPlayerClose = false;
        SetPromptVisible(false);
    }

    private void Update()
    {
        if (isGameLost) return;

        if (targetNPC == null || playerTransform == null || playerTransform == targetNPC)
        {
            // Referanslar henüz atanmadıysa veya sahnede geç yüklendiyse tekrar kontrol et
            InitializeReferences();
            if (targetNPC == null || playerTransform == null || playerTransform == targetNPC) return;
        }

        // Oyuncu ile Mahkum1 arasındaki mesafeyi ölç
        Transform currentTarget = GetCurrentInteractionTarget();
        float distance = Vector3.Distance(playerTransform.position, currentTarget.position);

        // 3 birim veya daha uzaktaysa görünmemeli, 3 birimden daha yakına geldiğinde görünmeli
        bool shouldBeVisible = distance < interactionDistance;

        if (shouldBeVisible != isPlayerClose)
        {
            isPlayerClose = shouldBeVisible;
            Debug.Log($"[NPCInteractionPrompt] Oyuncu: {playerTransform.position}, NPC hedefi ({currentTarget.name}): {currentTarget.position}, mesafe: {distance:F2}, prompt: {isPlayerClose}", this);
            SetPromptVisible(isPlayerClose && !hasStartedConversation);

            if (!isPlayerClose)
            {
                SetDialoguePanelVisible(false);
            }
            else if (hasStartedConversation && !isDialogueRequestInProgress)
            {
                SetDialoguePanelVisible(true);
                UnlockCursorForDialogue();
            }
        }

        // Oyuncu menzildeyken E tuşuna basarsa konuşmayı başlat
        if (isPlayerClose && IsInteractKeyPressed())
        {
            TryStartDialogue();
        }
    }

    /// <summary>
    /// Hem yeni Input System hem de eski Input Manager ile uyumlu E tuşu algılayıcı.
    /// Projedeki activeInputHandler ayarına göre en uygun girişi dinler.
    /// </summary>
    private bool IsInteractKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            return true;
        }
#endif
        return Input.GetKeyDown(KeyCode.E);
    }

    /// <summary>
    /// Oyuncu yakındayken ve servis meşgul değilken diyalog akışını tetikler.
    /// </summary>
    public void TryStartDialogue()
    {
        // Oyuncu uzaklaştıysa konuşma başlatılamaz
        if (!isPlayerClose || isGameLost || hasStartedConversation || isDialogueRequestInProgress) return;

        // Referanslar henüz çözülmediyse bulmayı dene
        if (ollamaService == null)
        {
            ResolveOllamaService();
        }

        if (bubblePresenter == null)
        {
            ResolveBubblePresenter();
        }

        // 1. OllamaService kontrolü
        if (ollamaService == null)
        {
            if (!hasLoggedServiceWarning)
            {
                Debug.LogWarning("[NPCInteractionPrompt] 'OllamaService' referansı atanmamış ve sahnede bulunamadı! Lütfen Mahkum1 nesnesine OllamaService bileşeni ekleyin veya Inspector'dan atayın.", this);
                hasLoggedServiceWarning = true;
            }

            if (bubblePresenter != null)
            {
                bubblePresenter.ShowError("AI servisi bağlı değil!");
            }
            return;
        }

        // 2. İstek sürerken tekrar tekrar istek gönderilmesini engelle
        if (ollamaService.IsBusy)
        {
            return;
        }

        hasStartedConversation = true;
        isDialogueRequestInProgress = true;
        SetPromptVisible(false);
        SetChoicesInteractable(false);

        // 3. WorldSpaceBubblePresenter kontrolü ve 'Düşünüyor...' durumu
        if (bubblePresenter == null)
        {
            if (!hasLoggedBubbleWarning)
            {
                Debug.LogWarning("[NPCInteractionPrompt] 'WorldSpaceBubblePresenter' referansı bulunamadı! Lütfen Mahkum1/HeadBubble nesnesini bağlayın.", this);
                hasLoggedBubbleWarning = true;
            }
        }
        else
        {
            // İstek gönderilir gönderilmez Mahkum1 üzerinde 'Düşünüyor...' balonunu aç
            bubblePresenter.ShowThinking();
        }

        // 4. Ollama'ya asenkron istek gönder (ana oyun döngüsü bloklanmaz)
        ollamaService.RequestResponse(interactionPrompt, OnInitialDialogueSuccess, OnInitialDialogueError);
    }

    private void OnInitialDialogueSuccess(string response)
    {
        isDialogueRequestInProgress = false;
        OnDialogueSuccess(response);
        SetDialoguePanelVisible(isPlayerClose);
        SetChoicesInteractable(true);
        if (isPlayerClose)
        {
            UnlockCursorForDialogue();
        }
    }

    private void OnInitialDialogueError(string errorMessage)
    {
        isDialogueRequestInProgress = false;
        hasStartedConversation = false;
        OnDialogueError(errorMessage);
        SetPromptVisible(isPlayerClose);
    }

    private void SelectHelp()
    {
        SendDialogueChoice(HelpMessage);
    }

    private void SelectIgnore()
    {
        SendDialogueChoice(IgnoreMessage);
    }

    private void SelectInsult()
    {
        if (isGameLost || isDialogueRequestInProgress || !hasStartedConversation || !isPlayerClose) return;
        EnterLossState();
    }

    private void SendDialogueChoice(string playerMessage)
    {
        if (isGameLost || isDialogueRequestInProgress || !hasStartedConversation || !isPlayerClose) return;

        if (ollamaService == null)
        {
            ResolveOllamaService();
        }

        if (ollamaService == null)
        {
            Debug.LogError("[NPCInteractionPrompt] Diyalog seçeneği gönderilemedi: OllamaService bulunamadı.", this);
            OnDialogueError("AI servisi bağlı değil!");
            return;
        }

        if (ollamaService.IsBusy) return;

        isDialogueRequestInProgress = true;
        SetChoicesInteractable(false);

        if (bubblePresenter != null)
        {
            bubblePresenter.ShowThinking();
        }

        ollamaService.RequestResponse(playerMessage, OnChoiceDialogueSuccess, OnChoiceDialogueError);
    }

    private void OnChoiceDialogueSuccess(string response)
    {
        isDialogueRequestInProgress = false;
        OnDialogueSuccess(response);
        SetChoicesInteractable(true);
    }

    private void OnChoiceDialogueError(string errorMessage)
    {
        isDialogueRequestInProgress = false;
        OnDialogueError(errorMessage);
        SetChoicesInteractable(true);
    }

    private void EnterLossState()
    {
        if (isGameLost) return;

        isGameLost = true;
        isPlayerClose = false;
        isDialogueRequestInProgress = false;
        SetPromptVisible(false);
        SetChoicesInteractable(false);
        SetDialoguePanelVisible(false);
        ShowGameOverMessage();

        if (playerInputs != null)
        {
            playerInputs.cursorLocked = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    private void ShowGameOverMessage()
    {
        Canvas canvas = GetScreenCanvas();
        if (canvas == null)
        {
            Debug.LogError("[NPCInteractionPrompt] Mağlubiyet mesajı gösterilemedi: Screen Space Canvas bulunamadı.", this);
            return;
        }

        Transform existingOverlay = canvas.transform.Find("DefeatOverlay");
        if (existingOverlay != null)
        {
            existingOverlay.gameObject.SetActive(true);
            return;
        }

        GameObject overlay = new GameObject("DefeatOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetAsLastSibling();

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image background = overlay.GetComponent<Image>();
        background.color = new Color(0.25f, 0.015f, 0.015f, 0.94f);

        GameObject messageObject = new GameObject("DefeatMessage", typeof(RectTransform));
        messageObject.transform.SetParent(overlay.transform, false);
        RectTransform messageRect = messageObject.GetComponent<RectTransform>();
        messageRect.anchorMin = new Vector2(0.08f, 0.35f);
        messageRect.anchorMax = new Vector2(0.92f, 0.65f);
        messageRect.offsetMin = Vector2.zero;
        messageRect.offsetMax = Vector2.zero;

        TextMeshProUGUI messageText = messageObject.AddComponent<TextMeshProUGUI>();
        TextMeshProUGUI existingText = canvas.GetComponentInChildren<TextMeshProUGUI>(true);
        if (existingText != null)
        {
            messageText.font = existingText.font;
        }

        messageText.text = InsultMessage;
        messageText.fontSize = 42;
        messageText.fontSizeMin = 26;
        messageText.fontSizeMax = 42;
        messageText.enableAutoSizing = true;
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.color = Color.white;
        messageText.fontStyle = FontStyles.Bold;
        messageText.raycastTarget = false;
    }

    private void InitializeDialogueUI()
    {
        Canvas canvas = GetScreenCanvas();
        if (canvas == null)
        {
            Debug.LogError("[NPCInteractionPrompt] Screen Space Canvas bulunamadı; diyalog seçenekleri bağlanamadı.", this);
            return;
        }

        Transform panelTransform = canvas.transform.Find("DialoguePanel");
        if (panelTransform == null)
        {
            Debug.LogError("[NPCInteractionPrompt] DialogueCanvas altında DialoguePanel bulunamadı.", this);
            return;
        }

        dialoguePanel = panelTransform.gameObject;
        TextMeshProUGUI heading = panelTransform.Find("NPCResponseText")?.GetComponent<TextMeshProUGUI>();
        if (heading != null)
        {
            heading.text = "Mahkûmla ne yapmak istiyorsun?";
        }

        string[] labels = { "Yardım Et", "Umursama", "Hakaret Et" };
        UnityEngine.Events.UnityAction[] actions = { SelectHelp, SelectIgnore, SelectInsult };
        for (int i = 0; i < labels.Length; i++)
        {
            Transform buttonTransform = panelTransform.Find($"DialogueButton{i + 1}");
            if (buttonTransform == null)
            {
                Debug.LogError($"[NPCInteractionPrompt] DialogueButton{i + 1} bulunamadı.", this);
                continue;
            }

            Button button = buttonTransform.GetComponent<Button>();
            TextMeshProUGUI buttonText = buttonTransform.GetComponentInChildren<TextMeshProUGUI>(true);
            if (button == null || buttonText == null)
            {
                Debug.LogError($"[NPCInteractionPrompt] DialogueButton{i + 1} için Button veya TextMeshProUGUI eksik.", this);
                continue;
            }

            choiceButtons[i] = button;
            buttonText.text = labels[i];
            button.onClick.RemoveListener(actions[i]);
            button.onClick.AddListener(actions[i]);
        }

        Transform extraButton = panelTransform.Find("DialogueButton4");
        if (extraButton != null)
        {
            extraButton.gameObject.SetActive(false);
        }
    }

    private void SetDialoguePanelVisible(bool visible)
    {
        if (dialoguePanel != null && dialoguePanel.activeSelf != visible)
        {
            dialoguePanel.SetActive(visible);
        }
    }

    private void SetChoicesInteractable(bool interactable)
    {
        foreach (Button button in choiceButtons)
        {
            if (button != null)
            {
                button.interactable = interactable && !isGameLost;
            }
        }
    }

    private void UnlockCursorForDialogue()
    {
        if (playerInputs != null)
        {
            playerInputs.cursorLocked = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// Ollama yanıtı başarıyla geldiğinde konuşma balonunda gösterilir.
    /// </summary>
    private void OnDialogueSuccess(string response)
    {
        if (bubblePresenter != null)
        {
            bubblePresenter.ShowResponse(response);
        }
        else
        {
            Debug.Log($"[NPCInteractionPrompt] Mahkum1 Cevabı: {response}");
        }
    }

    /// <summary>
    /// Ollama isteği başarısız olduğunda hata mesajı konuşma balonunda gösterilir.
    /// </summary>
    private void OnDialogueError(string errorMessage)
    {
        if (bubblePresenter != null)
        {
            bubblePresenter.ShowError(errorMessage);
        }
        else
        {
            Debug.LogError($"[NPCInteractionPrompt] Ollama Hatası: {errorMessage}");
        }
    }

    /// <summary>
    /// Öncelikle targetNPC (Mahkum1) hiyerarşisi altındaki konuşma balonunu arar.
    /// Başlangıçta inaktif olan nesneleri de kapsar.
    /// </summary>
    private void ResolveBubblePresenter()
    {
        if (bubblePresenter != null) return;

        // 1. Öncelik: targetNPC (Mahkum1) altındaki HeadBubble bileşeni (inaktifler dahil)
        if (targetNPC != null)
        {
            bubblePresenter = targetNPC.GetComponentInChildren<WorldSpaceBubblePresenter>(true);
        }

        // 2. Bu GameObject'in (Mahkum1) kendisi veya alt nesnelerinde ara (inaktifler dahil)
        if (bubblePresenter == null)
        {
            bubblePresenter = GetComponentInChildren<WorldSpaceBubblePresenter>(true);
        }

        // 3. Bulunamazsa sahnede genel olarak ara (inaktifler dahil - Unity 2022.3 uyumlu)
        if (bubblePresenter == null)
        {
            bubblePresenter = FindFirstObjectByType<WorldSpaceBubblePresenter>(FindObjectsInactive.Include);
        }

        // 4. En son fallback: Oyuncu altında kalmış eski bir balon varsa ara
        if (bubblePresenter == null && playerTransform != null)
        {
            bubblePresenter = playerTransform.GetComponentInChildren<WorldSpaceBubblePresenter>(true);
        }
    }

    /// <summary>
    /// Mahkum1 veya sahnedeki OllamaService bileşenini çözer.
    /// </summary>
    private void ResolveOllamaService()
    {
        if (ollamaService != null) return;

        // 1. targetNPC veya bu GameObject üzerinde ara
        if (targetNPC != null)
        {
            ollamaService = targetNPC.GetComponent<OllamaService>();
        }

        if (ollamaService == null)
        {
            ollamaService = GetComponent<OllamaService>();
        }

        // 2. Sahne genelinde ara (inaktif nesneler dahil)
        if (ollamaService == null)
        {
            ollamaService = FindFirstObjectByType<OllamaService>(FindObjectsInactive.Include);
        }
    }

    /// <summary>
    /// Sahnedeki Canvas bileşenlerini tarayarak WorldSpace olmayan (ScreenSpaceOverlay veya ScreenSpaceCamera)
    /// 2D ekran Canvas'ını bulur. Asla WorldSpace modundaki BubbleCanvas'ı döndürmez.
    /// </summary>
    private Canvas GetScreenCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas c in canvases)
        {
            if (c != null && c.renderMode != RenderMode.WorldSpace)
            {
                return c;
            }
        }

        return null;
    }

    private void InitializeReferences()
    {
        // 1. Mahkum1 (Hedef NPC) Kontrolü
        if (targetNPC == null)
        {
            if (gameObject.name == "Mahkum1")
            {
                targetNPC = transform;
            }
            else
            {
                GameObject foundNPC = GameObject.Find("Mahkum1");
                if (foundNPC != null)
                {
                    targetNPC = foundNPC.transform;
                }
                else
                {
                    targetNPC = transform;
                    if (!hasLoggedTargetError)
                    {
                        Debug.LogError("[NPCInteractionPrompt] 'Mahkum1' nesnesi sahnede bulunamadı! Lütfen bu scripti 'Mahkum1' nesnesine ekleyin veya Inspector'dan 'Target NPC' alanına Mahkum1'i atayın.", this);
                        hasLoggedTargetError = true;
                    }
                }
            }
        }

        // 2. Oyuncu Kontrolü (Mahkum1 ile çakışmayı ve yanlış referansı kesin olarak engelle)
        if (playerTransform == null || playerTransform == targetNPC)
        {
            playerTransform = null;

            // 2.1. Öncelikle StarterAssets Controller bileşenlerini kontrol et (En güvenilir yöntem)
            StarterAssets.ThirdPersonController tp = FindFirstObjectByType<StarterAssets.ThirdPersonController>();
            if (tp != null && (targetNPC == null || tp.transform != targetNPC))
            {
                playerTransform = tp.transform;
            }
            else
            {
                StarterAssets.FirstPersonController fp = FindFirstObjectByType<StarterAssets.FirstPersonController>();
                if (fp != null && (targetNPC == null || fp.transform != targetNPC))
                {
                    playerTransform = fp.transform;
                }
            }

            // 2.2. Controller bulunamadıysa 'Player' Tag'ine sahip nesneleri ara
            // Ancak Mahkum1 / targetNPC olan nesneleri kesinlikle filtrele!
            if (playerTransform == null)
            {
                GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");
                foreach (GameObject p in playerObjects)
                {
                    if (p != null && (targetNPC == null || p.transform != targetNPC) && p != gameObject && !p.name.Contains("Mahkum"))
                    {
                        playerTransform = p.transform;
                        break;
                    }
                }
            }

            // 2.3. Tag ile de bulunamadıysa yaygın oyuncu isimlerini ara (Player, PlayerArmature, PlayerCapsule)
            if (playerTransform == null)
            {
                string[] fallbackNames = { "Player", "PlayerArmature", "PlayerCapsule" };
                foreach (string fallbackName in fallbackNames)
                {
                    GameObject fallbackPlayer = GameObject.Find(fallbackName);
                    if (fallbackPlayer != null && (targetNPC == null || fallbackPlayer.transform != targetNPC) && fallbackPlayer != gameObject)
                    {
                        playerTransform = fallbackPlayer.transform;
                        break;
                    }
                }
            }

            // Son güvenlik kontrolü: Oyuncu hiçbir durumda NPC'nin kendisi olamaz!
            if (playerTransform == targetNPC)
            {
                playerTransform = null;
            }

            if (playerTransform == null && !hasLoggedPlayerError)
            {
                Debug.LogError("[NPCInteractionPrompt] Oyuncu (Player) bulunamadı! Lütfen Inspector üzerinden 'Player Transform' alanına oyuncunuzu atayın veya oyuncu nesnesine 'Player' Tag'ini verin.", this);
                hasLoggedPlayerError = true;
            }
        }

        if (playerTransform != null && playerInputs == null)
        {
            playerInputs = playerTransform.GetComponent<StarterAssets.StarterAssetsInputs>();
        }

        // 3. UI Kontrolü (Yalnızca Screen Space Canvas kullanılır)
        if (promptTextUI == null)
        {
            // Yalnızca ekran Canvas'ı (Screen Space) altındaki InteractionPrompt nesnesini ara
            Canvas screenCanvas = GetScreenCanvas();
            if (screenCanvas != null)
            {
                Transform promptTr = screenCanvas.transform.Find("InteractionPrompt");
                if (promptTr != null)
                {
                    promptTextUI = promptTr.GetComponent<TextMeshProUGUI>();
                }
            }

            // Sahnede genel olarak ara; ancak bulunan nesnenin WorldSpace Canvas altında OLMADIĞINDAN emin ol!
            if (promptTextUI == null)
            {
                GameObject promptObj = GameObject.Find("InteractionPrompt");
                if (promptObj != null)
                {
                    Canvas parentCanvas = promptObj.GetComponentInParent<Canvas>();
                    if (parentCanvas != null && parentCanvas.renderMode != RenderMode.WorldSpace)
                    {
                        promptTextUI = promptObj.GetComponent<TextMeshProUGUI>();
                    }
                }
            }

            // Yoksa doğrudan ekran Canvas'ı altında oluştur
            if (promptTextUI == null)
            {
                promptTextUI = CreatePromptUI();
            }
        }

        if (promptTextUI != null)
        {
            promptTextUI.text = promptMessage;
        }

        // 4. AI & Konuşma Balonu Referansları
        ResolveOllamaService();
        ResolveBubblePresenter();
        GetCurrentInteractionTarget();
    }

    private Transform GetCurrentInteractionTarget()
    {
        if (targetNPC == null)
        {
            interactionTarget = null;
            return null;
        }

        if (interactionTarget == null || !interactionTarget.IsChildOf(targetNPC))
        {
            Animator animator = targetNPC.GetComponentInChildren<Animator>(true);
            interactionTarget = animator != null ? animator.transform : targetNPC;

            Debug.Log($"[NPCInteractionPrompt] Etkileşim hedefi: {interactionTarget.name}, parent NPC: {targetNPC.name}, position: {interactionTarget.position}, Animator: {animator != null}", this);
        }

        return interactionTarget;
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptTextUI != null && promptTextUI.gameObject != null)
        {
            if (promptTextUI.gameObject.activeSelf != visible)
            {
                promptTextUI.gameObject.SetActive(visible);
            }
        }
    }

    /// <summary>
    /// Yalnızca Screen Space (ekran) Canvas hiyerarşisi altında dinamik olarak
    /// 2D "E - Konuş" etkileşim prompt'u oluşturur. Asla WorldSpace altına eklemez.
    /// </summary>
    private TextMeshProUGUI CreatePromptUI()
    {
        Canvas canvas = GetScreenCanvas();

        if (canvas == null)
        {
            Debug.LogWarning("[NPCInteractionPrompt] Sahnede Screen Space Canvas bulunamadı! 'DialogueCanvas' (ScreenSpaceOverlay) oluşturuluyor.", this);
            GameObject canvasObject = new GameObject("DialogueCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        Transform existing = canvas.transform.Find("InteractionPrompt");
        if (existing != null)
        {
            return existing.GetComponent<TextMeshProUGUI>();
        }

        GameObject promptObject = new GameObject("InteractionPrompt");
        promptObject.transform.SetParent(canvas.transform, false);

        RectTransform rect = promptObject.AddComponent<RectTransform>();
        // Ekranın alt orta kısmına konumlandır
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

    private void OnDrawGizmosSelected()
    {
        // Unity Editor Scene ekranında etkileşim yarıçapını sarı küre ile göster
        Gizmos.color = Color.yellow;
        Transform currentTarget = interactionTarget != null ? interactionTarget : targetNPC;
        Vector3 origin = currentTarget != null ? currentTarget.position : transform.position;
        Gizmos.DrawWireSphere(origin, interactionDistance);
    }
}
