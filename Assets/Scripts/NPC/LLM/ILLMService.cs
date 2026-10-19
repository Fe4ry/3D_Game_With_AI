using System;
using System.Collections;

/// LLM servisleri için sözleşme (Interface).
/// Unity coroutine mimarisiyle asenkron çağrıları destekler;
/// başarılı yanıt ve hata durumlarını ayrıştırarak geri bildirir.

public interface ILLMService
{

    /// LLM'e prompt gönderir ve yanıtı coroutine üzerinden asenkron olarak bekler.

    IEnumerator GenerateResponse(string prompt, Action<string> onSuccess, Action<string> onError);


    /// Doğrudan bir coroutine başlatarak isteği yürütür.

   
    void RequestResponse(string prompt, Action<string> onSuccess, Action<string> onError);

    /// Servisin şu anda aktif bir istek yürütüp yürütmediği bilgisi.

    bool IsBusy { get; }
}

