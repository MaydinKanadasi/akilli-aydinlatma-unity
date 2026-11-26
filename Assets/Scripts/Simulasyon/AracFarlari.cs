using UnityEngine;

namespace AkilliAydinlatma
{
    // Aracın farları ve stop lambaları: gece yanar, gündüz söner (gerçek araçtaki gibi anında; ortamın
    // 3 sn'lik yumuşak geçişini beklemez). Farlar yola ışık düşüren iki Spot Light ve parlayan far camları;
    // stoplar yalnızca parlayan (emission) kırmızı camlar, ışık kaynağı değil.
    // SahneKurucu aracı kurarken bağlar; SimulasyonYoneticisi gece/gündüz değişince Ayarla'yı çağırır.
    public class AracFarlari : MonoBehaviour
    {
        [SerializeField] Light[] farlar;
        [SerializeField] Renderer[] farCamlari;
        [SerializeField] Renderer[] stopCamlari;
        [SerializeField] Color farIsimasi = Color.white;
        [SerializeField] Color stopIsimasi = Color.red;

        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock mpb;

        public bool Yanik { get; private set; }

        public void Baglan(Light[] farlar, Renderer[] farCamlari, Renderer[] stopCamlari, Color farIsimasi, Color stopIsimasi)
        {
            this.farlar = farlar;
            this.farCamlari = farCamlari;
            this.stopCamlari = stopCamlari;
            this.farIsimasi = farIsimasi;
            this.stopIsimasi = stopIsimasi;
        }

        public void Ayarla(bool gece)
        {
            Yanik = gece;
            if (farlar != null)
                foreach (var far in farlar)
                    if (far != null) far.enabled = gece;
            IsimaUygula(farCamlari, gece ? farIsimasi : Color.black);
            IsimaUygula(stopCamlari, gece ? stopIsimasi : Color.black);
        }

        void IsimaUygula(Renderer[] camlar, Color isima)
        {
            if (camlar == null) return;
            mpb ??= new MaterialPropertyBlock();
            foreach (var cam in camlar)
            {
                if (cam == null) continue;
                cam.GetPropertyBlock(mpb);
                mpb.SetColor(EmissionColor, isima);
                cam.SetPropertyBlock(mpb);
            }
        }
    }
}
