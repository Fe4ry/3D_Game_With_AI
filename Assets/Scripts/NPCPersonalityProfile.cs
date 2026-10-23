using UnityEngine;

public enum NPCPersonality
{
    Sevimli,
    Sinirli,
    Egoist,
    Umursamaz,
    Duygusal
}

[DisallowMultipleComponent]
public sealed class NPCPersonalityProfile : MonoBehaviour
{
    [SerializeField] private bool randomizeOnAwake = true;
    [SerializeField] private NPCPersonality personality;

    public NPCPersonality Personality => personality;

    private void Awake()
    {
        if (randomizeOnAwake)
        {
            personality = (NPCPersonality)Random.Range(0, 5);
        }
    }

    public string GetCharacterContext()
    {
        switch (personality)
        {
            case NPCPersonality.Sinirli:
                return "Kişiliğin Sinirli: çabuk parlayan, kısa ve sert konuşan birisin. Geçmişin: haksız yere işini kaybettin ve uzun süre öfkeni içine attın.";
            case NPCPersonality.Egoist:
                return "Kişiliğin Egoist: önce kendi çıkarını düşünür, kendinden emin ve benmerkezci konuşursun. Geçmişin: tek başına ayakta kalmak için her kararı kendi yararına göre vermeyi öğrendin.";
            case NPCPersonality.Umursamaz:
                return "Kişiliğin Umursamaz: sakin, kısa ve mesafeli konuşursun. Geçmişin: uzun süre sokakta yaşarken hayal kırıklıklarından korunmak için beklentilerini azalttın.";
            case NPCPersonality.Duygusal:
                return "Kişiliğin Duygusal: hassas, içten ve duygularını belli ederek konuşursun. Geçmişin: ailenden uzak kaldın ve zor zamanlarda yalnız hissettin.";
            default:
                return "Kişiliğin Sevimli: nazik, sıcak ve iyi niyetli konuşursun. Geçmişin: evini kaybettikten sonra sokakta yaşayan insanlarla dayanışarak ayakta kaldın.";
        }
    }
}
