# Akıllı Sokak Aydınlatması – Unity Simülasyonu ve LLM Asistanı Planı

Nov 24, 2026 · @Aydın · Sürüm 2 (Claude Code incelemesiyle güncellendi)

## Bu doküman ne işe yarar

Bu doküman, projenin Unity simülasyonu ve LLM asistanı aşamasını sıfırdan başlayan yeni bir sohbete ya da Claude Code'a devretmek için hazırlandı. Bağlamı, alınan kararları, takvimi ve çalışma tercihlerini tek yerde toplar; yeni oturum buradan okuyarak kaldığı yerden devam edebilir.

**Nasıl kullanılır:**

- **Claude Code (önerilen):** Bu dosyanın bir kopyası Unity reposunun kökünde `CLAUDE.md` olarak duruyor; Claude Code her oturumun başında onu kendiliğinden okur. Plan değişince önce `CLAUDE.md` güncellenir, bu Masaüstü kopyası ondan yenilenir.
- **Yeni claude.ai sohbeti:** Dosyayı sohbete ekle ve "Bu plana göre kaldığımız yerden devam edelim" de.

Claude Code bu aşama için daha uygun: C# dosyalarını doğrudan yazıp düzenler, git, npm ve Unity'yi terminalden (batchmode) kendisi çalıştırır, Unity'nin hata kayıtlarını okur. Kopyala-yapıştır ve çıktı taşıma ihtiyacı ortadan kalkar. Unity editöründe görsel kontrol ve el işi (ör. anahtar kare animasyonu) kullanıcıda kalır.

## Proje özeti ve kontrol kuralları

ESP32 tabanlı, yaya ve araç algılayıp 5 sokak lambasını PWM ile kısarak enerji tasarrufu sağlayan bir sistem. Ekip projesi; kullanıcının görevi web arayüzü ve Unity simülasyonu. ESP32/donanım tarafını kimin yapacağı henüz belli değil.

Hocanın taslağındaki maddeler: LED + LED sürücü (MOSFET), PWM ile dimmerleme, hız algılama ve ortam ışığı (gündüz/gece), hıza göre önden yakılacak lamba sayısı (aralık / hız), görüntü işleme ile yaya/araç ayırımı, anlık hesaplama, arayüz tasarımı.

**Hocanın onayladığı 6 kontrol kuralı:**

1. Gündüz tüm lambalar kapalıdır (%0).
2. Gece yolda nesne yoksa tüm lambalar kapalıdır (%0).
3. Nesnenin bulunduğu lamba %100 yanar.
4. Önündeki lambalar %40 ve %20 olarak kademeli yanar. Araçlarda, aracın `lookaheadSec` saniye içinde ulaşacağı lambalar önceden %100 yakılır: `ceil(hız_m/s × lookaheadSec / lambaAralığı)`, en fazla 4. Yayada bu sayı 0'dır.
5. Hemen arkadaki lamba %20'ye iner, daha gerideki lambalar kapanır.
6. Yaya/araç ayırımı hız eşiğiyle yapılır (7 km/s üstü araç).

