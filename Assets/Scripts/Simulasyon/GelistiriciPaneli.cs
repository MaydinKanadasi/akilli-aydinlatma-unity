using System.Linq;
using UnityEngine;

namespace AkilliAydinlatma
{
    // GEÇİCİ geliştirici paneli (IMGUI). Simülasyonu Play modunda denemek için düğmeler ve anlık değerler.
    // Aşama 4'te UI Toolkit ile yapılacak asıl arayüzle değiştirilecek.
    [RequireComponent(typeof(SimulasyonYoneticisi))]
    public class GelistiriciPaneli : MonoBehaviour
    {
        SimulasyonYoneticisi sim;
        float hiz = 50f;
        GUIStyle baslik;

        void Awake() => sim = GetComponent<SimulasyonYoneticisi>();

        void OnGUI()
        {
            if (sim == null) return;
            // Panel, 720 piksel yüksekliğe göre tasarlandı; büyük ekranda orantılı büyür
            float olcek = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(olcek, olcek, 1f));
            baslik ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };

            var s = sim.State;
            GUILayout.BeginArea(new Rect(10, 10, 340, 700), GUI.skin.box);
            GUILayout.Label("Akıllı Aydınlatma – geliştirici paneli", baslik);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Yaya (5)")) Gonder(5);
            if (GUILayout.Button("Araç (50)")) Gonder(50);
            if (GUILayout.Button("Hızlı (90)")) Gonder(90);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Hız: {hiz:0} km/s", GUILayout.Width(95));
            float yeniHiz = Mathf.Round(GUILayout.HorizontalSlider(hiz, 1f, 120f));
            if (!Mathf.Approximately(yeniHiz, hiz)) { hiz = yeniHiz; sim.SetObjectSpeed(hiz); }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Bu hızla gönder")) sim.Spawn(hiz);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(sim.IsNight ? "Gündüze geç" : "Geceye geç")) sim.SetNight(!sim.IsNight);
            if (GUILayout.Button(sim.Paused ? "Devam et" : "Duraklat")) sim.SetPaused(!sim.Paused);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            foreach (int k in new[] { 1, 5, 10 })
            {
                bool secili = sim.TimeScale == k;
                if (GUILayout.Toggle(secili, $"{k}x", GUI.skin.button) && !secili) sim.SetTimeScale(k);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label(s.IsNight ? "Gece" : "Gündüz", baslik);
            if (s.Object is TrackedObject o)
                GUILayout.Label($"{(o.Type == ObjectType.Car ? "Araç" : "Yaya")} · {o.SpeedKmh:0.#} km/s · konum {o.Position:0.00}");
            else
                GUILayout.Label("Yolda nesne yok");
            GUILayout.Label(sim.OlculenHizKmh is double olc
                ? $"Sensörle ölçülen hız: {olc:0.0} km/s"
                : "Sensörle ölçülen hız: –");

            GUILayout.Label("Lambalar (%): " + string.Join("  ", s.Lamps));
            GUILayout.Label("PWM duty (0–255): " + string.Join("  ", s.Lamps.Select(KontrolMantigi.PercentToDuty)));

            GUILayout.Space(6);
            GUILayout.Label("Enerji", baslik);
            GUILayout.Label($"Anlık güç: akıllı {s.PowerW:0} W · normal {s.NormalPowerW:0} W");
            GUILayout.Label($"Biriken: akıllı {sim.SmartWh:0.00} Wh · normal {sim.NormalWh:0.00} Wh");
            double tasarruf = sim.NormalWh > 0 ? (1 - sim.SmartWh / sim.NormalWh) * 100 : 0;
            GUILayout.Label($"Tasarruf: %{tasarruf:0.0}");
            if (GUILayout.Button("Enerjiyi sıfırla")) sim.ResetEnergy();

            GUILayout.EndArea();
        }

        void Gonder(float kmh)
        {
            hiz = kmh;
            sim.Spawn(kmh);
        }
    }
}
