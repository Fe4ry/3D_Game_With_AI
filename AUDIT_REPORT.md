# Unity AI Mahkum Oyunu — İster Denetim Raporu

**Denetim tarihi:** 2026-10-22  
**Denetim kapsamı:** `Assets/Scripts`, `AdventureLevel` sahnesi, ilgili prefablar, NavMesh verisi, paket ve proje ayarları  
**Sonuç türleri:** PASS / FAIL / PARTIAL / NOT VERIFIED / NOT APPLICABLE

> Denetim, çalışma ağacındaki güncel (commit edilmemiş script/sahne değişiklikleri dahil) içeriği kapsar. Bu denetimde oyun kodu, sahne, prefab veya proje ayarı değiştirilmedi; yalnızca bu rapor güncellendi.

## 1. Proje Özeti

- Proje, `AdventureLevel` adlı etkin bir sahneyi build ayarlarına eklemiş. Oyuncu Starter Assets üçüncü şahıs kontrolcüsünü, NPC ise `OllamaService`, `NPCInteractionPrompt`, `NavMeshAgent` ve `NPCFollowPlayer` bileşenlerini kullanıyor.
- Ollama iletişimi `UnityWebRequest` ile asenkron yapılıyor. Ana servis `gemma3:4b` modelini `http://localhost:11434/api/generate` adresinde kullanıyor. HTTP yanıt zarfından alınan `response` alanı ayrıca `action` ve `dialogue` bekleyen bir JSON kararına ayrıştırılıyor.
- NPC ile etkileşim, `E` tuşu ve üç seçenekli UI üzerinden ilerliyor. Başlangıç yanıtı yalnızca gösteriliyor; oyuncu Yardım Et veya Umursama seçtiğinde ilgili karar LLM'den isteniyor ve takip başlatılıyor/durduruluyor. Hakaret seçeneği LLM kararından bağımsız olarak oyun kaybetme durumuna geçiyor.
- `NPCFollowPlayer` yalnızca `SetFollowing(true)` çağrısından sonra oyuncuya periyodik `SetDestination()` çağrısı yapıyor. `STAY` kararı takibi kapatıp mevcut NavMesh yolunu sıfırlıyor.
- `ILLMService` arayüzü var; ancak projeye özgü NPC temel sınıfı veya ondan türeyen NPC sınıfları tespit edilmedi.
- Proje ayarlarında Unity sürümü **2022.3.62f3**. İster belgesinde belirtilen **2022.3.35f1** ile aynı değil.

## 2. İster Kontrol Tablosu

