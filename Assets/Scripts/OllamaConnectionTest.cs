
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class OllamaConnectionTest : MonoBehaviour
{
    private IEnumerator Start()
    {
        string url = "http://localhost:11434/api/generate";

        string json = "{\"model\":\"llama3.2\",\"prompt\":\"Say hello to Unity in one short sentence.\",\"stream\":false}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);

            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                OllamaResponse result =
                    JsonUtility.FromJson<OllamaResponse>(
                        request.downloadHandler.text
                    );

                Debug.Log("Llama 3.2: " + result.response);
            }
            else
            {
                Debug.LogError("Ollama connection failed: " + request.error);
            }
        }
    }
}
