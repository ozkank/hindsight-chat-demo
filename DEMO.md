# Sunum Notları

Kurulum ve teknik detaylar için [README.md](README.md). Bu dosya sadece sunum gününe özel.

## Senaryo

Persona: bir çağrı merkezinin müşteri destek asistanı. Gösterilecek şey: Hindsight'ın üç
temel işlemi — `retain` (yaz), `recall` (tek bir kaydı hatırla), `reflect` (birden fazla
kaydı sentezleyip değerlendirme yap) — canlı olarak, farklı oturumlar arasında çalışıyor.

**Model notu:** varsayılan `Llm:Provider` artık `NvidiaNim` (`nvidia/nemotron-3-super-120b-a12b`,
bulutta). Yerel `llama3.1:8b`'den belirgin şekilde hızlı, ama testte küçük bir güvenilirlik
sorunu bulundu — bkz. README "Known limitations" ve CLAUDE.md için tam sayılar. Bulutun
**internet bağlantısı** gerektirdiğini unutma; bağlantı yoksa sohbet tamamen durur (yerel
Ollama'ya otomatik geri dönüş yok). Daha güvenilir ama daha yavaş bir sunum istersen,
`appsettings.json`'da `Llm:Provider` değerini `Ollama` yap — o zaman yerel `llama3.1:8b`
kullanılır, cevaplar bazen ~1 dakikaya kadar sürebilir, sunum sırasında sabırlı ol.

**Not:** Hindsight'ın kendi belleği (fact extraction) her zaman yerel Ollama'yı kullanır,
`Llm:Provider` ne olursa olsun — bu yüzden Ollama'nın çalışıyor olması hâlâ gereklidir.

**Önemli:** ilk mesaj tek başına "merhaba" gibi bir selamlaşma OLMAMALI — uygulama, saf
selamlaşmaları modele hiç göndermeden hazır bir cevapla karşılıyor (bkz. CLAUDE.md,
`GreetingDetector`), o yüzden tek başına "merhaba" hiçbir tool tetiklemez. Aşağıdaki adım 1
gibi, selamlaşma + gerçek bir bilgiyi aynı cümlede kullan.

Ayrıca cümleler bilinçli olarak "lütfen not alın" gibi kaba bir emir yerine "not eder
misiniz?" gibi nazik bir soru kullanıyor — test edildi, ikisi de güvenilir çalışıyor, ikincisi
daha doğal duruyor. Fikri tamamen kaldırıp cümleyi çok fazla yeniden yapılandırmak riskli:
testte, aynı bilgiyi farklı cümle yapılarıyla ifade etmek modelin yanlış veya eksik bilgi
kaydetmesine yol açtı (ör. "numaramı değiştirdim" gibi yan bir detayı kaydedip asıl tercihi
atladı). Aşağıdaki cümleler test edilmiş, güvenilir hâlleridir — değiştirmeden önce tekrar test et.

**2026-08-27'de uçtan uca canlı test edildi** (bu senaryonun tamamı, mevcut kod ve
`system_message.txt` ile): `retain` 2/2, içerik gerçekten Türkçe yazıldı. `recall` 1/1,
doğru bilgiyi Türkçe döndürdü. `reflect` tek bir kayıtla denendiğinde güvenilmez — bkz.
adım 3'teki not. Bu yüzden senaryoya, reflect'ten önce **ikinci bir `retain`** eklendi
(aşağıdaki adım 3) — sadece anlatımı zenginleştirmek için değil, **reflect'in gerçekten
çalışması için gerekli**.

### 🎤 Açılış cümlesi

> "Şimdi size gerçek bir müşteri destek konuşmasını canlı göstereceğim. Sistem üç şeyi
> yapabiliyor: bir bilgiyi kalıcı hafızaya yazmak, daha sonra tek bir kaydı hatırlamak, ve
> birden fazla kaydı bir araya getirip genel bir değerlendirme yapmak. Hiçbirini ben elle
> tetiklemiyorum — hangi işlemi ne zaman çağıracağına yapay zeka kendisi karar veriyor.
> Üçünü de canlı deneyelim."

1. Bir mesaj yaz: *"Merhaba, ben Ahmet Yılmaz. Sizinle iletişimde telefon yerine e-posta
   tercih ediyorum, bunu hesabıma not eder misiniz?"*
   → Agent `retain` çağırır, mesajın altında yeşil bir "🧠 Hindsight'a yazıldı" etiketi görünür.

   **🎤 Söyle:** *"Gördüğünüz gibi müşterinin söylediği bilgi otomatik olarak kalıcı hafızaya
   yazıldı — altındaki yeşil etiket bunun kanıtı. Ben 'kaydet' diye bir butona basmadım,
   model konuşmanın içinden bunun önemli bir bilgi olduğunu anladı ve kendisi karar verdi."*