| İster | Durum | Kanıt ve değerlendirme |
|---|---|---|
| 1 — Yapılandırılmış JSON kararları ve aksiyon tetikleme | **PARTIAL** | `format: "json"` isteği ve normal JSON ile `json`/etiketsiz Markdown kod bloğunu ayrıştıran parser mevcut. Ollama `gemma3:4b` ile testte bu modda ham JSON döndürdü; ayrıca `FOLLOW` ve `STAY` kararlarının `ApplyDecision()` üzerinden `NPCFollowPlayer.SetFollowing()` yoluna bağlandığı statik olarak doğrulandı. Unity Play Mode callback'i ve gerçek hareket hâlâ doğrulanmadı. `trust_level` alanı yok; projede güven sistemi de bulunmadı. |
| 2 — NavMesh ile otonom hareket | **PARTIAL** | Sahneye NavMeshSurface ve bağlı NavMeshData eklenmiş; NPC'de etkin NavMeshAgent ve takip scripti var. Tree_01 ve Rock_05 varlıklarında collider bulunuyor. `SetDestination` reddi loglanıyor; fakat kabul edilmiş yolun `PathComplete`/`PathPartial`/`PathInvalid` durumu ele alınmıyor. Baked alan ve engel çevresinde gerçek hareket Play Mode'da doğrulanmadı. |
| 3 — Kalıtım ve çok biçimlilik | **FAIL** | `OllamaService : MonoBehaviour, ILLMService` arayüz uygulamasıdır; istenen proje içi temel sınıf/alt sınıf kalıtımını göstermez. Birden fazla özel NPC sınıfı veya `virtual`/`override` tabanlı NPC davranışı bulunamadı. |
| 4 — Sahne durumunun LLM'e aktarılması | **FAIL** | İstek prompt'u önceki oyuncu/NPC diyaloglarını ve yeni oyuncu mesajını içeriyor; karar eşlemesi ve karakter yönergesi statik. Güncel konum veya başka dinamik oyun durumu prompt'a eklenmiyor. Sağlık/envanter/güven sisteminin mevcut olduğuna dair kanıt yok. |
| Kahraman — Düz zeminde hareket ve NPC'ye yaklaşma | **PARTIAL** | Sahne oyuncusu Player etiketli; PlayerArmature prefabında Starter Assets üçüncü şahıs kontrolcüsü, CharacterController ve Animator var. Giriş ayarı Input System için etkin. WASD hareketi ve etkileşim mesafesi gerçek çalışma sırasında test edilmedi. |
| Mahkum1 — LLM, takip ve fiziksel tutarlılık | **PARTIAL** | JSON kararı Yardım Et/Umursama seçimlerinden sonra takip bileşenine bağlanmış; format kısıtlamalı API testinde action değerleri beklenen çıktı. Gerçek oyun callback'i ve hareket test edilmedi. NPC modelinin child nesnesindeki SphereCollider trigger; NPC kökünde fiziksel bloklama kanıtlanmadı. |

## 3. Kod Kanıtları

### İster 1 — Karar formatı ve aksiyon

- [`Assets/Scripts/OllamaResponse.cs`](Assets/Scripts/OllamaResponse.cs): Ollama HTTP DTO'suna ek olarak `NpcAction` (`Follow`, `Stay`) ve `NpcDecision` (aksiyon + diyalog) tanımlı; `trust_level` yok.
- [`Assets/Scripts/NPC/LLM/OllamaService.cs`](Assets/Scripts/NPC/LLM/OllamaService.cs): `BuildSystemPrompt` tek JSON nesnesi ister. `HandleResponse`, HTTP JSON'undan `response` alanını alıp `TryParseNpcDecision` ile kararı doğrular. Parser tam yanıtı saran ` ```json ... ``` ` ve ` ``` ... ``` ` bloklarını açar; boş/hatalı blok, desteklenmeyen dil etiketi, bilinmeyen action veya boş alan mevcut `DECISION_PARSE_ERROR`/`onError` akışında kalır.
- [`Assets/Scripts/NPC/LLM/OllamaRequest.cs`](Assets/Scripts/NPC/LLM/OllamaRequest.cs): `/api/generate` isteğinde `format` alanı `json` olarak başlatılır ve parametreli constructor'da da açıkça atanır; model ayarı/değerleri değişmemiştir.
- [`Assets/Scripts/NPCInteractionPrompt.cs`](Assets/Scripts/NPCInteractionPrompt.cs): Başlangıç callback'i yalnızca cevabı gösterip aksiyonu seçime erteliyor. Yardım/umursama callback'i `ApplyDecision` üzerinden `SetFollowing` çağırıyor. Hakaret doğrudan `EnterLossState()` çağırıyor.
- [`Assets/Scripts/NPC/NPCFollowPlayer.cs`](Assets/Scripts/NPC/NPCFollowPlayer.cs): Takip varsayılan olarak kapalı. `SetFollowing` takip durumunu değiştiriyor; takip açıkken hedefi güncelliyor, `SetDestination` reddini bir kez uyarıyor. Kabul edilmiş yolun durumunu/partial path'i sonradan ele almıyor.

**Değerlendirme:** Karar parse ve aksiyon uygulama hattı kodda mevcut. Yerel modelin kod bloğu içerebilecek çıktıları için parser toleransı ve Ollama format kısıtlaması eklendi; model API'siyle `FOLLOW`/`STAY` smoke testi geçti. Unity HTTP/UI callback zinciri ayrıca doğrulanmalıdır. Ollama HTTP zarfı ile modelin ürettiği karar JSON'u ayrı katmanlardır.

