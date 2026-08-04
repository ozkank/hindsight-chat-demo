# Sunum Notları

Kurulum ve teknik detaylar için [README.md](README.md). Bu dosya sadece sunum gününe özel.

## Senaryo

Persona: bir çağrı merkezinin müşteri destek asistanı. Gösterilecek şey: Hindsight'ın üç
temel işlemi — `retain` (yaz), `recall` (tek bir kaydı hatırla), `reflect` (birden fazla
kaydı sentezleyip değerlendirme yap) — canlı olarak, farklı oturumlar arasında çalışıyor.

1. Bir mesaj yaz: *"Merhaba, ben Ahmet. Sizinle iletişimde telefon yerine e-posta tercih
   ediyorum, lütfen bunu hesabıma not alın."*
   → Agent `retain` çağırır, mesajın altında yeşil bir "🧠 Hindsight'a yazıldı" etiketi görünür.
   **Test edilen en güvenilir açılış cümlesi budur (4/4 denemede tetiklendi) — canlı demoda
   bunu kullan, kargo-şikayeti tarzı cümleler test sırasında çok daha az güvenilirdi.**
2. Sol üstteki **"+ Yeni arama"** butonuna bas (temiz bir sayfa açılır, aynı bank_id kullanılır
   — yani "farklı bir temsilci" aynı müşteri geçmişine erişebiliyor).
3. Sor: *"Merhaba, ben Ahmet, geçen hafta aramıştım. İletişim tercihimi hatırlıyor musunuz?"*
   → Agent `recall` çağırır, mesajın altında sarı bir "🧠 Hindsight'tan hatırlandı" etiketi
   görünür. **Dikkat: recall test sırasında düşük bir tetiklenme oranı gösterdi (birkaç
   denemede bir çalıştı). Tetiklenmezse tek yapman gereken aynı soruyu tekrar sormak —
   genelde 2-3 denemede biri çalışıyor. Canlıda "biraz daha net sorayım" diyip tekrar
   deneyebilirsin.**
4. Aynı sohbette sor: *"Genel olarak bana nasıl bir öneride bulunursunuz?"*
   → Agent `reflect` çağırır, mesajın altında mor bir "🧠 Hindsight'ta değerlendirildi"
   etiketi görünür. Tool çağrısı güvenilir tetikleniyor, ama üretilen cevap metni bazen
   yabancı bir dile (Çince) kayabiliyor — bu bir model kalite sorunu, mekanizmanın kendisi
   çalışıyor. İzleyiciye etiketi ve query'yi vurgula, cevap metnine değil.
5. Sağ üstteki linkten Hindsight Admin UI'ı açıp (`http://localhost:9999`) kaydedilen hafızayı
   canlı olarak gösterebilirsin.

**İpucu:** Tek bir net, tercih/bilgi bildiren cümle kullan (ör. adım 1'deki gibi). Şikayet
tarzı cümleler ("X gelmedi", "Y sorun yaşadım") test sırasında retain'i çok daha az
güvenilir tetikledi — bkz. README "Known limitations".

## Demo öncesi kontrol listesi

- [ ] `docker ps` → `hindsight` container'ı `Up` durumda
- [ ] `curl http://localhost:8888/health` → `"status":"healthy"`
- [ ] `curl http://localhost:11434/api/tags` → kullanılacak model listede
- [ ] `dotnet run` çalıştı, konsolda hata yok
- [ ] `curl http://localhost:5214/api/health` → `"healthy":true`
- [ ] Tarayıcıda uygulama açık, koyu tema arayüz görünüyor
- [ ] (opsiyonel) Hindsight Admin UI (`http://localhost:9999`) ayrı bir sekmede hazır
- [ ] **Hindsight container'ı yeniden oluşturduysan (`docker compose down/up`), uygulamayı da
      yeniden başlat** — eski MCP bağlantısı yeni container'a bağlanamaz, tool çağrıları
      sessizce başarısız olur.

## Sorun giderme

**`curl http://localhost:8888/health` bağlanamıyor**
Docker Desktop açık mı, container ayakta mı kontrol et: `docker compose -f docker-compose.hindsight.yml logs --tail=50`

**Ajan çalışıyor ama retain/recall/reflect hiç tetiklenmiyor**
`ollama list` ile modelin indirildiğini, `docker-compose.hindsight.yml`'deki
`HINDSIGHT_API_LLM_MODEL` ile `appsettings.json`'daki `Ollama:Model`'in aynı olduğunu
doğrula. Küçük modellerde ara sıra atlanabilir — aynı soruyu tekrar sormak genelde yeterli.
