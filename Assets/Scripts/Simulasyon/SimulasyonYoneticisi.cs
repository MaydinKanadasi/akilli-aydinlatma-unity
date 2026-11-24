using UnityEngine;

namespace AkilliAydinlatma
{
    // Web'deki useMockSimulation.ts'in Unity karşılığı: nesneyi yürütür, enerjiyi biriktirir,
    // kontrol mantığının çıktısını lambalara uygular. Komutlar (Spawn, SetNight, SetPaused,
    // SetTimeScale, ResetEnergy) LLM asistanının araçlarıyla aynı adları taşır.
    public class SimulasyonYoneticisi : MonoBehaviour
    {
        [SerializeField] LambaKontrol[] lambalar;
        [SerializeField] SensorAlani[] sensorler;
        [SerializeField] Transform yaya;
        [SerializeField] Transform arac;
        [SerializeField] Light ayIsigi;
        [SerializeField] Camera kamera;

        // ---- Durum (web ile aynı başlangıç: gece, yol boş, 5x, çalışıyor) ----
        public bool IsNight { get; private set; } = true;
        public TrackedObject? Nesne { get; private set; }
        public bool Paused { get; private set; }
        public int TimeScale { get; private set; } = 5;
        public double SmartWh { get; private set; }   // akıllı sistemin biriken tüketimi
        public double NormalWh { get; private set; }  // aynı sürede normal sistemin tüketimi
        public double SimTimeSec { get; private set; }

        // Sensörlerden ölçülen hız: iki sensör arasındaki mesafe / geçen simülasyon süresi
        public double? OlculenHizKmh { get; private set; }
        int sonSensorKonumu = -1;
        double sonSensorZamani;

        public SystemState State => KontrolMantigi.ComputeState(IsNight, Nesne);

        public void Baglan(LambaKontrol[] lambalar, SensorAlani[] sensorler, Transform yaya, Transform arac,
                           Light ayIsigi, Camera kamera)
        {
            this.lambalar = lambalar;
            this.sensorler = sensorler;
            this.yaya = yaya;
            this.arac = arac;
            this.ayIsigi = ayIsigi;
            this.kamera = kamera;
        }

        void Start()
        {
            OrtamiUygula();
            GorunumuGuncelle();
        }

        void Update()
        {
            if (!Paused) Advance(Time.deltaTime * TimeScale);
            GorunumuGuncelle();
        }

        // ---- Komutlar ----

        // Nesne 1. lambanın gerisinden (konum 0) yola girer; tür hızdan belirlenir (Kural 6)
        public void Spawn(double speedKmh)
        {
            Nesne = new TrackedObject(KontrolMantigi.ClassifyBySpeed(speedKmh), speedKmh, 0);
            OlculenHizKmh = null;
            sonSensorKonumu = -1;
        }

        // Yoldaki nesnenin hızını değiştirir; tür de hıza göre yeniden belirlenir
        public void SetObjectSpeed(double speedKmh)
        {
            if (Nesne is TrackedObject o)
                Nesne = new TrackedObject(KontrolMantigi.ClassifyBySpeed(speedKmh), speedKmh, o.Position);
        }

        public void SetNight(bool isNight)
        {
            IsNight = isNight;
            OrtamiUygula();
        }

        public void SetPaused(bool paused) => Paused = paused;

        public void SetTimeScale(int scale) => TimeScale = scale >= 10 ? 10 : scale >= 5 ? 5 : 1;

        public void ResetEnergy()
        {
            SmartWh = 0;
            NormalWh = 0;
        }

        // ---- Simülasyon adımı (web'deki advance() ile aynı sıra) ----

        // dt saniye ilerletir: önce o anki durumun gücü hesaplanır, sonra nesne hareket eder
        public void Advance(double dt)
        {
            var now = State;

            if (Nesne is TrackedObject o)
            {
                double yeni = KontrolMantigi.NextPosition(o, dt);
                SensorleriKontrolEt(o.Position, yeni, dt);
                // Son lambayı geçip uzaklaşınca nesne yoldan çıkar
                Nesne = yeni > KontrolMantigi.ExitPosition ? (TrackedObject?)null
                                                           : new TrackedObject(o.Type, o.SpeedKmh, yeni);
            }

            SmartWh += now.PowerW * dt / 3600;
            NormalWh += now.NormalPowerW * dt / 3600;
            SimTimeSec += dt;
        }

        // Nesne bu adımda (eski, yeni] aralığında hangi sensör çizgilerini geçtiyse onları algılar
        void SensorleriKontrolEt(double eski, double yeni, double dt)
        {
            if (yeni <= eski) return;
            for (int k = 0; k <= KontrolMantigi.LampCount; k++)
            {
                if (!(eski < k && k <= yeni)) continue;

                // Çizginin adım içinde tam geçildiği an (doğrusal ara değer)
                double t = SimTimeSec + (k - eski) / (yeni - eski) * dt;
                if (sonSensorKonumu >= 0 && t > sonSensorZamani)
                {
                    double metre = (k - sonSensorKonumu) * ControllerConfig.Default.LampSpacingM;
                    OlculenHizKmh = metre / (t - sonSensorZamani) * 3.6;
                }
                sonSensorKonumu = k;
                sonSensorZamani = t;

                if (sensorler != null)
                    foreach (var s in sensorler)
                        if (s != null && s.Konum == k) s.Algila();
            }
        }

        // ---- Görünüm ----

        void GorunumuGuncelle()
        {
            var s = State;
            if (lambalar != null)
                for (int i = 0; i < lambalar.Length && i < s.Lamps.Length; i++)
                    if (lambalar[i] != null) lambalar[i].YuzdeUygula(s.Lamps[i]);

            NesneGoster(yaya, Nesne is TrackedObject y && y.Type == ObjectType.Pedestrian ? y : (TrackedObject?)null);
            NesneGoster(arac, Nesne is TrackedObject a && a.Type == ObjectType.Car ? a : (TrackedObject?)null);
        }

        static void NesneGoster(Transform t, TrackedObject? nesne)
        {
            if (t == null) return;
            bool gorunur = nesne.HasValue;
            if (t.gameObject.activeSelf != gorunur) t.gameObject.SetActive(gorunur);
            if (!gorunur) return;
            var p = t.position;
            // Dünya X = konum × lamba aralığı (1 birim = 1 m)
            t.position = new Vector3((float)(nesne.Value.Position * ControllerConfig.Default.LampSpacingM), p.y, p.z);
        }

        // Gece/gündüz ortamı (Aşama 3'te yumuşak geçiş ve gökyüzüyle değiştirilecek)
        void OrtamiUygula()
        {
            if (ayIsigi != null)
            {
                ayIsigi.color = IsNight ? new Color(0.55f, 0.65f, 1f) : new Color(1f, 0.96f, 0.88f);
                ayIsigi.intensity = IsNight ? 0.15f : 1.2f;
            }
            if (kamera != null)
                kamera.backgroundColor = IsNight ? new Color(0.02f, 0.03f, 0.08f) : new Color(0.55f, 0.75f, 0.95f);
            RenderSettings.ambientLight = IsNight ? new Color(0.05f, 0.06f, 0.1f) : new Color(0.45f, 0.5f, 0.55f);
        }
    }
}
