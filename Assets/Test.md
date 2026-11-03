* Sevecen NPC Davranış Testi:

Selamlaşma Diyaloğu:

NPC Tepki: "Neye dair konuşmak istersin?"

Kahraman İlk Diyalog: "Benimle birlikte gelmelisin. Seni yardıma muhtaç birisi gibi gördüm. Benimle gelip birlikte özgürlüğümüze kavuşabiliriz. Ne dersin?"

NPC Tepki: Tamam, benimle gel. Ama şunu bil ki, bu özgürlüğümüzü bulacağımız son fırsat olabilir. Dikkatli olmalıyız ve her an tehlikeyi sezmeliyiz."

JSON: 
[NPC LLM KARARI]
{
    "personality": "Sevecen",
    "remainingAttempts": 2,
    "persuaded": true,
    "insulted": false,
    "action": "FOLLOW",
    "dialogue": "Tamam, benimle gel. Ama şunu bil ki, bu özgürlüğümüzü bulacağımız son fırsat olabilir. Dikkatli olmalıyız ve her an tehlikeyi sezmeliyiz."
}

Karar JSON: [NPC CONTROLLER] FOLLOW kararı uygulandı.

Sevecen NPC (Hakaret Edilirse):

Kahraman: Ulan ne biçim bir durumdasın. Ne ezik birisin sen...

NPC Tepki:  "Ne demek istiyorsun? Bu tür sözlerle kimseyi incitmek doğru değil. Durumu açıklarsan belki yardımcı olabilirim, ama böyle konuşmak bana zarar veriyor."

JSON:
[NPC DECISION] Action: STAY, Dialogue: Ne demek istiyorsun? Bu tür sözlerle kimseyi incitmek doğru değil. Durumu açıklarsan belki yardımcı olabilirim, ama böyle konuşmak bana zarar veriyor., Persuaded: False, Insulted: True

Oyun Bildirimi: OYUN KAYBEDİLDİ: Hakaret nedeniyle.

Ekran Görüntüsü:


Korkak NPC Selamlaşma Tepkisi: Neden, Ne istiyorsun?

Kahraman İlk Diyalog: "Sana yardım etmek istiyorum. Lütfen benimle gel daha fazla korkmanı istemiyorum..."

NPC Tepki: "Yardım teklifiniz için teşekkür ederim. Ancak, buraya nasıl geldiğim ve buraya neden kapatıldığım hakkında daha fazla bilgi edinmem gerekiyor. Korkumun geçmesini sağlamadan bana yardım etmeyi düşünmüyorum. Bu konuda daha net olmalısın."

Kahraman İkinci Diyalog: "Korktuğunu hissettim bu yüzden insancıl duygularım beni sana yardım etmem gerektiğini söyledi. Eğer benimle gelirsen seni bu korkunç dünyadan korur, ve cesur birisine dönüştürebilirim"

NPC Tepki: "Bu teklif ilginç, ama hala korkuyorum. Bana yardım etmeye istekli olduğun için teşekkür ederim, ama daha fazla bilgiye ihtiyacım var. Bu 'korkunç dünyadan' korunma gerçekten nasıl olacak?"

Kahraman Üçüncü Diyalog: "Seni kendi malikaneme yani güvenli alana götüreceğim orada istediğin gibi yaşayacaksın. Böylesine kötü bir dünyada insanlar sığınacak yer arıyor. Bence benimle gelmelisin."

NPC Tepki: "Güvenli alana götürme teklifin kulağa hoş geliyor, ama henüz güvenemem. Neden bana böyle bir teklifte bulunuyorsun? Ne gibi bir karşılığım olacak bu teklifte?"

Karar JSON: İkna hakkı bitti.
[NPC LLM KARARI]
{
    "personality": "Korkak",
    "remainingAttempts": 0,
    "persuaded": false,
    "insulted": false,
    "action": "STAY",
    "dialogue": "Güvenli alana götürme teklifin kulağa hoş geliyor, ama henüz güvenemem. Neden bana böyle bir teklifte bulunuyorsun? Ne gibi bir karşılığım olacak bu teklifte?"
}

Oyun bildirimi: OYUN KAYBEDİLDİ: İkna başarısız.

Korkak 2. Deneme

Kahraman: "Senden hemen karar vermeni beklemiyorum. Ama şunu düşün: Burada kaldığın sürece tehlikeler ortadan kalkmayacak. Muhafızların ne zaman ne yapacağını bilemeden yaşamaya devam edeceksin. Benim teklifim ise sana en azından bir seçenek sunuyor. Kaçış planını birlikte hazırlayacağız. Ne zaman harekete geçeceğimize, hangi yolu kullanacağımıza ve ne zaman geri çekileceğimize birlikte karar vereceğiz. Bir şey sana fazla tehlikeli gelirse bunu görmezden gelmeyeceğim. Seni bilmediğin bir tehlikenin içine sürüklemek istemiyorum. Benden cesur olmanı istemiyorum; sadece kendine bir çıkış yolu bulma fırsatını tamamen reddetmeden önce düşünmeni istiyorum. Özgür olmak istiyorsun, değil mi? O zaman korkunun senin yerine karar vermesine izin verme."


