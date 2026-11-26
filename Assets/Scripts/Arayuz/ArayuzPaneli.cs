using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace AkilliAydinlatma
{
    // Simülasyonun UI Toolkit arayüzü (Assets/UI/Arayuz.uxml + Arayuz.uss): kontroller, durum (lambalar ve
    // PWM duty), enerji. Komutlar SimulasyonYoneticisi'nin LLM araçlarıyla aynı adlı metotlarına gider.
    // Değerler her karede simülasyondan okunur (arayüz kendi durum tutmaz; LUMA komut verince de güncel kalır).
    [RequireComponent(typeof(SimulasyonYoneticisi), typeof(UIDocument))]
    public class ArayuzPaneli : MonoBehaviour
    {
        SimulasyonYoneticisi sim;

        VisualElement panel;
        Button goster, geceGunduz, duraklat;
        Button[] olcekler;
        Slider hiz;
        Label ortam, simDurumu, hizDegeri, nesne, olculenHiz;
        Label gucAkilli, gucNormal, tasarrufAnlik, tasarrufToplam, whNormal, whAkilli;
        VisualElement cubukNormal, cubukAkilli;
        VisualElement[] lambaDolgu;
        Label[] lambaYuzde, lambaDuty;

        static readonly int[] OlcekDegerleri = { 1, 5, 10 };

        // UIDocument ağacı kendi OnEnable'ında (bizimkinden önce) kurar; etkinleştirilince yeniden kurulur
        void OnEnable()
        {
            sim = GetComponent<SimulasyonYoneticisi>();
            var kok = GetComponent<UIDocument>().rootVisualElement;
            if (kok == null || kok.Q("panel") == null)
            {
                Debug.LogError("[ArayuzPaneli] Arayüz yüklenemedi: UIDocument'te Arayuz.uxml yok mu?");
                enabled = false;
                return;
            }

            panel = kok.Q("panel");
            goster = kok.Q<Button>("goster");
            kok.Q<Button>("gizle").clicked += () => Gizle(true);
            goster.clicked += () => Gizle(false);

            kok.Q<Button>("gonder-yaya").clicked += () => Gonder(5);
            kok.Q<Button>("gonder-arac").clicked += () => Gonder(50);
            kok.Q<Button>("gonder-hizli").clicked += () => Gonder(90);
            hiz = kok.Q<Slider>("hiz");
            hizDegeri = kok.Q<Label>("hiz-degeri");
            // Kaydırıcı yoldaki nesnenin hızını da değiştirir (eşiği geçerse türü değişir: 6. kural)
            hiz.RegisterValueChangedCallback(e =>
            {
                float kmh = Mathf.Round(e.newValue);
                hizDegeri.text = $"{kmh:0} km/s";
                sim.SetObjectSpeed(kmh);
            });
            kok.Q<Button>("gonder").clicked += () => sim.Spawn(Mathf.Round(hiz.value));

            geceGunduz = kok.Q<Button>("gece-gunduz");
            geceGunduz.clicked += () => sim.SetNight(!sim.IsNight);
            duraklat = kok.Q<Button>("duraklat");
            duraklat.clicked += () => sim.SetPaused(!sim.Paused);
            olcekler = new Button[OlcekDegerleri.Length];
            for (int i = 0; i < OlcekDegerleri.Length; i++)
            {
                int k = OlcekDegerleri[i];
                olcekler[i] = kok.Q<Button>($"olcek-{k}");
                olcekler[i].clicked += () => sim.SetTimeScale(k);
            }
            kok.Q<Button>("sifirla").clicked += () => sim.ResetEnergy();

            ortam = kok.Q<Label>("ortam");
            simDurumu = kok.Q<Label>("sim-durumu");
            nesne = kok.Q<Label>("nesne");
            olculenHiz = kok.Q<Label>("olculen-hiz");
            gucAkilli = kok.Q<Label>("guc-akilli");
            gucNormal = kok.Q<Label>("guc-normal");
            tasarrufAnlik = kok.Q<Label>("tasarruf-anlik");
            tasarrufToplam = kok.Q<Label>("tasarruf-toplam");
            whNormal = kok.Q<Label>("wh-normal");
            whAkilli = kok.Q<Label>("wh-akilli");
            cubukNormal = kok.Q("cubuk-normal");
            cubukAkilli = kok.Q("cubuk-akilli");

            LambalariKur(kok.Q("lambalar"), KontrolMantigi.LampCount);
            Guncelle();
        }

        // Her lamba: ad, parlaklık çubuğu, yüzde ve PWM duty (0–255)
        void LambalariKur(VisualElement kap, int adet)
        {
            kap.Clear();
            lambaDolgu = new VisualElement[adet];
            lambaYuzde = new Label[adet];
            lambaDuty = new Label[adet];
            for (int i = 0; i < adet; i++)
            {
                var lamba = new VisualElement();
                lamba.AddToClassList("lamba");
                var ad = new Label($"L{i + 1}");
                ad.AddToClassList("lamba-ad");
                var cubuk = new VisualElement();
                cubuk.AddToClassList("lamba-cubuk");
                lambaDolgu[i] = new VisualElement();
                lambaDolgu[i].AddToClassList("lamba-dolgu");
                cubuk.Add(lambaDolgu[i]);
                lambaYuzde[i] = new Label();
                lambaYuzde[i].AddToClassList("lamba-yuzde");
                lambaDuty[i] = new Label();
                lambaDuty[i].AddToClassList("lamba-duty");
                lamba.Add(ad);
                lamba.Add(cubuk);
                lamba.Add(lambaYuzde[i]);
                lamba.Add(lambaDuty[i]);
                kap.Add(lamba);
            }
        }

        void Update() => Guncelle();

        void Guncelle()
        {
            if (sim == null || panel == null) return;
            var s = sim.State;

            ortam.text = s.IsNight ? "Gece" : "Gündüz";
            ortam.EnableInClassList("gece", s.IsNight);
            ortam.EnableInClassList("gunduz", !s.IsNight);
            simDurumu.text = $"{(sim.Paused ? "Duraklatıldı" : "Çalışıyor")} · {sim.TimeScale}x · " +
                             $"süre {SureYaz(sim.SimTimeSec)}";

            geceGunduz.text = s.IsNight ? "Gündüze geç" : "Geceye geç";
            duraklat.text = sim.Paused ? "Devam et" : "Duraklat";
            for (int i = 0; i < olcekler.Length; i++)
                olcekler[i].EnableInClassList("secili", sim.TimeScale == OlcekDegerleri[i]);

            nesne.text = NesneYaz(s.Object);
            olculenHiz.text = "Sensörle ölçülen hız: " +
                              (sim.OlculenHizKmh is double olc ? $"{SayiYaz(olc, "0.0")} km/s" : "—");

            for (int i = 0; i < lambaDolgu.Length && i < s.Lamps.Length; i++)
            {
                int yuzde = s.Lamps[i];
                lambaDolgu[i].style.height = Length.Percent(yuzde);
                lambaYuzde[i].text = $"%{yuzde}";
                lambaDuty[i].text = $"duty {KontrolMantigi.PercentToDuty(yuzde)}";
            }

            gucAkilli.text = $"{s.PowerW:0} W";
            gucNormal.text = $"{s.NormalPowerW:0} W";
            tasarrufAnlik.text = TasarrufYaz(s.PowerW, s.NormalPowerW, "0");
            tasarrufToplam.text = TasarrufYaz(sim.SmartWh, sim.NormalWh, "0.0");
            whNormal.text = EnerjiYaz(sim.NormalWh);
            whAkilli.text = EnerjiYaz(sim.SmartWh);
            cubukNormal.style.width = Length.Percent(sim.NormalWh > 0 ? 100 : 0);
            cubukAkilli.style.width = Length.Percent(sim.NormalWh > 0 ? (float)(sim.SmartWh / sim.NormalWh * 100) : 0);
        }

        void Gonder(float kmh)
        {
            hiz.SetValueWithoutNotify(kmh);
            hizDegeri.text = $"{kmh:0} km/s";
            sim.Spawn(kmh);
        }

        void Gizle(bool gizle)
        {
            panel.EnableInClassList("gizli", gizle);
            goster.EnableInClassList("gizli", !gizle);
        }

        // ---- Biçimlendirme (Türkçe ondalık virgül; web arayüzündeki EnergyPanel ile aynı kurallar) ----

        public static string SayiYaz(double deger, string bicim) =>
            deger.ToString(bicim, CultureInfo.InvariantCulture).Replace('.', ',');

        // 1000 Wh'ten itibaren kWh
        public static string EnerjiYaz(double wh) =>
            wh >= 1000 ? $"{SayiYaz(wh / 1000, "0.00")} kWh" : $"{SayiYaz(wh, "0.0")} Wh";

        // Normal sistem hiç tüketmiyorsa (gündüz) tasarruf yüzdesi anlamsızdır
        public static string TasarrufYaz(double akilli, double normal, string bicim) =>
            normal > 0 ? $"%{SayiYaz((1 - akilli / normal) * 100, bicim)}" : "—";

        public static string NesneYaz(TrackedObject? o) =>
            o is TrackedObject n
                ? $"{(n.Type == ObjectType.Car ? "Araç" : "Yaya")} · {SayiYaz(n.SpeedKmh, "0.#")} km/s · konum {SayiYaz(n.Position, "0.00")}"
                : "Yolda nesne yok";

        // dk:sn (1 saatten uzunsa sa:dk:sn)
        public static string SureYaz(double sn)
        {
            int toplam = (int)sn;
            int sa = toplam / 3600, dk = toplam / 60 % 60, s = toplam % 60;
            return sa > 0 ? $"{sa}:{dk:00}:{s:00}" : $"{dk}:{s:00}";
        }
    }
}
