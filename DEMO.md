# Sunum Notları

Kurulum ve teknik detaylar için [README.md](README.md). Bu dosya sadece sunum gününe özel.

## Senaryo

Persona: bir çağrı merkezinin müşteri destek asistanı. Gösterilecek şey: Hindsight'ın üç
temel işlemi — `retain` (yaz), `recall` (tek bir kaydı hatırla), `reflect` (birden fazla
kaydı sentezleyip değerlendirme yap) — canlı olarak, farklı oturumlar arasında çalışıyor.

**Model notu:** varsayılan model `llama3.1:8b`. Bu model, testlerde `qwen2.5` ve
`llama3.2`'den çok daha güvenilir çıktı (retain/recall/reflect'in üçü de 3/3-4/4 tetiklendi,
Çince'ye kayma hiç görülmedi) — bkz. [CLAUDE.md](CLAUDE.md) için tam karşılaştırma. Tek dezavantajı: cevaplar
biraz yavaş gelebiliyor (bazen ~1 dakikaya kadar), sunum sırasında sabırlı ol, boşluğu
"Hindsight arka planda kontrol ediyor" diye doldurabilirsin.

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

1. Bir mesaj yaz: *"Merhaba, ben Ahmet Yılmaz. Sizinle iletişimde telefon yerine e-posta
   tercih ediyorum, bunu hesabıma not eder misiniz?"*
   → Agent `retain` çağırır, mesajın altında yeşil bir "🧠 Hindsight'a yazıldı" etiketi görünür.
2. Sol üstteki **"+ Yeni arama"** butonuna bas (temiz bir sayfa açılır, aynı bank_id kullanılır
   — yani "farklı bir temsilci" aynı müşteri geçmişine erişebiliyor).
3. Sor: *"Merhaba, ben yine Ahmet. Geçen hafta aramıştım da, benimle nasıl iletişime
   geçmemi tercih ettiğimi not almış mıydınız?"*
   → Agent `recall` çağırır, mesajın altında sarı bir "🧠 Hindsight'tan hatırlandı" etiketi
   görünür.
4. **Tekrar "+ Yeni arama" ile yeni bir oturum aç** (aynı oturumda sorma — model kendi
   sohbet geçmişinden cevap verip tool'u atlayabiliyor), sor: *"Merhaba, ben yine Ahmet.
   Yeni bir konu için aramadan önce, hesabımla ilgili bugüne kadar neler konuştuğumuzu
   genel olarak özetleyebilir misiniz?"*
   → Agent `reflect` çağırır, mesajın altında mor bir "🧠 Hindsight'ta değerlendirildi"
   etiketi görünür.
5. Sağ üstteki linkten Hindsight Admin UI'ı açıp (`http://localhost:9999`) kaydedilen hafızayı
   canlı olarak gösterebilirsin.
6. Sol menüdeki **"📖 Hafızayı REST'ten oku"** butonuna bas. Burada mimariyi anlat: agent,
   Microsoft Agent Framework + MCP üzerinden yazıyor (adım 1-4), bu buton ise aynı bankayı
   düz bir REST çağrısıyla, MCP ve LLM hiç devreye girmeden okuyor (`IHindsightRestClient`,
   `HindsightClient/` klasörü). İki entegrasyon yolunu yan yana göstermek için iyi bir an.

**İpucu:** Tek bir net, tercih/bilgi bildiren cümle kullan (ör. adım 1'deki gibi). Bir mesajda
birden fazla ayrı bilgi (ör. hem şikayet hem adres değişikliği) daha az güvenilir. Her
recall/reflect denemesini **yeni bir oturumda** ve **tek mesaj** olarak sor — aynı oturumda
art arda sorular, modelin gerçek bir hafıza sorgusu yapmadan kendi konuşma geçmişinden
cevap vermesine yol açabiliyor.

## Demo öncesi kontrol listesi

- [ ] `docker ps` → `hindsight` container'ı `Up` durumda
- [ ] `curl http://localhost:8888/health` → `"status":"healthy"`
- [ ] `curl http://localhost:11434/api/tags` → kullanılacak model listede
- [ ] `ollama ps` → `UNTIL` sütunu ~30 dakika gösteriyor (5 dakika değil). Değilse:
      `launchctl setenv OLLAMA_KEEP_ALIVE "30m"` çalıştır, Ollama'yı yeniden başlat.
      Bu, mesajlar arasında ara verdiğinde modelin bellekten atılıp yeniden yüklenmesini
      (ve o mesaja gereksiz bir gecikme eklemesini) engeller.
- [ ] `applications/HindsightChatDemo` içinde `dotnet run` çalıştı, konsolda hata yok
- [ ] `curl http://localhost:5214/api/health` → `"healthy":true`
- [ ] Tarayıcıda uygulama açık, koyu tema arayüz görünüyor
- [ ] Demo bankasını sıfırla, böylece ilk `retain` gerçekten ilk kayıt olur:
      `curl -X DELETE http://localhost:8888/v1/default/banks/destek-hatti-demo`
- [ ] (opsiyonel) Hindsight Admin UI (`http://localhost:9999`) ayrı bir sekmede hazır
- [ ] **Hindsight container'ı yeniden oluşturduysan (`docker compose down/up`), uygulamayı da
      yeniden başlat** — eski MCP bağlantısı yeni container'a bağlanamaz, tool çağrıları
      sessizce başarısız olur.
- [ ] **Sadece 5214 portunda TEK bir uygulama süreci çalıştığından emin ol** — Rider'dan
      başlatmadan önce `lsof -i :5214` ile kontrol et; eski bir `dotnet run` süreci aynı
      portu tutuyorsa Rider'ın kendi süreci bağlanamaz ve eski/güncel olmayan bir sürümle
      konuştuğunu fark etmeden test edebilirsin.

## Sorun giderme

**`curl http://localhost:8888/health` bağlanamıyor**
Docker Desktop açık mı, container ayakta mı kontrol et: `docker compose -f docker-compose.hindsight.yml logs --tail=50`

**Ajan çalışıyor ama retain/recall/reflect hiç tetiklenmiyor**
`ollama list` ile modelin indirildiğini, `docker-compose.hindsight.yml`'deki
`HINDSIGHT_API_LLM_MODEL` ile `applications/HindsightChatDemo/appsettings.json`'daki
`Ollama:Model`'in aynı olduğunu doğrula. Küçük modellerde ara sıra atlanabilir — aynı
soruyu tekrar sormak genelde yeterli.