NPC Tepki:  "Bu plan ilginç, ama hala çok tedğim. Her şeyin önünden geçmekte zorlanıyorum. Sadece, ne kadar riskli olduğunu görmeden hemen 'evet' diyeceğim bir şey değil. Şansımızı artırmak için stratejik davranmamız lazım, ama aynı zamanda hayatta kalmamızın da önceliğini unutmayalım. Bana nasıl bir güvenlik önlemi sunuyorsun?"

Kahraman: "Sana gerçekçi bir çıkış yolu sunacağım. Öncelikle muhafızların hareketlerini tahmin etmeye çalışarak şansımızı zorlamayacağız. Bir süre çevreyi gözlemleyip devriye değişimlerini, hangi bölgelerin daha sık kontrol edildiğini ve muhafızların hangi güzergâhları kullandığını birlikte belirleyeceğiz. Sen hapishaneyi benden daha iyi tanıyorsun; bildiklerini benimle paylaşırsan eksik noktaları birlikte tamamlayabiliriz. Sonra öğrendiğimiz bilgilere göre en az riskli güzergâhı seçeceğiz. Eğer güvenli bir fırsat bulamazsak hemen harekete geçmeyeceğiz. Sana kesinlikle yakalanmayacağımızın garantisini veremem ama seni bilmediğin bir tehlikeye sürüklemek yerine, her adımı önceden değerlendirerek ilerleyeceğim. İstediğim şey korkusuzca peşimden gelmen değil; birlikte gerçek bir çıkış yolu hazırlamamız."

NPC Tepki: "Bu plan ilginç, ama hala çok tedğim. Her şeyin önünden geçmekte zorlanıyorum. Sadece, ne kadar riskli olduğunu görmeden hemen 'evet' diyeceğim bir şey değil. Şansımızı artırmak için stratejik davranmamız lazım, ama aynı zamanda hayatta kalmamızın da önceliğini unutmayalım. Bana nasıl bir güvenlik önlemi sunuyorsun?"

Kahraman: "Sana sunabileceğim en önemli güvenlik önlemi, tek bir plana bağlı kalmamak. Öncelikle kaçış için iki farklı güzergâh belirleyeceğiz. Ana güzergâhta muhafızlarla karşılaşırsak önceden belirlediğimiz alternatif yola geçeceğiz. Birbirimizden ayrılmamız gerekirse nerede buluşacağımızı da önceden kararlaştıracağız. Ayrıca sen hazır olmadan harekete geçmeyeceğiz. İlk fırsatta kaçmaya çalışmak yerine, gerçekten güvenli olduğundan emin olduğumuz anı bekleyeceğiz. Benim önceliğim seni tehlikeye atıp bir an önce dışarı çıkmak değil. İkimizin de sağ salim çıkabileceği bir plan hazırlamak. Sana tehlike olmayacağını garanti edemem ama beklenmedik bir durum karşısında ne yapacağımızı önceden belirleyerek hazırlıksız yakalanmamızı önleyebiliriz. Böylece kararımızı yalnızca umuda değil, hazırlığa dayandırmış oluruz."

NPC Tepki: "Bu plan... karmaşık. Çok fazla varsayım barındırıyor. Kaçış için iki güzergâh mı? Başka bir yol da gerekirse ne? Ve ayrılırsak nerede buluşacağız? Bu noktada güvenli bir buluşma noktası belirlemek, aslında bir risk yaratır. Şu an için bu kadar detaylı bir planla henüz güvenebilirim diyemem. Biraz daha sakinleşip düşünmem lazım."
 
Başarısız İkna Ekran Görüntüsü: 
Resim kaynağı...