### İster 2 — NavMesh

- [`Assets/Scenes/AdventureLevel.unity`](Assets/Scenes/AdventureLevel.unity): `Mahkum1` nesnesinde etkin `NavMeshAgent` ve `NPCFollowPlayer` bileşenleri var. Takip hedefi sahnedeki PlayerArmature Transform'una bağlanmış. Agent hızı `3.5`, durma mesafesi `2`.
- Aynı sahnede NavMeshSurface etkin ve bir NavMeshData varlığına referans veriyor. Surface bütün sahne nesnelerini topluyor ve tüm katmanları kullanıyor. `m_UseGeometry: 0` collider geometrisine karşılık gelir; engel yolunun runtime sonucu ayrıca doğrulanmalıdır.
- [`Assets/Packages/SimpleNaturePack/Prefabs/Tree_01.prefab`](Assets/Packages/SimpleNaturePack/Prefabs/Tree_01.prefab) ağacında etkin, trigger olmayan BoxCollider var. [`Assets/Packages/SimpleNaturePack/Prefabs/Rock_05.prefab`](Assets/Packages/SimpleNaturePack/Prefabs/Rock_05.prefab) kayasında etkin, trigger olmayan MeshCollider var.
- [`Assets/Scripts/NPC/NPCFollowPlayer.cs`](Assets/Scripts/NPC/NPCFollowPlayer.cs) oyuncu/agent/`isOnNavMesh` önkoşullarını kontrol ediyor ve `SetDestination` reddini logluyor. Kabul edilen hedef için `pathStatus`, `pathPending`, partial/invalid yol ve ulaşılamayan hedef davranışı tanımlı değil.

**Sınırlama:** NavMeshData dosyası ikili formatta. Sahnede varlığın bağlı olması ve Surface ayarları, güncel baked verinin doğru engelleri içerdiğini veya gerçek agent yolunu tek başına doğrulamaz.

### İster 3 — Nesne yönelimli tasarım

- [`Assets/Scripts/NPC/LLM/ILLMService.cs`](Assets/Scripts/NPC/LLM/ILLMService.cs) bir servis arayüzü tanımlıyor.
- [`Assets/Scripts/NPC/LLM/OllamaService.cs`](Assets/Scripts/NPC/LLM/OllamaService.cs) bu arayüzü uyguluyor.
- [`Assets/Scripts/NPC/NPCFollowPlayer.cs`](Assets/Scripts/NPC/NPCFollowPlayer.cs) doğrudan `MonoBehaviour`'dan türetiliyor. Özel bir NPC temel sınıfı, türemiş NPC sınıfları veya NPC davranışında polimorfik override bulunmadı.

### İster 4 — Model bağlamı

- [`Assets/Scripts/NPC/LLM/OllamaService.cs`](Assets/Scripts/NPC/LLM/OllamaService.cs): Prompt geçmişi oyuncu mesajları ve NPC yanıtlarından oluşuyor; action eşlemesi ile karakter yönergesi sabit.
- [`Assets/Scripts/NPCInteractionPrompt.cs`](Assets/Scripts/NPCInteractionPrompt.cs): Başlangıç arka planı ve Yardım/Umursama mesajları sabit. Oyuncunun konumu veya oyun durumunu güncel olarak sağlayan bir bağlam sağlayıcı görülmüyor.
- İncelenen kodda sağlık, envanter ve güven/ilişki sistemi bulunamadı. Bunlar mevcut özellikmiş gibi değerlendirilmedi.

### Kahraman ve NPC

- [`Assets/Starter Assets/Runtime/ThirdPersonController/Prefabs/PlayerArmature.prefab`](Assets/Starter%20Assets/Runtime/ThirdPersonController/Prefabs/PlayerArmature.prefab): Animator, CharacterController ve Starter Assets üçüncü şahıs kontrol scripti bulunuyor.
- [`Assets/Scenes/AdventureLevel.unity`](Assets/Scenes/AdventureLevel.unity): `Kahraman` nesnesi Player etiketli; NPC etkileşim mesafesi sahnede `2` olarak ayarlanmış. PlayerArmature hedefi hem etkileşim hem takip için referans edilmiş.
- NPC modeline eklenmiş SphereCollider trigger olarak ayarlı. Bu bileşen fiziksel bloklama sağlamaz; NPC'nin solid collision gereksinimi ayrıca test edilmeli.
- [`Assets/Scripts/UI/WorldSpaceBubblePresenter.cs`](Assets/Scripts/UI/WorldSpaceBubblePresenter.cs) düşünme, yanıt ve hata metinlerini balonda gösteren metotlar içeriyor.

## 4. Sahne ve Inspector Kontrolleri

Dosyalardan doğrulanabilenler:

- `AdventureLevel` build sahnesi olarak etkin.
- Sahne `Mahkum1`, `Kahraman`, NavMeshSurface, NavMeshData, Screen Space diyalog UI'sı ve World Space konuşma balonu içeriyor.
- NPC Ollama URL'i `http://localhost:11434/api/generate`, modeli `gemma3:4b`, zaman aşımı 30 saniye.
- Sahnedeki etkileşim mesafesi 2 birim.
- NavMeshSurface `NavMeshData` referansı içeriyor; ayrıca proje paketlerinde `com.unity.ai.navigation` 1.1.7 kullanılıyor.
- `ProjectSettings.asset` etkin giriş işleyicisini `2` olarak ayarlamış (Both).
- `SetupTester` nesnesi etkin ve üzerindeki `OllamaConnectionTest`, sahne başlarken `llama3.2` modeline ayrı bir başlangıç isteği gönderecek şekilde ayarlı. Bu, NPC servisindeki `gemma3:4b` isteğinden bağımsızdır.
- Proje sürümü `ProjectVersion.txt` içinde 2022.3.62f3 olarak belirtilmiş; ister belgesindeki 2022.3.35f1 ile farklı.

Unity Editor/Inspector'da ayrıca doğrulanması gerekenler:

- NavMesh'in görsel kapsamı, yürünebilir zemin, ağaç/kaya çevresindeki gerçek boşluklar ve agent başlangıç konumunun NavMesh üzerinde olması.
- Ağaç ve kaya örneklerinin baked NavMesh'e dahil edilip edilmediği; gerçek yolun engel etrafından geçip geçmediği.
- PlayerArmature ve NPC modelinin hareket/animasyon eşleşmesi; oyuncu, NPC ve kapı çarpışma/trigger davranışları.
- Bu kontrol anında yerel Ollama ve `gemma3:4b` modeli erişilebilirdi; hedef/sunum makinesindeki erişim ayrıca doğrulanmalı. UI callback'lerinin Play Mode'da doğru görünmesi.

## 5. Çalışma Zamanı Testleri

