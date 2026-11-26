using System.Linq;
using UnityEngine;

namespace AkilliAydinlatma
{
    // GEÇİCİ geliştirici paneli (IMGUI). Simülasyonu Play modunda denemek için düğmeler ve anlık değerler.
    // Aşama 4'te UI Toolkit ile yapılacak asıl arayüzle değiştirilecek.
    // Sahneyi kapatmasın diye ekranın üstünde (gökyüzü tarafında) yatay ince bir şerittir:
    // kontroller | durum | enerji. "Gizle" ile küçük bir düğmeye iner.
    [RequireComponent(typeof(SimulasyonYoneticisi))]
    public class GelistiriciPaneli : MonoBehaviour
    {
        const float KontrolGenisligi = 340f, DurumGenisligi = 270f, EnerjiGenisligi = 280f;

        SimulasyonYoneticisi sim;
        float hiz = 50f;
        bool gizli;
        GUIStyle baslik;

        void Awake() => sim = GetComponent<SimulasyonYoneticisi>();

        void OnGUI()
        {
            if (sim == null) return;
            // Panel, 720 piksel yüksekliğe göre tasarlandı; büyük ekranda orantılı büyür
            float olcek = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(olcek, olcek, 1f));
            baslik ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };

            float ekranGenisligi = Screen.width / olcek;
            GUILayout.BeginArea(new Rect(10, 10, ekranGenisligi - 20, 300));

            if (gizli)
            {
                if (GUILayout.Button("▼ Paneli göster", GUILayout.Width(130))) gizli = false;
                GUILayout.EndArea();
                return;
            }

            // Kutu içeriğe göre boyutlanır (yükseklik ve genişlik)
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(false));

            GUILayout.BeginHorizontal();
            GUILayout.Label("Akıllı Aydınlatma – geliştirici paneli", baslik);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("▲ Gizle", GUILayout.Width(70))) gizli = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            KontrolSutunu();
            GUILayout.Space(14);
            DurumSutunu();
            GUILayout.Space(14);
            EnerjiSutunu();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        void KontrolSutunu()
        {
            GUILayout.BeginVertical(GUILayout.Width(KontrolGenisligi));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Yaya (5)")) Gonder(5);
            if (GUILayout.Button("Araç (50)")) Gonder(50);
            if (GUILayout.Button("Hızlı (90)")) Gonder(90);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Hız: {hiz:0} km/s", GUILayout.Width(85));
            float yeniHiz = Mathf.Round(GUILayout.HorizontalSlider(hiz, 1f, 120f, GUILayout.Width(120)));
            if (!Mathf.Approximately(yeniHiz, hiz)) { hiz = yeniHiz; sim.SetObjectSpeed(hiz); }
            if (GUILayout.Button("Bu hızla gönder")) sim.Spawn(hiz);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(sim.IsNight ? "Gündüze geç" : "Geceye geç")) sim.SetNight(!sim.IsNight);
            if (GUILayout.Button(sim.Paused ? "Devam et" : "Duraklat")) sim.SetPaused(!sim.Paused);
            foreach (int k in new[] { 1, 5, 10 })
            {
                bool secili = sim.TimeScale == k;
                if (GUILayout.Toggle(secili, $"{k}x", GUI.skin.button, GUILayout.Width(38)) && !secili)
                    sim.SetTimeScale(k);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        void DurumSutunu()
        {
            var s = sim.State;
            GUILayout.BeginVertical(GUILayout.Width(DurumGenisligi));
            string nesne = s.Object is TrackedObject o
                ? $"{(o.Type == ObjectType.Car ? "Araç" : "Yaya")} · {o.SpeedKmh:0.#} km/s · konum {o.Position:0.00}"
                : "Yolda nesne yok";
            GUILayout.Label($"{(s.IsNight ? "Gece" : "Gündüz")} · {nesne}", baslik);
            GUILayout.Label(sim.OlculenHizKmh is double olc
                ? $"Sensörle ölçülen hız: {olc:0.0} km/s"
                : "Sensörle ölçülen hız: –");
            GUILayout.Label("Lambalar (%): " + string.Join("  ", s.Lamps));
            GUILayout.Label("PWM duty (0–255): " + string.Join("  ", s.Lamps.Select(KontrolMantigi.PercentToDuty)));
            GUILayout.EndVertical();
        }

        void EnerjiSutunu()
        {
            var s = sim.State;
            GUILayout.BeginVertical(GUILayout.Width(EnerjiGenisligi));
            double tasarruf = sim.NormalWh > 0 ? (1 - sim.SmartWh / sim.NormalWh) * 100 : 0;
            GUILayout.Label($"Enerji · tasarruf %{tasarruf:0.0}", baslik);
            GUILayout.Label($"Anlık güç: akıllı {s.PowerW:0} W · normal {s.NormalPowerW:0} W");
            GUILayout.Label($"Biriken: akıllı {sim.SmartWh:0.00} Wh · normal {sim.NormalWh:0.00} Wh");
            if (GUILayout.Button("Enerjiyi sıfırla", GUILayout.Width(130))) sim.ResetEnergy();
            GUILayout.EndVertical();
        }

        void Gonder(float kmh)
        {
            hiz = kmh;
            sim.Spawn(kmh);
        }
    }
}
