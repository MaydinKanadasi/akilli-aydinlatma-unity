# Akıllı Sokak Aydınlatması – Unity Simülasyonu

ESP32 tabanlı akıllı sokak aydınlatma sisteminin Unity 6 (URP) ile yapılan 3B simülasyonu. Yoldaki yaya ve araçlara göre 5 lamba PWM ile kısılıp açılır; kontrol kuralları web arayüzüyle birebir aynıdır.

- **Web arayüzü:** [akilli-aydinlatma.vercel.app](https://akilli-aydinlatma.vercel.app) · [repo](https://github.com/MaydinKanadasi/akilli-aydinlatma)
- **Unity sürümü:** 6000.6.2f1, Universal Render Pipeline (URP)
- **Dersler:** Karmaşık Mühendislik Tasarımı (MOS2-4701), Bilgisayar Grafiklerinin Temelleri

## Açma

1. Unity Hub → **Add** → **Add project from disk** → bu klasörü seç.
2. Unity 6000.6.2f1 ile aç.

## Yol Haritası

- [ ] Sahne iskeleti (yol, kaldırım, 5 lamba, ışıklar, kamera)
- [ ] Kontrol mantığı (web ile birebir, EditMode testleriyle doğrulanır)
- [ ] Grafik: gölge, emission, bloom, gece/gündüz, anahtar kare animasyonları
- [ ] Arayüz ve LLM asistanı LUMA
- [ ] Unity ↔ web dijital ikiz (WebSocket)

Modeller yalnızca CC0 lisanslı kaynaklardan (Kenney, Quaternius) alınır.

## Ekip

| Görev | Üye |
|---|---|
| Web arayüzü ve Unity simülasyonu | Muhammet Aydın Kanadaşı ([@MaydinKanadasi](https://github.com/MaydinKanadasi)) |