"Bulunduğu lamba" = `Math.round(konum)`; JavaScript'te `Math.round(1.5) = 2`. C# port'unda aynı davranış için `Math.Floor(konum + 0.5)` kullanılmalı (`Mathf.Round` ve `Math.Round` 0.5'i çift sayıya yuvarlar).

| Parametre | Değer |
| --- | --- |
| Lamba sayısı | 5 |
| Lamba aralığı | 30 m |
| Araç için önden aydınlatma süresi | 3 sn |
| Yaya/araç hız eşiği | 7 km/s |
| Lamba gücü (%100) | 100 W |
| PWM (ESP32 LEDC) | 5 kHz, periyot 200 µs, 8 bit (0–255) |

Enerji: LED gücü duty ile doğrusal kabul edilir; akıllı güç = Σ (parlaklık/100 × 100 W). "Normal sistem" gece tüm lambaları %100 yakar (500 W), gündüz 0 W.

## Mevcut durum: web arayüzü

Web arayüzü tamamlandı ve `v0.3-demo` sürümüyle yayında. Hocaya `v0.2-demo` inceleme için gönderilmişti; `v0.3` yalnızca geniş ekranda iki sütunlu düzen ve README düzeltmesi ekledi.

|  |  |
| --- | --- |
| Canlı site | [akilli-aydinlatma.vercel.app](https://akilli-aydinlatma.vercel.app) (Vercel, `main`'den otomatik yayın) |
| Repo | [MaydinKanadasi/akilli-aydinlatma](https://github.com/MaydinKanadasi/akilli-aydinlatma) (açık), yerelde `~/Projects/akilli-aydinlatma` |
| Teknoloji | React 19 + Vite 8 + TypeScript 6, düz CSS, grafikler kendi SVG kodumuz (kütüphane yok) |
| Sürümler | `v0.1-demo` (temel demo), `v0.2-demo` (görsel revizyon), `v0.3-demo` (iki sütunlu düzen) |
| Son PR'lar | #11 iki sütunlu düzen, #12 README örnek veri düzeltmesi, #13 develop → main (v0.3) |

**Özellikler:** gerçekçi yol sahnesi (gece/gündüz, kaldırım, ışık konisi ve halkası, yürüyen yaya, farlı araç), hız kaydırıcısı ve hazır butonlar, duraklat / tek adım / 1x-5x-10x, yola tıklayıp sürükleme, enerji paneli, son ~30 sn'lik zaman grafikleri, osiloskop görünümlü PWM paneli, geniş ekranda iki sütunlu düzen.

**Dosya yapısı (`src/`):** `logic/types.ts` (ortak tipler, `HistorySample`), `logic/controller.ts` (kurallar, enerji, `PWM_FREQ_HZ`, `PWM_BITS`), `hooks/useMockSimulation.ts` (simülasyon, `advance()` fonksiyonu, geçmiş kaydı; `spawn`, `setNight`, `setPaused`, `setTimeScale`, `resetEnergy`), `components/` (`RoadView`, `EnergyPanel`, `ChartsPanel`, `LineChart`, `PwmPanel`), `App.tsx`.

**Ortak veri formatı** (Unity ve ESP32 de aynısını gönderecek). Örnek: gece, 40 km/s araç, konum 2.3:

```json
{
  "source": "mock",
  "isNight": true,
  "object": { "type": "car", "speedKmh": 40, "position": 2.3 },
  "lamps": [20, 100, 100, 100, 40],
  "powerW": 360,
  "normalPowerW": 500
}
```

Hesap: bulunduğu lamba `round(2.3) = 2`; 40 km/s = 11,1 m/s, `ceil(11,1 × 3 / 30) = 2` lamba önden %100 (3. ve 4.), sonra 5. lamba %40; 1. lamba hemen arkada %20. Güç 20 + 100 + 100 + 100 + 40 = 360 W. *Dokümandaki örnek değerler her zaman koddan hesaplanarak yazılır.*

`source`: `mock` | `unity` | `esp32`. `type`: `pedestrian` | `car`. `position`: lamba cinsinden konum (1 = 1. lambanın hizası, 0 = yola giriş, 6'yı geçince nesne yoldan çıkar; yol 0–180 m). Aynı anda yolda tek nesne var.

## Hedefler ve gereksinimler

Unity simülasyonu iki derste sunulacak ve en geç 15 Aralık 2026'da hazır olacak; finaller Ocak başında. Karmaşık Mühendislik proje teslimi Ocak 2027'nin ilk haftası. Sunum kullanıcının kendi Mac'inde yapılır (yalnızca macOS derlemesi gerekir).

|  | Karmaşık Mühendislik Tasarımı (MOS2-4701) | Bilgisayar Grafiklerinin Temelleri |
| --- | --- | --- |
| Hoca | Dr. Öğr. Üyesi Arzu Türksoy (Elektrik-Elektronik) | — |
| Vurgu | Sistem davranışı: 6 kural, sensörler, enerji tasarrufu, Unity ↔ web canlı veri (dijital ikiz) | Anahtar kare (keyframe) animasyonu, ışıklandırma ve gölgelendirme |
| LLM | Şart | Şart |

Grafik hocası sınavlarda yorum ağırlıklı sorular soruyor ve kod cevaplarını sözde kod (pseudocode) olarak bekliyor; sunumda kontrol mantığının sözde kodunu göstermek faydalı olur.

**LLM şartı:** İki hoca da projeye bir LLM entegre edilmesini istiyor; ne yapacağı bize bırakıldı. Seçilen: **doğal dille kontrol + enerji analizi** yapan tek bir sohbet asistanı. Koşullar: ücretli API yok; Mac'i yoracak ağır yerel model (Ollama) yok.

Aynı çalışmanın iki derste sunulacağı iki hocaya da bildirilmeli (yapıldı mı, açık konu).

### 15 Aralık önceliği

| Zorunlu (15 Aralık) | İsteğe bağlı (zaman kalırsa) |
| --- | --- |
| Sahne, 5 lamba, 6 kural birebir | LUMA'nın tam tasarımı: eklemli kollar, işaret etme, göğüs PWM göstergesi |
| Gölge, emission, bloom, gece/gündüz geçişi | Hazır (CC0) yaya ve araç modelleri |
| Elle anahtar kareli yaya yürüyüşü, kamera turu | LUMA'nın yüz ifadeleri |
| LLM sohbeti (web + Unity), en az basit LUMA (basit şekillerden drone, salınma ve pervane anahtar kareleri) | |
| macOS derlemesi, yedek video | |

## Alınan kararlar

**Unity**

- **Ayrı repo:** [MaydinKanadasi/akilli-aydinlatma-unity](https://github.com/MaydinKanadasi/akilli-aydinlatma-unity) (açık), yerelde `~/Projects/akilli-aydinlatma-unity`, Unity `.gitignore` ile; aynı `main` / `develop` / `feature/*` + PR akışı.
- **Proje tipi:** Unity **6000.6.2f1**, Universal 3D (URP) boş şablonu (`com.unity.template.urp-blank`). URP; gölge, emission ve bloom gibi grafik dersi konularını göstermek için gerekli. Kurulu modüller: Mac ve WebGL derleme desteği.
- **Lisans:** Personal lisans etkin. Batchmode komutları çalışırken Unity Hub'da oturum açık olmalı; değilse Unity çıkış kodu 198 ile kapanır ("Access token is unavailable"). Çözüm: Hub'ı açıp profil simgesinden oturum açmak.
- **Sahne koddan kurulur:** Bir editör betiği Unity menüsüne "Akıllı Aydınlatma → Sahneyi Kur" komutu ekler; yol, kaldırımlar, 5 lamba, ışıklar ve kamera tek tıkla oluşur. Aynı metot Claude Code tarafından batchmode'da da çalıştırılır. Sahne bozulursa yeniden kurulur.
- **Kontrol mantığı:** `controller.ts`'in birebir C# karşılığı; aynı kurallar, parametreler ve veri formatı. Hesaplar `double` ve `System.Math` ile yapılır (JS sayılarıyla aynı sonuç için; `float` sınır değerlerde farklı yuvarlayabilir).
- **Birebir doğrulama:** Web reposunda küçük bir betik `controller.ts`'ten test vektörleri (konum × hız × gece/gündüz → lambalar, güç) üretip JSON'a yazar; Unity'de EditMode testi (Unity Test Framework) C# sonuçlarını bu JSON ile karşılaştırır. Aşama 2'nin kabul kriteri bu testin geçmesidir.
- **Unity otomasyonu (batchmode):** Claude Code Unity'yi terminalden çalıştırır: sahne kurulumu `-executeMethod`, testler `-runTests -testPlatform EditMode`, derleme `-buildTarget OSXUniversal`. Bu komutlar çalışırken proje Unity editöründe **açık olmamalı** (proje kilitlenir). Kayıtlar `-logFile` ile alınır; editör kaydı `~/Library/Logs/Unity/Editor.log`.
- **Unity arayüzü:** UI Toolkit (UXML/USS metin dosyaları, Claude doğrudan yazar). Türkçe karakterler (ğ, ş, ı, İ, ö, ü, ç) için uygun font Aşama 4'te kontrol edilir.
- **Modeller:** Önce basit şekillerle (küp, silindir) mantık kurulur, sonra **CC0 lisanslı** ücretsiz modellerle (Kenney, Quaternius) değiştirilir. Asset Store kullanılmaz: lisansı modellerin açık bir repoda paylaşılmasına izin vermiyor. Git LFS kullanılmaz; tek bir dosya 50 MB'ı geçerse yeniden düşünülür.
- **Grafik dersi vurguları:** Yaya yürüyüşü Animation penceresinde anahtar karelerle elle yapılır (hazır animasyon değil). Sunum açılışı için anahtar karelerle bir kamera turu hazırlanır. Lamba spot ışığının şiddeti PWM yüzdesine bağlanır; gölgeler, emission, bloom ve gece/gündüz renk geçişi eklenir.
- **Dijital ikiz (yalnızca Karmaşık dersi için, 15 Aralık sonrası):** Unity → Node.js WebSocket sunucusu → web arayüzü (`source: "unity"`). Vercel kalıcı WebSocket sunucusu barındıramaz; ikiz demosu yerelde çalışır (web `npm run dev` + yerel Node sunucusu, Unity aynı Mac'te).

**LLM asistanı**

- **Tek asistan, iki arayüz:** Web arayüzünde sağ altta sohbet paneli; Unity'de kendi sohbet paneli. İkisi de aynı sunucu fonksiyonuna istek atar.
- **Sunucu:** Web reposunda Vercel sunucu fonksiyonu `/api/chat` (`api/chat.ts`). API anahtarı yalnızca Vercel ortam değişkeninde (`GROQ_API_KEY`) ve yerelde git'e girmeyen `.env.local`'de durur (`*.local` zaten `.gitignore`'da); repo açık olduğu için asla koda yazılmaz. Kötüye kullanıma karşı basit istek sınırı (IP başına dakikalık) ve mesaj uzunluğu / geçmiş uzunluğu sınırı eklenir.
- **Yerel test:** `npm run dev` (Vite) `/api` fonksiyonlarını çalıştırmaz; yerelde `npx vercel dev` kullanılır (`npx vercel link` ve `npx vercel env pull .env.local` ile). `api/` klasörü mevcut `tsc -b` kapsamında değil; ayrı bir `tsconfig` ile tip kontrolüne eklenir.
- **İşleyiş:** Arayüz kullanıcının mesajıyla birlikte o anki durumu gönderir: ortak veri formatındaki anlık durum + geçmiş özeti (`smartWh`, `normalWh`, tasarruf %, simülasyon süresi sn, zaman çarpanı, duraklatma). LLM ya cevap yazar ya da tool calling ile komut döndürür; arayüz komutu uygular. Cevaplar Türkçe.
- **Araçlar (tools):** `spawn_object(speedKmh)`, `set_night(isNight)`, `set_paused(paused)`, `set_time_scale(1|5|10)`, `reset_energy()`. Web'deki karşılıkları `useMockSimulation`'da hazır (`spawn`, `setNight`, `setPaused`, `setTimeScale`, `resetEnergy`).
- **Unity istemcisi:** `UnityWebRequest` ile canlı siteye (`https://akilli-aydinlatma.vercel.app/api/chat`) istek atar; masaüstü derlemesinde CORS gerekmez. Anahtar Unity'de de tutulmaz.
- **Model:** Ücretsiz katman. Birincil: Groq; kayıtta kredi kartı istemiyor, kullanım dakika/gün başına sınırlarla kısıtlı, ödeme bilgisi girilmedikçe ücret yok ([kaynak](https://community.groq.com/t/is-there-a-free-tier-and-what-are-its-limits/790)). OpenAI uyumlu API. Yedek: Google Gemini'nin uygun ülkelerde ücretsiz katmanı ([kaynak](https://ai.google.dev/gemini-api/docs/rate-limits)). Servis tek ayarla (`LLM_PROVIDER`) değiştirilebilir yazılır. Model adı, tool calling desteği ve güncel sınırlar uygulama anında kontrol edilir; ücretsiz katman koşulları değişebilir.
- **Sunum güvencesi:** İnternet ya da servis sorunu ihtimaline karşı asistanın çalıştığı anları gösteren yedek video kaydedilir.

**Robot karakteri – LUMA:** Asistan yalnızca Unity'de, yolun üstünde uçan insansı bir robot olarak görünür (adı ışık birimi lümenden). Web'deki sohbet paneli sade kalır, robot görseli yoktur.

- **Kafa:** Fener başlığı biçiminde; tepesinde ampul gibi parlayan bir anten.
- **Yüz:** Ekranda iki amber LED göz ve bir ağız. İfade emission ile değişir: düşünürken kısık, komutu uygularken gülen.
- **Göğüs:** 5 lambanın anlık parlaklığını gösteren küçük bir PWM göstergesi.
- **Kollar:** Eklemli iki kol; el sallama ve ilgili lambayı işaret etme hareketleri anahtar karelerle.
- **Uçuş:** Kafanın iki yanında pervaneler (dönüş ve havada salınma anahtar karelerle); altından yola düşen bir ışık konisi.
- **Davranış:** Asistana komut verilince ilgili noktaya uçar; örneğin araç gönderilince yolun başına gider.
- **Renkler:** Lacivert gövde, amber ışıklar. Özgün bir tasarım; başka markaların maskotlarına benzetilmez.

*Not: Bu tasarım bir başlangıç konseptidir. 15 Aralık için minimum sürüm basit şekillerden yapılmış uçan bir drone'dur (salınma, pervane, ışık konisi); kalan ayrıntılar zaman kaldıkça ve hocaların geri bildirimine göre eklenir.*

## Takvim

| Tarih | İş |
| --- | --- |
| 24–29 Kasım | Aşama 0 (kurulum) + Aşama 1 (sahne iskeleti) |
| 30 Kasım – 6 Aralık | Aşama 2 (Unity kontrol mantığı) ‖ Aşama 2b (web LLM asistanı), paralel |
| 7–12 Aralık | Aşama 3 (grafik, anahtar kareler) + Aşama 4 başlangıcı (Unity arayüzü, LUMA sohbeti) |
| 13–15 Aralık | macOS derlemesi, yedek video, iki ders için sunum provası — **15 Aralık hedef** |
| 16 Aralık – 3 Ocak | Aşama 5 dijital ikiz (Karmaşık dersi) + tampon |

Kullanıcının müsaitliği değişken olduğu için ikizden sonra tampon bırakıldı. Bir hafta kayarsa önce "İsteğe bağlı" sütunundaki işler düşer.

## Aşamalar ve kabul kriterleri

Her aşama bir ya da birkaç `feature/*` dalı ile yapılır ve PR ile `develop`'a birleştirilir. Bir aşama, kabul kriteri sağlanınca biter.

**Aşama 0 – Kurulum**

- [x] Unity Hub ve Unity 6 kurulumunu kontrol et → 6000.6.2f1, Mac + WebGL modülleri
- [x] Unity Hub'da oturum aç (Personal lisans zaten etkindi, 23 Eylül 2026)
- [x] `~/Projects/akilli-aydinlatma-unity` konumunda Universal 3D (URP) projesi oluştur (batchmode; URP 17.6.0, Input System, Test Framework hazır)
- [x] Unity `.gitignore` ile git'i başlat, GitHub'da `akilli-aydinlatma-unity` reposunu aç (açık), `develop` dalını varsayılan yap
- [ ] Groq hesabı aç, API anahtarı al (**kullanıcı yapar**; anahtar hiçbir sohbete ya da dosyaya yapıştırılmaz, Aşama 2b'de Vercel'e kullanıcı girer)

Kabul: Boş URP projesi açılıyor, repo GitHub'da.

**Aşama 1 – Sahne iskeleti**

- [x] Editör betiği: "Akıllı Aydınlatma → Sahneyi Kur" menü komutu (batchmode'da da çalışır) → `Assets/Editor/SahneKurucu.cs`
- [x] Ölçek 1 birim = 1 m; 5 lamba 30 m arayla (30, 60, 90, 120, 150 m), yol 0–180 m, iki yanda kaldırım
- [x] Her lambada direk, armatür ve bir Spot Light; yönlü ışık (güneş/ay) ve yola bakan kamera

Kabul: Boş sahnede menüden tek tıkla yol ve lambalar oluşuyor; tekrar çalıştırınca eskisini silip yeniden kuruyor. **Sağlandı** (24 Kasım; menüden ve batchmode'dan denendi, tekrar kurulumda nesne sayısı aynı: 67).

Notlar:
- Sahne `Assets/Scenes/Sokak.unity` olarak kaydedilir ve derleme listesindeki tek sahnedir. Malzemeler `Assets/Materials/Sahne/` altındadır. Yol X ekseni boyunca uzanır: dünya X = konum × 30 m. Direkler +Z kaldırımında.
- Sahne her kurulumda sıfırdan oluşturulur: bu sahnede elle yapılan değişiklikler kalıcı olmaz, kalıcı olması gerekenler `SahneKurucu.cs`'e yazılır. Her kurulumda sahne dosyasındaki iç kimlikler değiştiği için git'te büyük ama anlamsız bir fark görünür; normaldir.

**Aşama 2 – Kontrol mantığı**

- [x] `controller.ts`'in C# karşılığı (kurallar, `ClassifyBySpeed`, enerji hesabı; `double` ile) → `Assets/Scripts/Mantik/KontrolMantigi.cs` (`AkilliAydinlatma` assembly'si, UnityEngine'e bağlı değil)
- [x] Web reposunda test vektörü betiği (`npm run -s test-vektorleri`, 2877 durum), Unity'de EditMode testi (`Assets/Tests/EditMode/`) → 5/5 geçti, 24 Kasım. Kontrol mantığı değişirse iki taraf birlikte güncellenir ve JSON yeniden üretilir.
- [ ] Simülasyon yöneticisi: nesne gönderme, hız, duraklatma, zaman çarpanı
- [ ] Lambalar arasına sensör alanları (trigger); yaya/araç basit şekillerle hareket ediyor
- [ ] Spot ışık şiddeti = PWM yüzdesi

Kabul: EditMode testi geçiyor: aynı konum ve hızda lamba yüzdeleri ve güç web arayüzüyle birebir aynı.

**Aşama 2b – LLM asistanı (web)**

- [ ] `api/chat.ts` Vercel fonksiyonu, Groq bağlantısı, tool tanımları, istek sınırı, `api/` için tip kontrolü
- [ ] Web'de sohbet paneli; tool çağrılarını simülasyona uygulama
- [ ] Yerelde `npx vercel dev` ile test, Vercel'de `GROQ_API_KEY` tanımlama (kullanıcı), yayın

Kabul: "Gece yap ve 70 km/s hızla bir araç gönder" komutu uygulanıyor; "Şu ana kadar ne kadar tasarruf ettik?" sorusuna güncel veriyle cevap geliyor.

**Aşama 3 – Grafik**

- [ ] Gölgeler, armatürde emission, URP Volume ile bloom
- [ ] `PC_RPAsset` içinde ek ışık gölge alanını (Additional Lights Shadow Atlas) 4096'ya çıkar: 5 spot gölgesi 2048'e sığmıyor, Unity "Reduced additional punctual light shadows resolution" uyarısı veriyor
- [ ] Spot ışık şiddeti (şu an 80) ve kamera açısı ince ayarı: şu an ışıklar biraz sönük, yol ekranda ince bir şerit
- [ ] Gece/gündüz geçişi: yönlü ışık rengi ve şiddeti, gökyüzü
- [ ] Yaya yürüyüşü ve LUMA drone'unun salınma/pervane hareketi: Animation penceresinde anahtar karelerle (LUMA önce basit şekillerden)
- [ ] CC0 yaya ve araç modelleri (Kenney / Quaternius); araç farları (Spot Light)

Kabul: Grafik dersinin anahtar kare ve ışıklandırma konuları sahnede tek tek gösterilebiliyor.

**Aşama 4 – Unity arayüzü ve sunum**

- [ ] Enerji paneli ve kontroller (gönder, hız, duraklat, gece/gündüz), UI Toolkit, Türkçe font
- [ ] LUMA sohbet paneli (komutta ilgili noktaya uçuş, konuşurken ışık) → aynı `/api/chat`
- [ ] Anahtar karelerle kamera turu
- [ ] macOS derlemesi (batchmode), yedek video, iki ders için ayrı sunum akışı, kontrol mantığının sözde kodu

Kabul: Simülasyon editör açmadan kendi başına sunulabiliyor.

**Aşama 5 – Dijital ikiz (Karmaşık dersi)**

- [ ] Node.js WebSocket sunucusu (yerel), Unity istemcisi, web'de kaynak seçimi (`mock` / `unity`)

Kabul: Unity'de giden araç, yerelde çalışan web panelinde canlı görünüyor.

## Çalışma şekli ve tercihler

Kullanıcının Unity bilgisi yok; iş büyük ölçüde "vibecoding" ile ilerler. Kodu asistan yazar ve terminalden çalıştırır; kullanıcı editörde görsel kontrol yapıp sonucu bildirir.

**Tercihler (her ortamda)**

- Her seferinde tek adım ver; sonucu gelmeden sonrakine geçme.
- Kullanıcıdan bir şey istendiğinde açıkça belirt: hangi uygulama (Unity editörü, Unity Hub, tarayıcı, Mac Terminal), nereye tıklanacak, ne görülmesi beklenir.
- Emin olmadığın her konuda varsayma, sor.
- Kontroller zincirle çalıştırılır: web'de `npx tsc -b && npx eslint src && npm run dev`.
- Git: `main` = sunulabilir sürümler (etiketli: `v0.x-demo`), `develop` = günlük geliştirme ve GitHub'da varsayılan dal, her özellik `feature/<ad>` dalında. Birleştirme `gh pr create` + `gh pr merge --merge --delete-branch`; `develop` → `main` PR'ında `--delete-branch` kullanılmaz. Repoda `fetch.prune` açık.
- Kullanıcı bazen soruyu cevaplamadan çıktı gönderir; açık kalan soruyu kısaca yeniden sor (seçmeli sorular iyi çalışıyor).

**Yalnızca Claude Code'da**

- Dosyaları Claude kendisi yazar ve düzenler; komutları kendisi çalıştırır.
- Unity batchmode komutlarından önce kullanıcıya projenin editörde kapalı olup olmadığı sorulur.

**Yalnızca claude.ai sohbetinde**

- Dosyalar editörde kopyala-yapıştır yerine terminalden yazılır (`cat > dosya << 'EOF'`). Değişikliklerde dosyanın tam hali yeniden yazdırılır. İçinde kod bloğu olan içerik gönderilirken dış blok dört ters tırnakla (````) açılır, yoksa sohbet ekranı bloğu böler.
- Her adımda hangi klasör ve hangi terminal (VS Code terminali / Mac Terminal) olduğu ve beklenen çıktı yazılır.
- Bir adımın commit'i, hız için bir sonraki adımın komut bloğunun başına konabilir.

**Makine ve araçlar**

|  |  |
| --- | --- |
| Bilgisayar | MacBook Air M4, 16 GB RAM, yaklaşık 59 GB boş alan |
| Araçlar | Node.js v24, VS Code (`code` komutu tanımlı), GitHub CLI (`gh`, MaydinKanadasi hesabıyla oturum açık), Vercel hesabı (Vercel CLI kurulu değil, `npx vercel` ile kullanılır), Git LFS yok |
| Tarayıcı | Safari (SVG favicon'u önbellekte tutuyor), Chrome |
| Unity | Unity Hub + Unity 6000.6.2f1 (`/Applications/Unity/Hub/Editor/6000.6.2f1`), Mac + WebGL derleme modülleri |

## Açık konular ve sıradaki adım

**Açık konular**

- [x] ~~Mac'teki Unity sürümü doğrulanacak~~ → 6000.6.2f1
- [ ] İki hocaya aynı çalışmanın iki derste sunulacağı bildirildi mi?
- [ ] Arzu Hoca'nın web arayüzü demosuna geri bildirimi bekleniyor; gelen düzeltmeler web reposunda ayrı `feature/*` dallarında yapılır.
- [ ] ESP32/donanım tarafını kimin yapacağı belli değil; kullanıcı gerekirse devralabilir.
- [ ] README'deki ekip tablosuna diğer üyeler eklenecek.
- [ ] Groq hesabı ve API anahtarı (kullanıcı).

**Sıradaki adım:** Aşama 2'nin kalanı: simülasyon yöneticisi (nesne gönderme, hız, duraklatma, zaman çarpanı), spot ışık şiddetinin PWM yüzdesine bağlanması, sensör alanları. Groq hesabı Aşama 2b'den önce açılmalı.