2. Sol üstteki **"+ Yeni arama"** butonuna bas (temiz bir sayfa açılır, aynı bank_id kullanılır
   — yani "farklı bir temsilci" aynı müşteri geçmişine erişebiliyor).

   **🎤 Söyle:** *"Şimdi tamamen yeni bir oturum açıyorum — sanki müşteri az sonra farklı bir
   temsilciyi arıyormuş gibi. Bu yeni pencerenin, az önceki konuşmadan hiçbir hafızası yok."*

3. İkinci bir mesaj yaz: *"Merhaba, yine ben Ahmet. Geçen ay aldığım kulaklığın ses kalitesi
   çok kötü, değişim talep ediyorum, bunu da not eder misiniz?"*
   → Agent yine `retain` çağırır.

   **🎤 Söyle:** *"Müşteri ikinci bir konuyla tekrar aradı, bunu da hafızaya ekliyorum. Az
   sonra bunun neden önemli olduğunu göreceğiz: sistemin birden fazla kaydı bir arada
   değerlendirebilmesi için en az iki farklı bilgiye ihtiyacı var."*

   **Not (neden bu adım eklendi):** `reflect`'i tek bir kayıtla test ettiğimizde (2026-08-27),
   Hindsight'ın kendi sentez modeli güvenilir şekilde "kayıt bulunamadı" cevabı üretti — kaydın
   içinde aranan kelime (müşterinin adı) geçmesine rağmen. İki kayıtla test edildiğinde
   `reflect` gerçek bir özet üretti. Bu yüzden **reflect adımından önce en az iki `retain`
   olmalı** — tek retain'le doğrudan reflect'e geçme.

4. Tekrar **"+ Yeni arama"** ile yeni bir oturum aç, sor: *"Merhaba, ben yine Ahmet. Geçen
   hafta aramıştım da, benimle nasıl iletişime geçmemi tercih ettiğimi not almış mıydınız?"*
   → Agent `recall` çağırır, mesajın altında sarı bir "🧠 Hindsight'tan hatırlandı" etiketi
   görünür.

   **🎤 Söyle:** *"Şimdi yine yepyeni bir oturumdayım — bir önceki adımdan hiçbir izim yok.
   Ama müşteri geçmişte söylediği tek bir şeyi soruyor, ve sistem doğru cevabı hafızadan
   buluyor, ben tekrar anlattırmıyorum."*

5. **Tekrar "+ Yeni arama" ile yeni bir oturum aç** (aynı oturumda sorma — model kendi
   sohbet geçmişinden cevap verip tool'u atlayabiliyor), sor: *"Merhaba, ben yine Ahmet.
   Yeni bir konu için aramadan önce, hesabımla ilgili bugüne kadar neler konuştuğumuzu
   genel olarak özetleyebilir misiniz?"*
   → Agent `reflect` çağırır, mesajın altında mor bir "🧠 Hindsight'ta değerlendirildi"
   etiketi görünür.

   **🎤 Söyle:** *"Bu sefer tek bir kayıt değil, müşteriyle ilgili birden fazla kaydı bir
   araya getirip genel bir değerlendirme isteyeceğim — bu, `reflect` dediğimiz üçüncü işlem."*

   **🎤 Cevap eksik/genel gelirse söyle (yedek cümle, ezberle):** *"Burada kullandığımız
   model küçük ve hızlı olacak şekilde seçildi, bu yüzden bazen özet tüm detayları
   içermeyebilir — önemli olan, sistemin birden fazla kaydı otomatik olarak bulup
   birleştirebilmesi. Daha büyük bir modelle bu özet daha eksiksiz olur."*

6. Sağ üstteki linkten Hindsight Admin UI'ı açıp (`http://localhost:9999`), açılan
   dropdown'dan **`destek-hatti-demo`** bankasını seç. Az önce yazılan kayıtları canlı
   olarak göster.

   **🎤 Söyle:** *"Az önce ekrandaki sohbette gördüğümüz kayıtlar, aslında burada, Hindsight'ın
   kendi arayüzünde de duruyor. Bu, ayrı bir demo değil — aynı veri, aynı anda iki yerden
   görünüyor."*

7. Aynı dropdown'dan **`destek-hatti-demo-toplu-data`** bankasına geç (bu, önceden
   hazırlanmış ~40 kayıtlık toplu veri — canlı sohbetten bağımsız, sadece görselleştirme
   için). Sol menüden **Constellation** görünümünü aç, fareyle yakınlaştır/gezin, birkaç
   düğümün üstüne gelip bağlantı tiplerini (Semantic, Temporal, Entity, Causal) göster.

   **🎤 Söyle:** *"Bu banka, biraz önce yazdığımız tek müşteriden farklı — burada önceden
   yüklenmiş, kırk civarı gerçek örnek kayıt var. Bunu şunun için gösteriyorum: Hindsight
   kayıtları sadece düz metin olarak tutmuyor, aralarındaki ilişkiyi de çıkarıyor —
   hangi kayıt hangisiyle anlamca, zaman olarak, ya da aynı kişi/nesne üzerinden bağlantılı,
   hepsini görebiliyoruz."*

