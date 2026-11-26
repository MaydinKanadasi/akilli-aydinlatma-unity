using UnityEngine;

namespace AkilliAydinlatma
{
    // Bir sokak lambasının görünümü: PWM yüzdesine göre spot ışık şiddeti ve armatürün parlaması.
    // LED gücü PWM duty ile doğrusal kabul edildiği için ışık şiddeti de yüzdeyle doğrusal ölçeklenir.
    public class LambaKontrol : MonoBehaviour
    {
        [SerializeField] Light isik;
        [SerializeField] Renderer armatur;
        [SerializeField] float maksSiddet = 150f;       // %100'deki ışık şiddeti
        [SerializeField] Color maksIsima = Color.white; // %100'deki armatür parlaması (emission)

        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock mpb;

        public int Yuzde { get; private set; } = -1;

        // SahneKurucu sahneyi kurarken çağırır
        public void Baglan(Light isik, Renderer armatur, float maksSiddet, Color maksIsima)
        {
            this.isik = isik;
            this.armatur = armatur;
            this.maksSiddet = maksSiddet;
            this.maksIsima = maksIsima;
        }

        public void YuzdeUygula(int yuzde)
        {
            if (yuzde == Yuzde) return;
            Yuzde = yuzde;
            float k = yuzde / 100f;

            if (isik != null)
            {
                isik.enabled = yuzde > 0;
                isik.intensity = maksSiddet * k;
            }

            if (armatur != null)
            {
                mpb ??= new MaterialPropertyBlock();
                armatur.GetPropertyBlock(mpb);
                mpb.SetColor(EmissionColor, maksIsima * k);
                armatur.SetPropertyBlock(mpb);
            }
        }
    }
}