| Kontrol | Sonuç | Gözlem |
|---|---|---|
| İlgili C# dosyalarında VS Code Problems | **PASS (editör tanısı)** | `OllamaService.cs`, `OllamaRequest.cs`, `NPCFollowPlayer.cs` ve `NPCInteractionPrompt.cs` için hata raporlanmadı. Bu sonuç tek başına Unity derlemesi değildir. |
| `dotnet build Assembly-CSharp.csproj --no-restore` | **NOT VERIFIED (araç/ortam engeli)** | Derleme başlamadan MSB3644 verdi: .NET Framework v4.7.1 reference assemblies/developer pack kurulu değil. Bu, kaynak kodun derlendiğini veya derlenmediğini kanıtlamaz. |
| Unity batchmode açılışı | **NOT VERIFIED** | Deneme, projenin başka bir Unity örneğinde zaten açık olduğu için `project already open` ile durdu. Açık editör kapatılmadı; Play Mode başlatılmadı. |
| `GET http://localhost:11434/api/tags` | **PASS (yerel servis)** | HTTP 200. Yanıtta `gemma3:4b` ve `llama3.2:latest` modelleri listelendi. |
| Parser: normal JSON | **PASS (izole C# test)** | `TryParseNpcDecision` metodu doğrudan güncel kaynak dosyasından alınıp .NET 10 harness'te test edildi; `FOLLOW` ve diyalog doğru üretildi. JSON dönüştürücü Unity `JsonUtility` değil, uyumlu alan tabanlı test stub'ı olduğundan Unity parser eşdeğerliği kanıtlanmış sayılmaz. |
| Parser: ` ```json ... ``` ` ve ` ``` ... ``` ` | **PASS (izole C# test)** | Etiketli fenced `FOLLOW` ve etiketsiz CRLF fenced `STAY` cevapları ayrıştırıldı; action ve dialogue değerleri doğrulandı. Aynı Unity JsonUtility sınırlaması geçerlidir. |
| Parser: hatalı/boş/uygunsuz JSON | **PASS (izole C# test)** | Hatalı JSON, boş yanıt, desteklenmeyen kod bloğu etiketi, bilinmeyen action ve eksik dialogue reddedildi; her durumda hata mesajı üretildi. |
| Ollama `/api/generate` `format: "json"` — üretim prompt'u | **PASS (yerel model API testi)** | Mevcut `gemma3:4b` ve `BuildSystemPrompt` yönergeleriyle yardım isteği ham JSON `FOLLOW`, umursama isteği ham JSON `STAY` döndürdü; iki diyalog da ayrıştırılabilir ve beklentiyle uyumluydu. Test edilen isteğe `format: "json"` alanı kondu. |
| Karar -> NPC takip çağrısı | **PASS (statik bağlantı kontrolü)** | `OnChoiceDialogueSuccess` -> `ApplyDecision` -> `follower.SetFollowing(decision.Action == NpcAction.Follow)` zinciri doğrulandı. `NPCFollowPlayer` true için takip/`SetDestination`, false için `ResetPath` uygular. Bu kontrol gerçek NPC hareketi testi değildir. |
| Unity Play Mode / oyuncu WASD / NPC etkileşimi / NavMesh | **NOT VERIFIED** | Tuş girişi, karakter hareketi, UI callback'leri, NavMesh agent konumu/yolu ve ağaç-kaya etrafından dolaşma gözlemlenmedi. |
| Otomatik testler | **NOT VERIFIED / test bulunamadı** | `Assets` altında NUnit `[Test]`/`[UnityTest]` fixture'ı bulunamadı; `ProjectSetupTest` ve `OllamaConnectionTest` sahne bileşenleridir, otomatik test fixture'ı değildir. |

## 6. Kritik Eksikler ve Öncelik

### P0 — Demo/teslim anında bloklayabilecek durum

Bu denetimde doğrulanmış P0 yok. Yerel Ollama endpoint'i erişilebilir ve hedef model listede mevcut. Unity oyun içi akışı çalıştırılmadığından demo hazır olduğu sonucu çıkarılamaz.

### P1 — Akademik ister eksikleri

1. **Kalıtım/polimorfizm gösterimi yok.** Arayüz kullanımı tek başına bu isterin yerine geçmiyor.
2. **Dinamik sahne bağlamı LLM'e verilmiyor.** Diyalog geçmişi/action eşlemesi var; güncel oyun verisi prompt'a taşınmıyor.
3. **NavMesh davranışı gerçek sahnede doğrulanmamış.** Baked data referansı ve takip kodu var; engellerden dolaşma ve ulaşılamayan/partial path davranışı kanıtlanmadı.
4. **Unity JSON ayrıştırma ve callback akışı Play Mode'da çalıştırılmadı.** İzole parser testi Unity `JsonUtility`'yi kullanmadı; gerçek oyun akışı henüz doğrulanmadı.

### P2 — Güvenilirlik ve kalite

1. Takip kodu hedefi güncelliyor ve `SetDestination` reddini bildiriyor; fakat kabul edilmiş yolun `pathStatus`/partial durumuna göre bir kurtarma veya kullanıcı geri bildirimi yok.
2. Sahne başlangıcındaki etkin `OllamaConnectionTest`, NPC'den ayrı `llama3.2` isteği gönderiyor. Her iki model de yerel listede; yine de gereksiz ikinci istek kaynak tüketebilir. Log etiketi `Gemm3:4b` iken ayarlı model `llama3.2`, bu da tanıyı yanıltabilir.
3. NPC model child nesnesine eklenen SphereCollider trigger; bu, fiziksel bloklama kanıtı değildir.
4. Proje sürümü 2022.3.62f3; isterdeki 2022.3.35f1'den farklı. Hedef sürümde açılma/çalışma ayrıca denenmeli.
5. Derleme sonucu ortam nedeniyle alınamadı; `.NET Framework 4.7.1` targeting pack eksikliği giderilmeden `dotnet build` kanıt sayılamaz.

## 7. Minimum Düzeltme Planı

1. **Mevcut karar akışını Unity Play Mode'da uçtan uca test edin:** Yardım -> `FOLLOW` ile takip başlasın; Umursama -> `STAY` ile takip dursun. Başlangıç repliğinin aksiyonu bilerek ertelediğini ve hakaretin LLM'den bağımsız kayıp durumu olduğunu rapor/demoda açıkça gösterin.
2. **NavMesh için Play Mode testi yapın:** Agent başlangıçta NavMesh üzerinde mi; ağaç/kaya etrafında dolaşıyor mu; hedef değiştiğinde yol yenileniyor mu; partial/invalid hedef ne yapıyor? Yalnızca test başarısızsa en küçük bake/agent/path düzeltmesini belirleyin.
3. **Kalıtım isteri için:** Anlamlı ortak NPC taban sınıfı ve özel davranışlı alt sınıf(lar) tasarlayın; taban tür üzerinden farklı davranışın dispatch edildiğini gösterin. Yapay sınıf hiyerarşisinden kaçının.
4. **Bağlam aktarımı için:** Yalnızca projede gerçekten bulunan ve güncel oyun durumunu prompt'a ekleyin. Şu an sağlık/envanter/güven sistemi varsaymayın; yeni veri sistemi ayrı kapsam/onay gerektirir.
5. **Oyuncu/NPC çarpışması için:** Play Mode'da oyuncunun NPC ve etkileşimli çevreyle fiziksel temasını test edin. Trigger collider'ı solid engel olarak değerlendirmeyin.

JSON format kısıtlaması, fenced JSON desteği ve mevcut aksiyon hattı bu follow-up kapsamında düzeltildi/doğrulandı. Unity JsonUtility ve gerçek sahne callback'i/karakter hareketi Play Mode'da test edilmedi.

## 8. Tekrar Test Planı

1. Unity Console/Problems'ta script derleme ve serialization hatası olmadığını kontrol edin; hedef sürüm 2022.3.35f1 ise o sürümde ayrıca açın. `dotnet build` için eksik .NET Framework 4.7.1 targeting pack'i gereken makinede temin edin.
2. Başlangıçtaki `/api/tags` ve `gemma3:4b` kullanılabilirliğini doğrulayın. `/api/generate` isteğinin `format: "json"` gönderdiğini ve ham `response` çıktısının tek JSON nesnesi olduğunu Unity servisinden doğrulayın.
3. Oyuncuyla NPC arasındaki mesafeyi ölçerek prompt'un 2 birimde görünmesini, `E` ile başlangıç diyaloğunu ve seçenek panelini; yanıtın balonda gösterilmesini test edin.
4. Yardım seçeneğinde `FOLLOW` aksiyonunun bir defa uygulanıp agent'ı hareket ettirdiğini; Umursama seçeneğinde `STAY` aksiyonunun takip yolunu sıfırladığını doğrulayın. Başlangıç cevabının takip başlatmadığını ve hakaretin ayrı kayıp akışını test edin.
5. Unity `JsonUtility` ile normal/fenced JSON, bozuk JSON, eksik/boş alan ve bilinmeyen action durumlarını doğrulayın; hata görünür olsun ve aksiyon uygulanmasın.
6. `FOLLOW` açıkken oyuncuyu ağaç/kaya arkasında ve birkaç farklı hedefte deneyin. Dolaşmayı, hedef yenilenmesini, durma mesafesini ve unreachable/partial path davranışını kaydedin.
7. WASD ile düz zeminde yürüme, oyuncu/NPC/çevre collider davranışı, animasyon ve diyalog sırasında kontrol/cursor akışını gözlemleyin.
8. Kalıtım örneği veya dinamik bağlam eklendiyse taban tip üzerinden dispatch'i ve prompt'a güncel değer aktarımını tekrar test edin.

## 9. Doğrulanamayan Maddeler

- Unity Editor Play Mode ve gerçek sahne/Inspector çalışma zamanı davranışı çalıştırılmadı. Batchmode denemesi aynı projenin açık Unity örneği nedeniyle reddedildi.
- Yerel Ollama erişilebilir; `format: "json"` ile üretim prompt'una yakın iki istek ham JSON ve doğru `FOLLOW`/`STAY` döndürdü. Ancak UnityWebRequest -> `OllamaResponse` -> `TryParseNpcDecision` -> UI/aksiyon callback zinciri çalıştırılmadı.
- Parser testleri mevcut C# metotları kaynak dosyasından alınarak .NET 10 harness'te yapıldı; `JsonUtility` için System.Text.Json tabanlı test dönüştürücüsü kullanıldı. Bu test Unity'nin gerçek `JsonUtility` davranışını ispatlamaz.
- Ollama'nın `format: "json"` modu test edilen isteklerde kod çitsiz JSON üretti; parser yine de format kısıtlamasına tek başına güvenmeyip her iki fenced biçimi kabul edecek şekilde çalışır.
- NavMeshData ikili asseti bağlı olsa da bake geometrisinin ağaç/kaya çevresindeki gerçek sonucu, agent'ın NavMesh başlangıç konumu ve runtime path status doğrulanmadı.
- WASD, kamera, animator geçişleri, UI etkileşimi ve NPC fiziksel çarpışması gerçek oyun sırasında test edilmedi. Sahne dosyasında açıkça adlandırılmış bir kapı bulunamadığından kapı etkileşimi de doğrulanmadı.
- `dotnet build` .NET Framework v4.7.1 reference assemblies olmadığı için çalışmadı; bu nedenle gerçek derleme sonucu doğrulanmadı.
- Unity sürümü 2022.3.62f3 olarak tespit edildi; isterde geçen 2022.3.35f1 ile uyumluluk ayrıca test edilmedi.

## Sonuç

**Mevcut proje belirtilen akademik isterlerin tümünü gerçekten karşılıyor mu?** Hayır. Yapılandırılmış `FOLLOW`/`STAY` kararını işleyen kod, JSON format kısıtlaması ve Markdown kod bloğu desteği mevcut; izole parser testleri ile yerel model API testleri geçti. Ancak Unity `JsonUtility`/UI callback zinciri ve gerçek NPC hareketi Play Mode'da doğrulanmadı. Kalıtım/polimorfizm ve dinamik sahne bağlamı isterleri karşılanmıyor; NavMesh'in ağaç/kaya çevresindeki gerçek çalışması da doğrulanmadı.

**Teslim öncesi mutlaka giderilmesi gerekenler:** Unity Play Mode'da karar parse'ını ve Yardım/Umursama seçimlerinin takibi başlatıp durdurduğunu gösterin; ardından gerçek NavMesh engel dolaşmasını ve oyuncu etkileşim akışını test edin. İsterler birebir puanlanacaksa anlamlı kalıtım/polimorfizm örneği ile mevcut oyun durumunun prompt'a aktarılması da tamamlanmalı. `dotnet build` sonucu mevcut ortamda .NET Framework targeting pack eksikliği nedeniyle doğrulanamadı.
