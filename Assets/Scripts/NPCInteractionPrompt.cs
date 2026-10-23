using UnityEngine;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Diyalog akışını yönetir; yakınlık/girdi, UI, Ollama servisi ve NPC davranışını
/// ilgili bileşenlere devreder.
/// </summary>
public class NPCInteractionPrompt : MonoBehaviour
{
    private const string PlayerFacingRequestError = "Şu an yanıt veremiyorum. Lütfen tekrar dene.";

    private enum DialogueStage
    {
        First,
        Second,
        Final,
        EgoistFinal,
        Completed
    }

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

    [Tooltip("Ekran diyalog panelini sunan bileşen. Boş bırakılırsa aynı nesnede çözülür veya oluşturulur.")]
    [SerializeField] private NPCDialogueView dialogueView;

    private NPCPersonalityProfile personalityProfile;

    [Tooltip("E tuşuna basıldığında karaktere iletilecek başlangıç konuşma promptu.")]
    [SerializeField] private string interactionPrompt = "Merhaba, neden buradasın, bir sorunun var mı?";

    private const string HelpMessage = "Sana yardım edebilirim! Güvenli bir alana götürmek istiyorum. Benimle gelir misin?";
    private const string IgnoreMessage = "Seni umursamıyorum.";
    private const string PersuadeMessage = "Benimle gelmeyi bir daha düşün. Sana yardım edebilirim.";
    private const string DamageEgoMessage = "Bu kadar bencil olman komik.";

    private bool hasLoggedPlayerError = false;
    private bool hasLoggedTargetError = false;
    private bool hasLoggedServiceWarning = false;
    private bool hasLoggedBubbleWarning = false;
    private Transform interactionTarget;
    private StarterAssets.StarterAssetsInputs playerInputs;
    private bool isDialogueRequestInProgress;
    private bool hasStartedConversation;
    private bool hasCompletedDialogue;
    private bool cursorStateCaptured;
    private bool cursorWasLocked;
    private readonly NPCInteractionController interactionController = new NPCInteractionController();
    private int conversationSequence;
    private DialogueStage dialogueStage = DialogueStage.First;
    private bool IsPlayerClose => interactionController.IsPlayerClose;

    private void Awake()
    {
        InitializeReferences();
        ResolveDialogueView();
        ResolvePersonalityProfile();
        dialogueView.Initialize();
        dialogueView.InitializeInteractionPrompt(promptMessage, promptTextUI);
        dialogueView.HideChoices();
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
        bool cancelPendingRequest = isDialogueRequestInProgress;
        interactionController.Reset();
        SetPromptVisible(false);
        SetDialoguePanelVisible(false);
        if (bubblePresenter != null)
        {
            bubblePresenter.Hide();
        }

        conversationSequence++;
        hasStartedConversation = false;
        isDialogueRequestInProgress = false;
        if (cancelPendingRequest && ollamaService != null)
        {
            ollamaService.CancelCurrentRequest();
        }

        RestoreCursorAfterDialogue();
    }

    private void Update()
    {
        if (targetNPC == null || playerTransform == null || playerTransform == targetNPC)
        {
            // Referanslar henüz atanmadıysa veya sahnede geç yüklendiyse tekrar kontrol et
            InitializeReferences();
            if (targetNPC == null || playerTransform == null || playerTransform == targetNPC) return;
        }

        Transform currentTarget = GetCurrentInteractionTarget();
        if (interactionController.UpdateProximity(playerTransform, currentTarget, interactionDistance, out float distance))
        {
            Debug.Log($"[NPCInteractionPrompt] Oyuncu: {playerTransform.position}, NPC hedefi ({currentTarget.name}): {currentTarget.position}, mesafe: {distance:F2}, prompt: {IsPlayerClose}", this);
            SetPromptVisible(IsPlayerClose && !hasStartedConversation && !hasCompletedDialogue);

            if (!IsPlayerClose)
            {
                if (hasStartedConversation)
                {
                    SetDialoguePanelVisible(false);
                    RestoreCursorAfterDialogue();
                    Debug.Log(
                        "[NPCInteractionPrompt] Player left interaction range; keeping the conversation and pending response active.",
                        this);
                }
                else
                {
                    SetDialoguePanelVisible(false);
                }
            }
            else if (hasStartedConversation && !isDialogueRequestInProgress)
            {
                SetDialoguePanelVisible(true);
                UnlockCursorForDialogue();
            }
        }

        if (IsPlayerClose && hasStartedConversation && interactionController.IsCancelKeyPressed())
        {
            EndConversation();
            return;
        }

        // Oyuncu menzildeyken E tuşuna basarsa konuşmayı başlat
        if (IsPlayerClose && interactionController.IsInteractKeyPressed())
        {
            TryStartDialogue();
        }
    }

