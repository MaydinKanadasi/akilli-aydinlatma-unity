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
//       -onizleme <cikti.png> [-gunduz] [-kameralar "ad=x,y,z,bakX,bakY,bakZ,fov;ad2=..."]
//       [-yaya <konum> [-adimpozu]] [-isik "siddet,menzil,aci,icOran"]
// -yaya: lambalar kurallardan (5 km/s yaya) hesaplanır; -isik: spot ayarı denemesi (sahne değişmez)
// -gunduz: gündüz ortamı, lambalar %0 (1. kural)
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
        bool gunduz = Array.IndexOf(Environment.GetCommandLineArgs(), "-gunduz") >= 0;
        var ortam = UnityEngine.Object.FindAnyObjectByType<OrtamGecisi>();
        if (ortam != null) ortam.Ayarla(gece: !gunduz, aninda: true);

        // Yaya gösteriliyorsa lamba yüzdeleri kurallardan (5 km/s yaya, o konumda) hesaplanır
        string yayaArg = Arguman("-yaya");
        int[] yuzdeler = yayaArg == null ? OrnekYuzdeler
            : KontrolMantigi.ComputeLampLevels(!gunduz, new TrackedObject(ObjectType.Pedestrian, 5,
                double.Parse(yayaArg, CultureInfo.InvariantCulture)));

        // -isik "siddet,menzil,aci,icOran": spot ışık ayarlarını sahneyi değiştirmeden denemek için
        string isikArg = Arguman("-isik");
        float[] isikAyari = isikArg == null ? null
            : Array.ConvertAll(isikArg.Split(','), s => float.Parse(s, CultureInfo.InvariantCulture));

        var lambalar = UnityEngine.Object.FindObjectsByType<LambaKontrol>();
        Array.Sort(lambalar, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        for (int i = 0; i < lambalar.Length && i < yuzdeler.Length; i++)
        {
            int yuzde = gunduz ? 0 : yuzdeler[i];
            lambalar[i].YuzdeUygula(yuzde);
            var isik = lambalar[i].GetComponentInChildren<Light>(true);
            if (isik != null && isikAyari != null)
            {
                isik.intensity = isikAyari[0] * yuzde / 100f;
                isik.range = isikAyari[1];
                isik.spotAngle = isikAyari[2];
                isik.innerSpotAngle = isikAyari[2] * isikAyari[3];
            }
            Debug.Log($"[SahneOnizleme] {lambalar[i].name} x={lambalar[i].transform.position.x} " +
                      $"%{yuzde} isik={(isik != null && isik.enabled ? isik.intensity.ToString("0.#") : "kapali")}");
        }

        // GameObject.Find pasif nesneleri bulmaz; araç ve yaya sahnede pasif durduğu için ebeveynden aranır.
        // -yaya <konum>: araç yerine yaya o konumda gösterilir; -adimpozu ile eklemler bir adım pozuna döner
        // (eklem noktalarının doğru yerde olduğunu görmek için; sahne kaydedilmez).
        var nesneler = GameObject.Find("Sokak/Nesneler");
        string yayaKonumu = yayaArg;
        var nesne = nesneler != null ? nesneler.transform.Find(yayaKonumu != null ? "Yaya" : "Arac") : null;
        if (nesne != null)
        {
            float konum = yayaKonumu != null ? float.Parse(yayaKonumu, CultureInfo.InvariantCulture) : OrnekAracKonumu;
            nesne.gameObject.SetActive(true);
            var p = nesne.position;
            nesne.position = new Vector3(konum * SahneKurucu.LambaAraligi, p.y, p.z);
            var farlar = nesne.GetComponent<AracFarlari>();
            if (farlar != null) farlar.Ayarla(gece: !gunduz);
            if (yayaKonumu != null && Array.IndexOf(Environment.GetCommandLineArgs(), "-adimpozu") >= 0)
                AdimPozu(nesne);
        }
        else Debug.LogWarning("[SahneOnizleme] Nesne bulunamadı");

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
        // Sahne batchmode'da yeni açıldığında ortam ışığı henüz hesaplanmamış olabiliyor ve ilk kare
        // soluk/mavimsi çıkıyor: önce ortamı güncelle, bir ısınma karesi çiz, sonra asıl kareyi çek.
        DynamicGI.UpdateEnvironment();
        // URP, hedef dokunun biçimiyle çizer: 8 bitlik (LDR) hedefte HDR kaybolur, emission 1'de kesilir ve
        // bloom çalışmaz. Play'deki gibi olması için HDR (half float, doğrusal) dokuya çizip sRGB'ye çeviriyoruz.
        var rt = new RenderTexture(Genislik, Yukseklik, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        kamera.targetTexture = rt;
        kamera.Render();
        kamera.Render();
        RenderTexture.active = rt;
        var hdr = new Texture2D(Genislik, Yukseklik, TextureFormat.RGBAHalf, false, true);
        hdr.ReadPixels(new Rect(0, 0, Genislik, Yukseklik), 0, 0);
        kamera.targetTexture = null;
        RenderTexture.active = null;

        var pikseller = hdr.GetPixels();
        for (int i = 0; i < pikseller.Length; i++)
        {
            var c = pikseller[i];
            pikseller[i] = new Color(Mathf.LinearToGammaSpace(Mathf.Clamp01(c.r)),
                                     Mathf.LinearToGammaSpace(Mathf.Clamp01(c.g)),
                                     Mathf.LinearToGammaSpace(Mathf.Clamp01(c.b)), 1f);
        }
        var tex = new Texture2D(Genislik, Yukseklik, TextureFormat.RGB24, false);
        tex.SetPixels(pikseller);
        tex.Apply();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(cikti)));
        File.WriteAllBytes(cikti, tex.EncodeToPNG());
        Debug.Log($"[SahneOnizleme] Görüntü yazıldı: {cikti}");
    }

    // Sol bacak önde, sağ bacak arkada; kollar ters yönde (Rotation.x, eksi = öne)
    static void AdimPozu(Transform yaya)
    {
        var acilar = new (string ad, float x)[]
        {
            ("Kalca/Bacak_Sol", -25f), ("Kalca/Bacak_Sol/Diz_Sol", 5f),
            ("Kalca/Bacak_Sag", 20f), ("Kalca/Bacak_Sag/Diz_Sag", 35f),
            ("Kalca/Govde/Omuz_Sol", 20f), ("Kalca/Govde/Omuz_Sol/Dirsek_Sol", -15f),
            ("Kalca/Govde/Omuz_Sag", -25f), ("Kalca/Govde/Omuz_Sag/Dirsek_Sag", -30f),
        };
        foreach (var (ad, x) in acilar)
        {
            var eklem = yaya.Find(ad);
            if (eklem != null) eklem.localRotation = Quaternion.Euler(x, 0f, 0f);
            else Debug.LogWarning($"[SahneOnizleme] Eklem bulunamadı: {ad}");
        }
    }

    static string Arguman(string ad)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == ad) return args[i + 1];
        return null;
    }
}
