# Sunum Notları

Kurulum ve teknik detaylar için [README.md](README.md). Bu dosya sadece sunum gününe özel.

## Senaryo

Persona: bir çağrı merkezinin müşteri destek asistanı. Gösterilecek şey: bir müşterinin ilk
aramada anlattığı bir bilgiyi, sonraki aramada farklı bir "temsilci" (yeni oturum) ona tekrar
sordurmadan hatırlaması.

1. Bir mesaj yaz: *"Merhaba, ben Ahmet. Kargom 2 haftadır teslim edilmedi."*
   → Agent `retain` çağırır, mesajın altında yeşil bir "🧠 Hindsight'a yazıldı" etiketi görünür.
2. Sol üstteki **"+ Yeni arama"** butonuna bas (temiz bir sayfa açılır, aynı bank_id kullanılır
   — yani "farklı bir temsilci" aynı müşteri geçmişine erişebiliyor).
3. Sor: *"Merhaba, ben Ahmet, geçen hafta aramıştım. Sorunum ne durumda, hatırlıyor musunuz?"*
   → Agent `recall` çağırır, mesajın altında sarı bir "🧠 Hindsight'tan hatırlandı" etiketi
   görünür. İzleyiciye tam olarak hangi query ile Hindsight'a gidildiğini gösterir.
4. Sağ üstteki linkten Hindsight Admin UI'ı açıp (`http://localhost:9999`) kaydedilen hafızayı
   canlı olarak gösterebilirsin.

**İpucu:** Tek bir net bilgi içeren kısa cümleler kullan (ör. yukarıdaki gibi). Aynı mesajda
birden fazla ayrı bilgi (ör. hem şikayet hem adres değişikliği) bir arada gönderilirse
retain'in ikisini de yakalama olasılığı düşüyor — bkz. README "Known limitations".

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

**Ajan çalışıyor ama retain/recall hiç tetiklenmiyor**
`ollama list` ile modelin indirildiğini, `docker-compose.hindsight.yml`'deki
`HINDSIGHT_API_LLM_MODEL` ile `appsettings.json`'daki `Ollama:Model`'in aynı olduğunu
doğrula. Küçük modellerde ara sıra atlanabilir — aynı soruyu tekrar sormak genelde yeterli.