    /// <summary>
    /// Oyuncu yakındayken ve servis meşgul değilken diyalog akışını tetikler.
    /// </summary>
    public void TryStartDialogue()
    {
        // Oyuncu uzaklaştıysa konuşma başlatılamaz
        if (!IsPlayerClose || hasCompletedDialogue || hasStartedConversation || isDialogueRequestInProgress) return;

        // Referanslar henüz çözülmediyse bulmayı dene
        if (ollamaService == null)
        {
            ResolveOllamaService();
        }

        if (bubblePresenter == null)
        {
            ResolveBubblePresenter();
        }

        ResolvePersonalityProfile();
        if (personalityProfile == null)
        {
            Debug.LogError("[NPCInteractionPrompt] NPC kişilik profili oluşturulamadı.", this);
            return;
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
        int currentConversation = ++conversationSequence;
        dialogueStage = DialogueStage.First;
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
        ollamaService.RequestResponse(
            BuildRequestPrompt(interactionPrompt, true),
            decision => OnInitialDialogueSuccess(currentConversation, decision),
            errorMessage => OnInitialDialogueError(currentConversation, errorMessage));
    }

    private void OnInitialDialogueSuccess(int currentConversation, NpcDecision decision)
    {
        if (!IsCurrentConversation(currentConversation)) return;

        isDialogueRequestInProgress = false;
        if (!IsValidDecision(decision))
        {
            OnInitialDialogueError(currentConversation, "NPC başlangıç diyaloğunda geçersiz bir karar döndürdü.");
            return;
        }

        Debug.Log("[NPCInteractionPrompt] Initial dialogue displayed; NPC action is deferred until the player chooses an option.", this);
        OnDialogueSuccess(decision.Dialogue);
        SetDialogueText("Bir eylem seç.");
        ShowFirstStageChoices();
        SetDialoguePanelVisible(IsPlayerClose);
        SetChoicesInteractable(true);
        if (IsPlayerClose)
        {
            UnlockCursorForDialogue();
        }
    }

    private void OnInitialDialogueError(int currentConversation, string errorMessage)
    {
        if (!IsCurrentConversation(currentConversation)) return;

        conversationSequence++;
        isDialogueRequestInProgress = false;
        hasStartedConversation = false;
        dialogueStage = DialogueStage.First;
        try
        {
            OnDialogueError(errorMessage);
        }
        finally
        {
            SetDialoguePanelVisible(false);
            SetPromptVisible(IsPlayerClose && !hasCompletedDialogue);
            RestoreCursorAfterDialogue();
            SetChoicesInteractable(false);
        }
    }

    private void SelectHelp()
    {
        if (dialogueStage != DialogueStage.First) return;
        SendDialogueChoice("Help", HelpMessage);
    }

    private void SelectIgnore()
    {
        if (dialogueStage != DialogueStage.First && dialogueStage != DialogueStage.Final) return;
        SendDialogueChoice("Ignore", IgnoreMessage);
    }

    private void SelectPersuade()
    {
        if (dialogueStage != DialogueStage.Second && dialogueStage != DialogueStage.Final) return;
        SendDialogueChoice("Persuade", PersuadeMessage);
    }

    private void SelectDamageEgo()
    {
        if (dialogueStage != DialogueStage.EgoistFinal ||
            personalityProfile == null ||
            personalityProfile.Personality != NPCPersonality.Egoist)
        {
            return;
        }

        SendDialogueChoice("DamageEgo", DamageEgoMessage);
    }

    private void SendDialogueChoice(string choiceName, string playerMessage)
    {
        if (isDialogueRequestInProgress || !hasStartedConversation || !IsPlayerClose)
        {
            Debug.Log($"[NPCInteractionPrompt] {choiceName} choice was rejected by the current interaction state.", this);
            return;
        }

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
        int currentConversation = conversationSequence;
        Debug.Log($"[NPCInteractionPrompt] {choiceName} choice accepted; requesting NPC response.", this);

        if (bubblePresenter != null)
        {
            bubblePresenter.ShowThinking();
        }

        ollamaService.RequestResponse(
            BuildRequestPrompt(playerMessage, false),
            decision => OnChoiceDialogueSuccess(currentConversation, decision),
            errorMessage => OnChoiceDialogueError(currentConversation, errorMessage));
    }

    private void OnChoiceDialogueSuccess(int currentConversation, NpcDecision decision)
    {
        if (!IsCurrentConversation(currentConversation)) return;

        Debug.Log(
            $"[NPCInteractionPrompt] Choice response received; action: {(decision != null ? decision.Action.ToString() : "invalid")}; " +
            $"persuaded: {(decision != null && decision.Persuaded)}.",
            this);
        if (!IsValidDecision(decision))
        {
            OnChoiceDialogueError(currentConversation, "NPC ikna yanıtı boş veya tutarsız.");
            return;
        }

        bool wantsToFollow = decision.Persuaded && decision.Action == NpcAction.Follow;
        if (wantsToFollow && !StartFollowing())
        {
            isDialogueRequestInProgress = false;
            SetChoicesInteractable(true);
            return;
        }

        OnDialogueSuccess(decision.Dialogue);
        isDialogueRequestInProgress = false;
        if (wantsToFollow)
        {
            CompleteDialogue();
            return;
        }

        if (dialogueStage == DialogueStage.First)
        {
            dialogueStage = DialogueStage.Second;
            ShowSecondStageChoice();
        }
        else if (dialogueStage == DialogueStage.Second &&
                 personalityProfile != null &&
                 personalityProfile.Personality == NPCPersonality.Egoist)
        {
            dialogueStage = DialogueStage.EgoistFinal;
            ShowEgoistFinalChoice();
        }
        else if (dialogueStage == DialogueStage.Second)
        {
            dialogueStage = DialogueStage.Final;
            ShowFinalStageChoices();
        }
        else
        {
            CompleteDialogue();
            return;
        }

        SetChoicesInteractable(true);
    }

    private void OnChoiceDialogueError(int currentConversation, string errorMessage)
    {
        if (!IsCurrentConversation(currentConversation)) return;

        isDialogueRequestInProgress = false;
        try
        {
            OnDialogueError(errorMessage);
        }
        finally
        {
            SetChoicesInteractable(true);
        }
    }

    private void CompleteDialogue()
    {
        hasCompletedDialogue = true;
        SetChoicesInteractable(false);
        conversationSequence++;
        hasStartedConversation = false;
        isDialogueRequestInProgress = false;
        dialogueStage = DialogueStage.Completed;
        SetDialoguePanelVisible(false);
        SetPromptVisible(false);
        RestoreCursorAfterDialogue();
    }

    private bool IsCurrentConversation(int currentConversation)
    {
        return hasStartedConversation && currentConversation == conversationSequence;
    }

    private void EndConversation()
    {
        bool cancelPendingRequest = isDialogueRequestInProgress;
        conversationSequence++;
        hasStartedConversation = false;
        isDialogueRequestInProgress = false;
        if (cancelPendingRequest && ollamaService != null)
        {
            ollamaService.CancelCurrentRequest();
        }

        dialogueStage = DialogueStage.First;
        ShowFirstStageChoices();
        SetDialoguePanelVisible(false);
        if (bubblePresenter != null)
        {
            bubblePresenter.Hide();
        }

        SetDialogueText("Mahkûmla ne yapmak istiyorsun?");
        SetPromptVisible(IsPlayerClose && !hasCompletedDialogue);
        RestoreCursorAfterDialogue();
        SetChoicesInteractable(false);
    }

    private void ResolveDialogueView()
    {
        if (dialogueView == null)
        {
            dialogueView = GetComponent<NPCDialogueView>();
        }

        if (dialogueView == null)
        {
            dialogueView = gameObject.AddComponent<NPCDialogueView>();
        }
    }

    private void ResolvePersonalityProfile()
    {
        if (personalityProfile != null || targetNPC == null)
        {
            return;
        }

        personalityProfile = targetNPC.GetComponent<NPCPersonalityProfile>();
        if (personalityProfile == null)
        {
            personalityProfile = targetNPC.gameObject.AddComponent<NPCPersonalityProfile>();
        }
    }

    private void SetDialoguePanelVisible(bool visible)
    {
        if (dialogueView != null)
        {
            dialogueView.SetPanelVisible(visible);
        }
    }

    private void SetChoicesInteractable(bool interactable)
    {
        if (dialogueView != null)
        {
            dialogueView.SetChoicesInteractable(interactable);
        }
    }

    private void UnlockCursorForDialogue()
    {
        if (!cursorStateCaptured)
        {
            cursorWasLocked = playerInputs != null
                ? playerInputs.cursorLocked
                : Cursor.lockState == CursorLockMode.Locked;
            cursorStateCaptured = true;
        }

        if (playerInputs != null)
        {
            playerInputs.cursorLocked = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestoreCursorAfterDialogue()
    {
        if (!cursorStateCaptured) return;

        if (playerInputs != null)
        {
            playerInputs.cursorLocked = cursorWasLocked;
        }

        Cursor.lockState = cursorWasLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !cursorWasLocked;
        cursorStateCaptured = false;
    }

    private void ShowFirstStageChoices()
    {
        if (dialogueView != null)
        {
            dialogueView.ShowFirstStageChoices(SelectHelp, SelectIgnore);
        }
    }

    private void ShowSecondStageChoice()
    {
        if (dialogueView != null)
        {
            dialogueView.ShowSecondStageChoice(SelectPersuade);
        }
    }

    private void ShowFinalStageChoices()
    {
        if (dialogueView != null)
        {
            dialogueView.ShowFinalStageChoices(SelectPersuade, SelectIgnore);
        }
    }

    private void ShowEgoistFinalChoice()
    {
        if (dialogueView != null)
        {
            dialogueView.ShowEgoistFinalChoice(SelectDamageEgo);
        }
    }

    private string BuildRequestPrompt(string playerMessage, bool isIntroduction)
    {
        ResolvePersonalityProfile();
        string characterContext = isIntroduction
            ? $"SABİT NPC KİŞİLİĞİ VE GEÇMİŞİ: {personalityProfile.GetCharacterContext()}\n"
            : "Önceki konuşma geçmişinde verilen NPC kişiliğini ve geçmişini koru.\n";
        string stageInstructions = dialogueStage switch
        {
            DialogueStage.First => "İlk aşamadasın; oyuncu yardım etme veya umursamama eylemini seçti. Kişiliğini ve konuşma bağlamını değerlendirip ikna sonucuna kendin karar ver.",
            DialogueStage.Second => "İkinci aşamadasın; oyuncu seni ikna etmeye çalıştı. Kişiliğini ve konuşma bağlamını değerlendirip ikna sonucuna kendin karar ver.",
            DialogueStage.Final => "Son aşamadasın; oyuncu ikna etme veya umursamama eylemini seçti. Kişiliğini ve konuşma bağlamını değerlendirip ikna sonucuna kendin karar ver.",
            DialogueStage.EgoistFinal => "Son aşamadasın; Egoist kişiliğindeki NPC için oyuncu egonu zedeleme eylemini seçti. Bu eylemin etkisini kişiliğine ve konuşma bağlamına göre değerlendirip ikna sonucuna kendin karar ver.",
            _ => "Bu konuşma tamamlandı; yeni karar verme."
        };
        string purpose = isIntroduction
            ? "Kendini kişiliğine ve geçmişine uygun, kısa ve doğal biçimde tanıt. Henüz oyuncu seçeneği seçilmediği için FOLLOW kararı verme."
            : "En son oyuncu mesajına karşılık kısa, doğal bir Türkçe diyalog üret ve istenen karar alanlarını doğru doldur.";

        return $"{characterContext}DİYALOG AŞAMASI: {stageInstructions}\n" +
               $"{purpose}\nOYUNCU MESAJI: {playerMessage}";
    }

    private void SetDialogueText(string text)
    {
        if (dialogueView != null)
        {
            dialogueView.SetDialogueText(text);
        }
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
            Debug.Log($"[NPCInteractionPrompt] Model response received ({response?.Length ?? 0} characters); no bubble presenter is assigned.", this);
        }
    }

    private bool StartFollowing()
    {
        NPCFollowPlayer follower = targetNPC != null ? targetNPC.GetComponent<NPCFollowPlayer>() : null;
        if (follower == null || !follower.SetFollowing(true))
        {
            const string errorMessage = "NPC şu anda oyuncuyu takip edemiyor.";
            Debug.LogWarning($"[NPCInteractionPrompt] {errorMessage}", this);
            OnDialogueError(errorMessage);
            return false;
        }

        Debug.Log(
            $"[NPCInteractionPrompt] NPC '{targetNPC.name}' now follows player '{playerTransform?.name ?? "unassigned"}'.",
            this);
        return true;
    }

    private static bool IsValidDecision(NpcDecision decision)
    {
        return decision != null &&
               !string.IsNullOrWhiteSpace(decision.Dialogue) &&
               (decision.Action == NpcAction.Follow || decision.Action == NpcAction.Stay) &&
               decision.Persuaded == (decision.Action == NpcAction.Follow);
    }

    /// <summary>
    /// Ollama isteği başarısız olduğunda hata mesajı konuşma balonunda gösterilir.
    /// </summary>
    private void OnDialogueError(string errorMessage)
    {
        Debug.LogWarning($"[NPCInteractionPrompt] Dialogue request failed: {errorMessage}", this);
        if (bubblePresenter != null)
        {
            bubblePresenter.ShowError(PlayerFacingRequestError);
        }
        else
        {
            Debug.LogError($"[NPCInteractionPrompt] Ollama Hatası: {errorMessage}", this);
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

        // AI & konuşma balonu referansları
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
        if (dialogueView != null)
        {
            dialogueView.SetInteractionPromptVisible(visible);
        }
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
