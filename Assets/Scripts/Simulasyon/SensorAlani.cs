using UnityEngine;

namespace AkilliAydinlatma
{
    // Yol üzerindeki bir algılama çizgisi (gerçek sistemde lamba direğindeki hareket/hız sensörü).
    // Nesne çizgiyi geçince kısa süre yeşil yanar. Algılama SimulasyonYoneticisi'nde süpürme (sweep)
    // kontrolüyle yapılır: 10x hızda hızlı bir araç bir karede onlarca metre ilerleyebildiği için
    // fizik tetikleyicisi (trigger) çizgiyi atlayabilir; iki kare arasındaki aralık kontrolü atlamaz.
    public class SensorAlani : MonoBehaviour
    {
        [SerializeField] int konum;       // lamba cinsinden konum (0 = giriş, 1..5 = lambalar)
        [SerializeField] Renderer gosterge;

        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly Color Aktif = new Color(0.2f, 1f, 0.4f) * 3f;
        const float YanmaSuresi = 0.4f; // gerçek zamanda saniye

        MaterialPropertyBlock mpb;
        float kalan;

        public int Konum => konum;

        public void Baglan(int konum, Renderer gosterge)
        {
            this.konum = konum;
            this.gosterge = gosterge;
        }

        public void Algila()
        {
            kalan = YanmaSuresi;
            Uygula(1f);
        }

        void Update()
        {
            if (kalan <= 0f) return;
            kalan -= Time.unscaledDeltaTime;
            Uygula(Mathf.Clamp01(kalan / YanmaSuresi));
        }

        void Uygula(float k)
        {
            if (gosterge == null) return;
            mpb ??= new MaterialPropertyBlock();
            gosterge.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionColor, Aktif * k);
            gosterge.SetPropertyBlock(mpb);
        }
    }
}