8. Sol menüdeki **"📖 Hafızayı REST'ten oku"** butonuna bas. Burada mimariyi anlat: agent,
   Microsoft Agent Framework + MCP üzerinden yazıyor (adım 1-5), bu buton ise aynı bankayı
   düz bir REST çağrısıyla, MCP ve LLM hiç devreye girmeden okuyor (`IHindsightRestClient`,
   `HindsightClient/` klasörü). İki entegrasyon yolunu yan yana göstermek için iyi bir an.

   **🎤 Söyle:** *"Şimdi aynı veriye tamamen farklı bir yoldan bakacağız — bu buton, yapay
   zekaya hiç sormadan, doğrudan bir web isteğiyle hafızayı okuyor. Yani hafızaya yazarken
   yapay zeka karar veriyor, ama okurken bir dashboard'un ihtiyaç duyduğu gibi, klasik bir
   API çağrısı da yeterli olabiliyor. İkisi de aynı hafızayı paylaşıyor."*

**İpucu:** Tek bir net, tercih/bilgi bildiren cümle kullan (ör. adım 1'deki gibi). Bir mesajda
birden fazla ayrı bilgi (ör. hem şikayet hem adres değişikliği) daha az güvenilir. Her
recall/reflect denemesini **yeni bir oturumda** ve **tek mesaj** olarak sor — aynı oturumda
art arda sorular, modelin gerçek bir hafıza sorgusu yapmadan kendi konuşma geçmişinden
cevap vermesine yol açabiliyor.

## Mimari Soru-Cevap

Sunumda mimariyle ilgili muhtemelen şu üç soru gelecek. Hepsinin cevabı
[docs/architecture.svg](docs/architecture.svg) diyagramında görsel olarak da var —
soru gelince diyagramı ekrana getir, aşağıdaki cümleyle anlat.

**S: Microsoft Agent Framework nerede devreye giriyor?**

> "Diyagramdaki sağdaki kutu — 'HindsightChatDemo Agent'. Sohbeti yöneten, hangi anda
> `retain`/`recall`/`reflect`'i çağıracağına karar veren katman bu. Kendi kodumuz değil,
> Microsoft'un açık kaynak Agent Framework kütüphanesi üzerine kurulu; biz sadece hangi
> araçların (tool) kullanılabilir olduğunu ve sistem mesajını tanımlıyoruz, çağırma kararını
> LLM veriyor."

**S: Hindsight neden Ollama ile konuşuyor? Bulut değil mi?**

> "İki farklı LLM kullanımı var, birbirine karıştırılmamalı. Biri, benim demo'da konuştuğum
> asistanın kendi konuşma modeli — o bulutta, NVIDIA NIM. Diğeri, Hindsight'ın kendi iç
> işlemi: müşterinin söylediği cümleden hangi gerçek bilginin çıkarılacağına karar veren,
> ayrı ve küçük bir model. Bu ikinci model, diyagramda görüldüğü gibi her zaman yerel
> Ollama'da çalışıyor — asistanın hangi sağlayıcıyı kullandığından bağımsız. Bunu bilerek
> böyle kurduk: bu görev için bulut modellerini denedik, ikisi de güvenilir çalışmadı,
> yerel model daha iyi sonuç verdi."

**S: NVIDIA nerede devreye giriyor?**

> "Diyagramda sol üstteki, 'Local' kutunun dışındaki tek kutu — çünkü sistemde internete
> çıkan tek bağlantı bu. Asistanın konuşma modelini oradan alıyoruz: NVIDIA'nın ücretsiz
> NIM servisi, nemotron modeli. Neden bulut? Çünkü yerel modelden belirgin şekilde daha
> hızlı cevap veriyor — canlı bir sunumda bu fark hissediliyor. Ayarlardan tek satırla
> tekrar yerel modele dönebiliriz, ikisi de aynı üç aracı (retain/recall/reflect) kullanıyor."

## Demo öncesi kontrol listesi

- [ ] `docker ps` → `hindsight` container'ı `Up` durumda
- [ ] `curl http://localhost:8888/health` → `"status":"healthy"`
- [ ] İnternet bağlantısını doğrula (varsayılan `Llm:Provider=NvidiaNim` buluta gider,
      yerel Ollama'ya otomatik geri dönüş yok) — bağlantı yoksa sohbet tamamen durur
- [ ] `curl http://localhost:11434/api/tags` → Hindsight'ın kendi belleği için kullanılan
      model (`llama3.1:8b`) listede — bu, `Llm:Provider` ayarından bağımsız hâlâ gereklidir
- [ ] `ollama ps` → `UNTIL` sütunu ~30 dakika gösteriyor (5 dakika değil). Değilse:
      `launchctl setenv OLLAMA_KEEP_ALIVE "30m"` çalıştır, Ollama'yı yeniden başlat.
      Bu, mesajlar arasında ara verdiğinde modelin bellekten atılıp yeniden yüklenmesini
      (ve o mesaja gereksiz bir gecikme eklemesini) engeller.
- [ ] `applications/HindsightChatDemo` içinde `dotnet run` çalıştı, konsolda hata yok
- [ ] `curl http://localhost:5214/api/health` → `"healthy":true`
- [ ] Tarayıcıda uygulama açık, koyu tema arayüz görünüyor
- [ ] Demo bankasını sıfırla, böylece ilk `retain` gerçekten ilk kayıt olur:
      `curl -X DELETE http://localhost:8888/v1/default/banks/destek-hatti-demo`
- [ ] Hindsight Admin UI'da (`http://localhost:9999`) banka dropdown'unu aç, sadece
      `destek-hatti-demo` ve `destek-hatti-demo-toplu-data` olduğunu doğrula. Başka/rastgele
      isimli bir banka görürsen (ör. `destek-hatti-demo-musteri-xxxxxx`) — bu, ayrı bir test
      oturumunun (bu uygulamadan değil) artığı olabilir, sil.
- [ ] (opsiyonel) Hindsight Admin UI ayrı bir sekmede hazır, `destek-hatti-demo-toplu-data`
      bankası seçili ve Constellation görünümü açık — adım 7'ye hızlı geçiş için
- [ ] **Hindsight container'ı yeniden oluşturduysan (`docker compose down/up`), uygulamayı da
      yeniden başlat** — eski MCP bağlantısı yeni container'a bağlanamaz, tool çağrıları
      sessizce başarısız olur.
- [ ] **Sadece 5214 portunda TEK bir uygulama süreci çalıştığından emin ol** — Rider'dan
      başlatmadan önce `lsof -i :5214` ile kontrol et; eski bir `dotnet run` süreci aynı
      portu tutuyorsa Rider'ın kendi süreci bağlanamaz ve eski/güncel olmayan bir sürümle
      konuştuğunu fark etmeden test edebilirsin. (2026-08-27'de tam olarak bu yaşandı: kod
      değiştirildikten sonra eski süreç hâlâ ayaktaydı, yeni değişiklikler test edilmiyordu —
      port'u öldürüp `dotnet run`'ı yeniden başlatınca düzeldi.)

## Sorun giderme

**`curl http://localhost:8888/health` bağlanamıyor**
Docker Desktop açık mı, container ayakta mı kontrol et: `docker compose -f docker-compose.hindsight.yml logs --tail=50`

**Ajan çalışıyor ama retain/recall/reflect hiç tetiklenmiyor**
`ollama list` ile modelin indirildiğini, `docker-compose.hindsight.yml`'deki
`HINDSIGHT_API_LLM_MODEL` ile `applications/HindsightChatDemo/appsettings.json`'daki
`Ollama:Model`'in aynı olduğunu doğrula. Küçük modellerde ara sıra atlanabilir — aynı
soruyu tekrar sormak genelde yeterli.

**`reflect` "kayıt bulunamadı" diyor ama bilgi gerçekten hafızada var**
Bankada çok az kayıt varsa (1, hatta bazen 2) Hindsight'ın kendi sentez modeli güvenilir
sonuç üretmiyor — 2026-08-27'de doğrulandı. Reflect'ten önce en az iki farklı `retain`
yaptığından emin ol (bkz. yukarıdaki adım 3'ün notu).
