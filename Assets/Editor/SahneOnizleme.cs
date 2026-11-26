using System;
using System.Globalization;
using System.IO;
using AkilliAydinlatma;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Sokak sahnesinin kamerasından PNG önizleme çeker (yalnızca batchmode'dan; editörde Play gerekmez).
// Gece, araç 50 km/s ile konum 3,43'te: lambalar 0 20 100 100 100 (Aşama 2'de Play'de görülen durum).
// Sahne kaydedilmez; yalnızca görüntü üretilir. Geliştirici paneli (IMGUI) görüntüde yoktur.
//
// Unity -batchmode -quit -projectPath . -executeMethod SahneOnizleme.KomutSatirindanCek
//       -onizleme <cikti.png> [-kameralar "ad=x,y,z,bakX,bakY,bakZ,fov;ad2=..."]
public static class SahneOnizleme
{
    const string SahneYolu = "Assets/Scenes/Sokak.unity";
    const int Genislik = 1920, Yukseklik = 1080;
    static readonly int[] OrnekYuzdeler = { 0, 20, 100, 100, 100 };
    const float OrnekAracKonumu = 3.43f;

    public static void KomutSatirindanCek()
    {
        string cikti = Arguman("-onizleme") ?? "onizleme.png";
        EditorSceneManager.OpenScene(SahneYolu, OpenSceneMode.Single);

        var lambalar = UnityEngine.Object.FindObjectsByType<LambaKontrol>(FindObjectsSortMode.None);
        Array.Sort(lambalar, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        for (int i = 0; i < lambalar.Length && i < OrnekYuzdeler.Length; i++)
        {
            lambalar[i].YuzdeUygula(OrnekYuzdeler[i]);
            var isik = lambalar[i].GetComponentInChildren<Light>(true);
            Debug.Log($"[SahneOnizleme] {lambalar[i].name} x={lambalar[i].transform.position.x} " +
                      $"%{OrnekYuzdeler[i]} isik={(isik != null && isik.enabled ? isik.intensity.ToString("0.#") : "kapali")}");
        }

        // GameObject.Find pasif nesneleri bulmaz; araç sahnede pasif durduğu için ebeveynden aranır
        var nesneler = GameObject.Find("Sokak/Nesneler");
        var arac = nesneler != null ? nesneler.transform.Find("Arac") : null;
        if (arac != null)
        {
            arac.gameObject.SetActive(true);
            var p = arac.position;
            arac.position = new Vector3(OrnekAracKonumu * SahneKurucu.LambaAraligi, p.y, p.z);
        }
        else Debug.LogWarning("[SahneOnizleme] Araç bulunamadı");

        var kamera = Camera.main;
        // -kameralar "ad=x,y,z,bakX,bakY,bakZ,fov;ad2=..." → her biri için <cikti>_<ad>.png
        string kameralar = Arguman("-kameralar");
        if (kameralar == null)
        {
            Cek(kamera, cikti);
            return;
        }
        foreach (var tanim in kameralar.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parca = tanim.Split('=');
            var d = Array.ConvertAll(parca[1].Split(','), s => float.Parse(s, CultureInfo.InvariantCulture));
            kamera.transform.position = new Vector3(d[0], d[1], d[2]);
            kamera.transform.LookAt(new Vector3(d[3], d[4], d[5]));
            kamera.fieldOfView = d[6];
            Cek(kamera, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(cikti)),
                $"{Path.GetFileNameWithoutExtension(cikti)}_{parca[0]}.png"));
        }
    }

    static void Cek(Camera kamera, string cikti)
    {
        var rt = new RenderTexture(Genislik, Yukseklik, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        kamera.targetTexture = rt;
        kamera.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(Genislik, Yukseklik, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Genislik, Yukseklik), 0, 0);
        tex.Apply();
        kamera.targetTexture = null;
        RenderTexture.active = null;

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(cikti)));
        File.WriteAllBytes(cikti, tex.EncodeToPNG());
        Debug.Log($"[SahneOnizleme] Görüntü yazıldı: {cikti}");
    }

    static string Arguman(string ad)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == ad) return args[i + 1];
        return null;
    }
}
