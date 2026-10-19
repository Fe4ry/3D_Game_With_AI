#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SetupHeadBubbleTool
{
    private const string PrefKey = "LinkOllamaAndBubble_v4";

    static SetupHeadBubbleTool()
    {
        EditorApplication.update += RunOnce;
    }

    private static void RunOnce()
    {
        EditorApplication.update -= RunOnce;

        if (SessionState.GetBool(PrefKey, false)) return;
        SessionState.SetBool(PrefKey, true);

        ExecuteLinking();
    }

    [MenuItem("Tools/Link Ollama and Bubble on Mahkum1")]
    public static void ManualLink()
    {
        ExecuteLinking();
    }

    public static void ExecuteLinking()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.isLoaded || !activeScene.path.Contains("AdventureLevel"))
        {
            string scenePath = "Assets/Scenes/AdventureLevel.unity";
            if (System.IO.File.Exists(scenePath))
            {
                activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
        }

        // 1. Mahkum1 nesnesini bul
        GameObject mahkum1 = GameObject.Find("Mahkum1");
        if (mahkum1 == null)
        {
            Debug.LogError("[SetupHeadBubbleTool] Sahnede 'Mahkum1' nesnesi bulunamadı!");
            return;
        }

        // 2. Mahkum1 üzerinde OllamaService var mı kontrol et; yoksa ekle, varsa mevcut olanı kullan
        OllamaService service = mahkum1.GetComponent<OllamaService>();
        if (service == null)
        {
            // Sahnede başka bir nesnede var mı bak
            service = Object.FindFirstObjectByType<OllamaService>(FindObjectsInactive.Include);
            if (service == null)
            {
                service = mahkum1.AddComponent<OllamaService>();
                Debug.Log("[SetupHeadBubbleTool] 'Mahkum1' üzerine yeni OllamaService bileşeni eklendi.");
            }
        }

        // 3. Mahkum1 üzerindeki veya altındaki WorldSpaceBubblePresenter'ı bul
        WorldSpaceBubblePresenter bubble = mahkum1.GetComponentInChildren<WorldSpaceBubblePresenter>(true);
        if (bubble == null)
        {
            // Sahnede genel ara
            bubble = Object.FindFirstObjectByType<WorldSpaceBubblePresenter>(FindObjectsInactive.Include);
        }

        // 4. Mahkum1 üzerindeki NPCInteractionPrompt bileşenini al
        NPCInteractionPrompt prompt = mahkum1.GetComponent<NPCInteractionPrompt>();
        if (prompt == null)
        {
            prompt = mahkum1.AddComponent<NPCInteractionPrompt>();
        }

        // 5. Inspector alanlarını bağla
        SerializedObject so = new SerializedObject(prompt);
        SerializedProperty serviceProp = so.FindProperty("ollamaService");
        SerializedProperty bubbleProp = so.FindProperty("bubblePresenter");

        bool modified = false;

        if (serviceProp != null && service != null)
        {
            serviceProp.objectReferenceValue = service;
            modified = true;
        }

        if (bubbleProp != null && bubble != null)
        {
            bubbleProp.objectReferenceValue = bubble;
            modified = true;
        }

        if (modified)
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(prompt);
            EditorUtility.SetDirty(mahkum1);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log($"[SetupHeadBubbleTool] Mahkum1 üzerindeki bağlantılar tamamlandı! OllamaService: {(service != null ? service.name : "null")}, BubblePresenter: {(bubble != null ? bubble.name : "null")}");
        }
    }
}
#endif