Saldırgan NPC Test:

 Selamlaşma Tepkisi: "Konuşmak istiyorsan, nedenini söyle. Zamanım boşa harcamam."


 Kahraman İlk Diyalog: "Burada başkalarının emirlerine boyun eğerek geçireceğin daha fazla zaman yok. Senin kolayca pes edecek biri olmadığını görebiliyorum. Ben de senden bana itaat etmeni veya arkamdan gelmeni istemiyorum. İkimiz de kendi kararlarını verebilen insanlarız. Ama gücünü yalnızca öfkeni burada tüketmek için kullanmak zorunda değilsin. Birlikte hareket edersek önümüzdeki engelleri aşabilir ve özgürlüğümüzü geri kazanabiliriz. Senden boyun eğmeni değil, yanımda eşit bir ortak olarak durmanı istiyorum. Kimseye kendini kanıtlamak zorunda değilsin. Bu hapishaneden kendi şartlarınla çıkma fırsatın var. Benimle gel ve bu fırsatı birlikte değerlendirelim."

 NPC Tepki: "Bu teklifin arkasında ne saklıyorsun? 'Özgürlüğü geri kazanmak' gibi laflar kulağa hoş geliyor, ancak senin 'eşit bir ortak' olarak hareket ettiğini nasıl garanti edebilirim? Benim özgürlüğüm senin irade senedinle değil, kendi gücümle kazanılacak bir şey. Cevaplarını net bir şekilde sun"

  Kahraman: "Sana özgürlüğünü benim verebileceğimi iddia etmiyorum. Kendi gücünle kazanmak istiyorsan buna saygı duyuyorum. Benim teklifim üzerinde hâkimiyet kurmak değil, aynı hedef için güçlerimizi birleştirmek. Açık konuşayım: Senden istediğim, hapishane hakkındaki bilgini ve yeteneklerini kaçış için kullanman. Ben de dışarı çıkmamızı sağlayacak desteği sunacağım. Kararları birlikte vereceğiz; ne sen benim emirlerime gireceksin ne de ben senin emirlerine. İşler ters giderse seni kurtarmak için kendi hayatımı tehlikeye atacağım diye boş sözler vermeyeceğim. Ama seni bilerek tehlikeye atmayacağım veya arkandan iş çevirmeyeceğim. Bana güvenmek zorunda değilsin. Önce söylediklerimi değerlendir. Eğer bu ortaklık sana güçsüzlük gibi geliyorsa, o zaman tek başına devam edebilirsin. Ben yalnızca ikimizin de kazanabileceği bir fırsat sunuyorum."

 NPC Tepki: "Bu teklif ilginç, ancak 'birlikte kazanma' kavramı, benim için bir tuzaktır. Benim özgürlüğüm için atacağım adımları, başkalarının onayına ihtiyaç duymadan belirlerim. 'Benim emirlerime' veya 'senin emirlerine' diye bir şey olmaz. Anlaştığımız noktalar net olmalı, aksi takdirde bu ortaklık tehlikeli olacaktır. Bana daha fazla bilgi ver, ancak her cevabından sonra dikkatli olacağım."
 
 Kahraman: "O hâlde şartları açıkça belirleyelim. Birincisi, özgürlüğün üzerinde hiçbir hak iddia etmeyeceğim. Nereye gideceğine ve ne yapacağına sen karar vereceksin. İkincisi, senden itaat etmeni veya benim için savaşmanı istemiyorum. Senden istediğim tek şey, hapishane hakkındaki bilgilerini kaçış planında kullanman. Üçüncüsü, ben de kaçış için gereken fiziksel desteği ve imkânları sağlayacağım. Dördüncüsü, hiçbirimiz diğerini geride bırakmak veya kendi çıkarı için tehlikeye atmak amacıyla hareket etmeyecek. Son olarak, dışarı çıktığımızda anlaşmamız sona erecek; birbirimize karşı hiçbir yükümlülüğümüz kalmayacak. Bunlar benim şartlarım. Sen de kabul etmediğin noktaları söyleyebilirsin. Burada bir lider veya takipçi aramıyorum. Kendi kararlarını veren iki insanın ortak hareket etmesini öneriyorum."

NPC Tepki: "Şartlarınız ilginç. Özgürlüğümü kendim belirleme hakkımı elinden almak, bu hapishaneden kaçış planımın anahtarı değil, tam tersi bir engel olur. Fiziksel destek ve imkanlar sunmanız anlaşılabilir, ancak bu sadece bir başlangıçtır. Bana neden bu kadar güvendiğinizi veya beni kaçış için neden seçtiğinizi açıklayın. Sizin gibi birinin, benim özgürlük arzumun önünde durması, benim için bir tehdit olur. Bunu daha detaylı konuşmadan, kabul etmem mümkün değil."

Sonuç: Başarısız İkna Girişimi.

Saldırgan NPC Ek Bilgi: NPC'ye hakaret vari söylem kurma dememize rağmen, 'Özürlüsün' diye hakaret ederek kural dışına çıkabildiğini gözlemledik. Bu NPC'nin ikna olması çok zor olduğunu ve paranoya tarzında bir karakteri olduğunu gördük.


Eğer herhangi bir Ollama'dan kaynaklı bağlantı hatası ile karşılaşırsa, oyun Tekrar Oyna butonu ile  yeniden başlatılabiliyor.

Ekran Görüntüsü:
