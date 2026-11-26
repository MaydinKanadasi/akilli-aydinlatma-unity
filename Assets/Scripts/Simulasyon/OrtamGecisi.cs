using UnityEngine;

namespace AkilliAydinlatma
{
    // Gece/gündüz görünümü: yönlü ışık (ay/güneş), ortam ışığı ve gökyüzü birkaç saniyede yumuşakça değişir.
    // Kontrol kuralları anında değişir (gündüz lambalar hemen %0); yumuşak olan yalnızca görünümdür.
    // Geçiş, 0 (gece) ile 1 (gündüz) arasında ilerleyen tek bir sayıyla (t) yapılır; her özellik iki
    // anahtar değer arasında bu sayıyla ara değerlenir (interpolation) ve SmoothStep ile yumuşatılır.
    public class OrtamGecisi : MonoBehaviour
    {
        [SerializeField] Light gokIsigi;    // yönlü ışık: gece ay, gündüz güneş
        [SerializeField] Material gokyuzu;  // Skybox/Procedural malzemesi (varlık; çalışırken kopyası değişir)
        [SerializeField] float gecisSuresi = 3f; // saniye

        struct Ortam
        {
            public Color IsikRengi;
            public float IsikSiddeti;
            public Vector3 IsikAcisi;   // Euler: X yükseklik (eğim), Y yön
            public Color OrtamIsigi;
            public float GokPozlama;   // gökyüzü parlaklığı
            public Color GokRengi;
            public float Atmosfer;     // atmosfer kalınlığı: gündüz mavi saçılma, gece az
        }

        static readonly Ortam Gece = new Ortam
        {
            IsikRengi = new Color(0.55f, 0.65f, 1f),   // soğuk mavi ay ışığı
            IsikSiddeti = 0.15f,
            IsikAcisi = new Vector3(35f, -40f, 0f),
            OrtamIsigi = new Color(0.05f, 0.06f, 0.1f),
            GokPozlama = 0.06f,
            GokRengi = new Color(0.25f, 0.3f, 0.6f),
            Atmosfer = 0.5f,
        };

        static readonly Ortam Gunduz = new Ortam
        {
            IsikRengi = new Color(1f, 0.96f, 0.88f),  // sıcak beyaz güneş
            IsikSiddeti = 1.2f,
            IsikAcisi = new Vector3(55f, -20f, 0f),
            OrtamIsigi = new Color(0.45f, 0.5f, 0.55f),
            GokPozlama = 1f,
            GokRengi = new Color(0.5f, 0.5f, 0.5f),
            Atmosfer = 1f,
        };

        static readonly int Pozlama = Shader.PropertyToID("_Exposure");
        static readonly int GokTonu = Shader.PropertyToID("_SkyTint");
        static readonly int AtmosferKalinligi = Shader.PropertyToID("_AtmosphereThickness");

        float t;      // şu anki durum: 0 gece, 1 gündüz
        float hedef;  // gidilen durum
        Material calisanGokyuzu;

        public bool GecisSuruyor => !Mathf.Approximately(t, hedef);

        // SahneKurucu sahneyi kurarken çağırır
        public void Baglan(Light gokIsigi, Material gokyuzu)
        {
            this.gokIsigi = gokIsigi;
            this.gokyuzu = gokyuzu;
        }

        // aninda: geçişsiz (sahne açılışı, önizleme); değilse gecisSuresi içinde yumuşak geçiş
        public void Ayarla(bool gece, bool aninda)
        {
            hedef = gece ? 0f : 1f;
            if (aninda)
            {
                t = hedef;
                Uygula();
            }
        }

        void Update()
        {
            if (!GecisSuruyor) return;
            // Gerçek zamanla ilerler: simülasyonun zaman çarpanı ve duraklatma geçişi etkilemez
            t = Mathf.MoveTowards(t, hedef, Time.unscaledDeltaTime / gecisSuresi);
            Uygula();
        }

        void Uygula()
        {
            float k = Mathf.SmoothStep(0f, 1f, t); // başta ve sonda yavaş, ortada hızlı

            if (gokIsigi != null)
            {
                gokIsigi.color = Color.Lerp(Gece.IsikRengi, Gunduz.IsikRengi, k);
                gokIsigi.intensity = Mathf.Lerp(Gece.IsikSiddeti, Gunduz.IsikSiddeti, k);
                gokIsigi.transform.rotation = Quaternion.Euler(Vector3.Lerp(Gece.IsikAcisi, Gunduz.IsikAcisi, k));
            }

            RenderSettings.ambientLight = Color.Lerp(Gece.OrtamIsigi, Gunduz.OrtamIsigi, k);

            if (gokyuzu != null)
            {
                // Varlık dosyası değişmesin diye çalışırken bir kopyası kullanılır
                if (calisanGokyuzu == null)
                {
                    calisanGokyuzu = new Material(gokyuzu) { name = gokyuzu.name + " (çalışma)" };
                    RenderSettings.skybox = calisanGokyuzu;
                }
                calisanGokyuzu.SetFloat(Pozlama, Mathf.Lerp(Gece.GokPozlama, Gunduz.GokPozlama, k));
                calisanGokyuzu.SetColor(GokTonu, Color.Lerp(Gece.GokRengi, Gunduz.GokRengi, k));
                calisanGokyuzu.SetFloat(AtmosferKalinligi, Mathf.Lerp(Gece.Atmosfer, Gunduz.Atmosfer, k));
            }
        }

        // SahneKurucu gökyüzü malzemesini ve ışığı gece değerleriyle kaydeder (editörde sahne gece görünür)
        public static void GeceDegerleriniYaz(Material gokyuzu, Light gokIsigi)
        {
            gokyuzu.SetFloat(Pozlama, Gece.GokPozlama);
            gokyuzu.SetColor(GokTonu, Gece.GokRengi);
            gokyuzu.SetFloat(AtmosferKalinligi, Gece.Atmosfer);
            gokIsigi.color = Gece.IsikRengi;
            gokIsigi.intensity = Gece.IsikSiddeti;
            gokIsigi.transform.rotation = Quaternion.Euler(Gece.IsikAcisi);
            RenderSettings.ambientLight = Gece.OrtamIsigi;
        }
    }
}
