using System;
using UnityEngine;

internal static class OllamaResponseParser
{
    [Serializable]
    private sealed class DecisionPayload
    {
        public string action = string.Empty;
        public string dialogue = string.Empty;
        public bool persuaded = false;
    }

    public static bool TryParseNpcDecision(
        string rawModelResponse,
        out NpcDecision decision,
        out string errorMessage)
    {
        decision = null;
        errorMessage = null;

        string jsonResponse = UnwrapJsonCodeFence(rawModelResponse);
        if (jsonResponse == null)
        {
            errorMessage = "Ollama karar yanıtı boş veya desteklenmeyen bir Markdown kod bloğu biçiminde.";
            return false;
        }

        try
        {
            if (!TryReadRootBooleanProperty(jsonResponse, "persuaded", out bool persuaded))
            {
                errorMessage = "Ollama karar JSON'unda geçerli bir boolean 'persuaded' alanı bulunamadı.";
                return false;
            }

            DecisionPayload payload = JsonUtility.FromJson<DecisionPayload>(jsonResponse);
            if (payload == null || string.IsNullOrWhiteSpace(payload.action))
            {
                errorMessage = "Ollama karar JSON'unda geçerli bir 'action' alanı bulunamadı.";
                return false;
            }

            string actionText = payload.action.Trim();
            NpcAction action;
            if (string.Equals(actionText, "FOLLOW", StringComparison.OrdinalIgnoreCase))
            {
                action = NpcAction.Follow;
            }
            else if (string.Equals(actionText, "STAY", StringComparison.OrdinalIgnoreCase))
            {
                action = NpcAction.Stay;
            }
            else
            {
                errorMessage = $"Ollama bilinmeyen bir aksiyon döndürdü: '{payload.action}'. İzin verilen aksiyonlar FOLLOW ve STAY.";
                return false;
            }

            if (persuaded != (action == NpcAction.Follow))
            {
                errorMessage = "Ollama'nın 'action' ve 'persuaded' alanları birbiriyle çelişiyor.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(payload.dialogue))
            {
                errorMessage = "Ollama karar JSON'unda geçerli bir 'dialogue' alanı bulunamadı.";
                return false;
            }

            decision = new NpcDecision(action, payload.dialogue, persuaded);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Ollama karar JSON'u ayrıştırılamadı: {ex.Message}";
            return false;
        }
    }

    private static bool TryReadRootBooleanProperty(string json, string propertyName, out bool value)
    {
        value = false;
        int index = 0;
        SkipJsonWhitespace(json, ref index);
        if (index >= json.Length || json[index++] != '{')
        {
            return false;
        }

        while (index < json.Length)
        {
            SkipJsonWhitespace(json, ref index);
            if (index >= json.Length || json[index] == '}')
            {
                return false;
            }

            if (json[index++] != '"')
            {
                return false;
            }

            int keyStart = index;
            bool escaped = false;
            while (index < json.Length)
            {
                char character = json[index];
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    break;
                }

                index++;
            }

            if (index >= json.Length)
            {
                return false;
            }

            bool isTargetProperty =
                !escaped &&
                index - keyStart == propertyName.Length &&
                string.Compare(json, keyStart, propertyName, 0, propertyName.Length, StringComparison.Ordinal) == 0;
            index++;
            SkipJsonWhitespace(json, ref index);
            if (index >= json.Length || json[index++] != ':')
            {
                return false;
            }

            SkipJsonWhitespace(json, ref index);
            if (isTargetProperty)
            {
                if (index + 4 <= json.Length &&
                    string.Compare(json, index, "true", 0, 4, StringComparison.Ordinal) == 0)
                {
                    int end = index + 4;
                    if (IsJsonValueDelimiter(json, end))
                    {
                        value = true;
                        return true;
                    }
                }
                else if (index + 5 <= json.Length &&
                    string.Compare(json, index, "false", 0, 5, StringComparison.Ordinal) == 0)
                {
                    int end = index + 5;
                    if (IsJsonValueDelimiter(json, end))
                    {
                        value = false;
                        return true;
                    }
                }

                return false;
            }

            int nestedDepth = 0;
            bool insideString = false;
            escaped = false;
            while (index < json.Length)
            {
                char character = json[index];
                if (insideString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (character == '\\')
                    {
                        escaped = true;
                    }
                    else if (character == '"')
                    {
                        insideString = false;
                    }
                }
                else if (character == '"')
                {
                    insideString = true;
                }
                else if (character == '{' || character == '[')
                {
                    nestedDepth++;
                }
                else if (character == '}' || character == ']')
                {
                    if (nestedDepth == 0)
                    {
                        return false;
                    }

                    nestedDepth--;
                }
                else if (character == ',' && nestedDepth == 0)
                {
                    index++;
                    break;
                }

                index++;
            }
        }

        return false;
    }

    private static bool IsJsonValueDelimiter(string json, int index)
    {
        SkipJsonWhitespace(json, ref index);
        return index < json.Length && (json[index] == ',' || json[index] == '}');
    }

    private static void SkipJsonWhitespace(string json, ref int index)
    {
        while (index < json.Length && char.IsWhiteSpace(json[index]))
        {
            index++;
        }
    }

    private static string UnwrapJsonCodeFence(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }

        string trimmedResponse = response.Trim();
        if (!trimmedResponse.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmedResponse;
        }

        int openingFenceEnd = trimmedResponse.IndexOf('\n');
        int closingFenceStart = trimmedResponse.LastIndexOf("```", StringComparison.Ordinal);
        if (openingFenceEnd < 0 ||
            closingFenceStart <= openingFenceEnd ||
            closingFenceStart != trimmedResponse.Length - 3 ||
            (trimmedResponse[closingFenceStart - 1] != '\n' && trimmedResponse[closingFenceStart - 1] != '\r'))
        {
            return null;
        }

        string language = trimmedResponse.Substring(3, openingFenceEnd - 3).Trim();
        if (language.Length > 0 && !string.Equals(language, "json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string jsonResponse = trimmedResponse.Substring(openingFenceEnd + 1, closingFenceStart - openingFenceEnd - 1).Trim();
        return string.IsNullOrWhiteSpace(jsonResponse) ? null : jsonResponse;
    }
}
