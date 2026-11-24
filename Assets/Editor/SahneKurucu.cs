using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// "Akıllı Aydınlatma → Sahneyi Kur" menü komutu.
// Yol, kaldırımlar, 5 lamba, ay ışığı ve kamerayı koddan kurar ve Assets/Scenes/Sokak.unity olarak kaydeder.
// Her çalıştırmada sahne sıfırdan yeniden oluşturulur; elle yapılan değişiklikler bu sahnede kalıcı olmaz.
//
// Ölçek: 1 birim = 1 m. Yol X ekseni boyunca uzanır.
// Konum (lamba cinsinden) p ise dünyadaki X = p × LambaAraligi. 0 = yola giriş, 6 = çıkış (180 m).
public static class SahneKurucu
{
    // Web arayüzündeki controller.ts ile aynı değerler
    public const int LambaSayisi = 5;
    public const float LambaAraligi = 30f; // m

    // Yol ölçüleri (m)
    const float YolUzunlugu = (LambaSayisi + 1) * LambaAraligi; // 180 m
    const float YolGenisligi = 7f; // iki şerit
    const float KaldirimGenisligi = 3f;
    const float KaldirimYuksekligi = 0.15f;

    // Lamba ölçüleri (m)
    const float DirekYuksekligi = 8f;
    const float DirekZ = YolGenisligi / 2f + 1f; // direk, yolun +Z tarafındaki kaldırımda
    const float ArmaturZ = 1.5f; // armatür, kolla yolun üstüne uzanır

    // Spot ışık ayarları (ilk değerler; Aşama 3'te ince ayar yapılacak)
    const float IsikSiddeti = 80f;
    const float IsikMenzili = 25f;
    const float IsikAcisi = 110f;

    const string SahneYolu = "Assets/Scenes/Sokak.unity";
    const string MalzemeKlasoru = "Assets/Materials/Sahne";

    [MenuItem("Akıllı Aydınlatma/Sahneyi Kur")]
    public static void MenudenKur()
    {
        // Açık sahnede kaydedilmemiş değişiklik varsa önce kullanıcıya sor
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Kur();
        Debug.Log($"[SahneKurucu] Sahne kuruldu ve kaydedildi: {SahneYolu}");
    }

    // Terminalden: Unity -batchmode -quit -projectPath ... -executeMethod SahneKurucu.KomutSatirindanKur
    public static void KomutSatirindanKur()
    {
        Kur();
        Debug.Log($"[SahneKurucu] Batchmode: sahne kuruldu ve kaydedildi: {SahneYolu}");
    }

    static void Kur()
    {
        var malzeme = MalzemeleriHazirla();

        // Her seferinde boş bir sahneden başla: eski sahne tamamen silinmiş olur
        var sahne = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var kok = new GameObject("Sokak");

        ZeminKur(kok.transform, malzeme);
        YolKur(kok.transform, malzeme);
        LambalariKur(kok.transform, malzeme);
        AyIsigiKur(kok.transform);
        KameraKur(kok.transform);
        GeceOrtamiAyarla();

        Directory.CreateDirectory(Path.GetDirectoryName(SahneYolu));
        EditorSceneManager.SaveScene(sahne, SahneYolu);
        SahneyiDerlemeListesineEkle();
    }

    // ---------- Sahne parçaları ----------

    static void ZeminKur(Transform kok, Dictionary<string, Material> m)
    {
        var zemin = Kutu("Zemin", kok, m["Zemin"]);
        zemin.transform.position = new Vector3(YolUzunlugu / 2f, -0.05f, 0f);
        zemin.transform.localScale = new Vector3(YolUzunlugu + 120f, 0.1f, 120f);
    }

    static void YolKur(Transform kok, Dictionary<string, Material> m)
    {
        var yol = new GameObject("Yol").transform;
        yol.SetParent(kok, false);

        var asfalt = Kutu("Asfalt", yol, m["Asfalt"]);
        asfalt.transform.position = new Vector3(YolUzunlugu / 2f, 0.01f, 0f);
        asfalt.transform.localScale = new Vector3(YolUzunlugu, 0.02f, YolGenisligi);

        // Kaldırımlar: yolun iki yanında, biraz yüksek
        float kaldirimZ = YolGenisligi / 2f + KaldirimGenisligi / 2f;
        foreach (var (ad, z) in new[] { ("Kaldirim_Sol", kaldirimZ), ("Kaldirim_Sag", -kaldirimZ) })
        {
            var k = Kutu(ad, yol, m["Kaldirim"]);
            k.transform.position = new Vector3(YolUzunlugu / 2f, KaldirimYuksekligi / 2f, z);
            k.transform.localScale = new Vector3(YolUzunlugu, KaldirimYuksekligi, KaldirimGenisligi);
        }

        // Orta şerit çizgisi: 3 m çizgi, 3 m boşluk
        var cizgiler = new GameObject("SeritCizgileri").transform;
        cizgiler.SetParent(yol, false);
        const float cizgiBoyu = 3f, bosluk = 3f;
        int i = 0;
        for (float x = cizgiBoyu / 2f; x < YolUzunlugu; x += cizgiBoyu + bosluk, i++)
        {
            var c = Kutu($"Cizgi_{i + 1}", cizgiler, m["Cizgi"]);
            c.transform.position = new Vector3(x, 0.025f, 0f);
            c.transform.localScale = new Vector3(cizgiBoyu, 0.01f, 0.15f);
        }

        // Giriş ve çıkış çizgileri (konum 0 ve 6)
        foreach (var (ad, x) in new[] { ("GirisCizgisi", 0f), ("CikisCizgisi", YolUzunlugu) })
        {
            var c = Kutu(ad, yol, m["Cizgi"]);
            c.transform.position = new Vector3(x, 0.025f, 0f);
            c.transform.localScale = new Vector3(0.4f, 0.01f, YolGenisligi);
        }
    }

    static void LambalariKur(Transform kok, Dictionary<string, Material> m)
    {
        var lambalar = new GameObject("Lambalar").transform;
        lambalar.SetParent(kok, false);

        for (int i = 1; i <= LambaSayisi; i++)
        {
            float x = i * LambaAraligi; // 30, 60, 90, 120, 150 m
            var lamba = new GameObject($"Lamba_{i}").transform;
            lamba.SetParent(lambalar, false);
            lamba.position = new Vector3(x, 0f, 0f);

            var direk = Silindir("Direk", lamba, m["Direk"]);
            direk.transform.position = new Vector3(x, DirekYuksekligi / 2f, DirekZ);
            direk.transform.localScale = new Vector3(0.2f, DirekYuksekligi / 2f, 0.2f); // silindir 2 birim boyunda

            // Direğin tepesinden yola uzanan kol
            float kolBoyu = DirekZ - ArmaturZ;
            var kol = Kutu("Kol", lamba, m["Direk"]);
            kol.transform.position = new Vector3(x, DirekYuksekligi, (DirekZ + ArmaturZ) / 2f);
            kol.transform.localScale = new Vector3(0.12f, 0.12f, kolBoyu);

            var armatur = Kutu("Armatur", lamba, m["Armatur"]);
            armatur.transform.position = new Vector3(x, DirekYuksekligi - 0.1f, ArmaturZ);
            armatur.transform.localScale = new Vector3(0.6f, 0.15f, 0.9f);

            // Aşağı bakan spot ışık; şiddeti Aşama 2'de PWM yüzdesine bağlanacak
            var isikGo = new GameObject("Isik");
            isikGo.transform.SetParent(lamba, false);
            isikGo.transform.position = new Vector3(x, DirekYuksekligi - 0.25f, ArmaturZ);
            isikGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var isik = isikGo.AddComponent<Light>();
            isik.type = LightType.Spot;
            isik.color = new Color(1f, 0.85f, 0.6f); // sıcak beyaz
            isik.intensity = IsikSiddeti;
            isik.range = IsikMenzili;
            isik.spotAngle = IsikAcisi;
            isik.innerSpotAngle = IsikAcisi * 0.6f;
            isik.shadows = LightShadows.Soft;
            isik.lightmapBakeType = LightmapBakeType.Realtime;
        }
    }

    static void AyIsigiKur(Transform kok)
    {
        var ay = new GameObject("AyIsigi");
        ay.transform.SetParent(kok, false);
        ay.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
        var isik = ay.AddComponent<Light>();
        isik.type = LightType.Directional;
        isik.color = new Color(0.55f, 0.65f, 1f); // soğuk mavi ay ışığı
        isik.intensity = 0.15f;
        isik.shadows = LightShadows.Soft;
        RenderSettings.sun = isik;
    }

    static void KameraKur(Transform kok)
    {
        var kameraGo = new GameObject("Kamera") { tag = "MainCamera" };
        kameraGo.transform.SetParent(kok, false);
        // Yolun ortasına (90 m) çapraz yukarıdan bakar; 5 lambanın hepsi görünür
        kameraGo.transform.position = new Vector3(YolUzunlugu / 2f, 45f, -80f);
        kameraGo.transform.LookAt(new Vector3(YolUzunlugu / 2f, 0f, 5f));
        var kamera = kameraGo.AddComponent<Camera>();
        kamera.fieldOfView = 55f;
        kamera.nearClipPlane = 0.3f;
        kamera.farClipPlane = 600f;
        kamera.clearFlags = CameraClearFlags.SolidColor;
        kamera.backgroundColor = new Color(0.02f, 0.03f, 0.08f); // gece göğü
        kameraGo.AddComponent<AudioListener>();
    }

    static void GeceOrtamiAyarla()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.06f, 0.1f);
    }

    // ---------- Yardımcılar ----------

    static GameObject Kutu(string ad, Transform ebeveyn, Material malzeme) =>
        Ilkel(PrimitiveType.Cube, ad, ebeveyn, malzeme);

    static GameObject Silindir(string ad, Transform ebeveyn, Material malzeme) =>
        Ilkel(PrimitiveType.Cylinder, ad, ebeveyn, malzeme);

    static GameObject Ilkel(PrimitiveType tur, string ad, Transform ebeveyn, Material malzeme)
    {
        var go = GameObject.CreatePrimitive(tur);
        go.name = ad;
        go.transform.SetParent(ebeveyn, false);
        // Dekor nesnelerinde çarpışma gerekmiyor; sensör alanları Aşama 2'de ayrıca eklenecek
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = malzeme;
        return go;
    }

    // Malzemeler varlık (asset) olarak kaydedilir; yeniden kurulumda aynı dosyalar güncellenir
    static Dictionary<string, Material> MalzemeleriHazirla()
    {
        Directory.CreateDirectory(MalzemeKlasoru);
        return new Dictionary<string, Material>
        {
            ["Zemin"] = Malzeme("Zemin", new Color(0.12f, 0.16f, 0.1f)),
            ["Asfalt"] = Malzeme("Asfalt", new Color(0.18f, 0.18f, 0.2f), puruzsuzluk: 0.2f),
            ["Kaldirim"] = Malzeme("Kaldirim", new Color(0.45f, 0.44f, 0.42f)),
            ["Cizgi"] = Malzeme("Cizgi", new Color(0.9f, 0.9f, 0.85f)),
            ["Direk"] = Malzeme("Direk", new Color(0.25f, 0.27f, 0.3f), metalik: 0.6f, puruzsuzluk: 0.5f),
            ["Armatur"] = Malzeme("Armatur", new Color(0.9f, 0.85f, 0.7f), isima: new Color(1f, 0.85f, 0.6f) * 2f),
        };
    }

    static Material Malzeme(string ad, Color renk, float metalik = 0f, float puruzsuzluk = 0.1f, Color? isima = null)
    {
        string yol = $"{MalzemeKlasoru}/{ad}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(yol);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[SahneKurucu] URP Lit shader bulunamadı. Proje URP ile mi açıldı?");
                shader = Shader.Find("Standard");
            }
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, yol);
        }

        mat.SetColor("_BaseColor", renk);
        mat.SetFloat("_Metallic", metalik);
        mat.SetFloat("_Smoothness", puruzsuzluk);
        if (isima.HasValue)
        {
            // Emission: armatürün kendi kendine parlaması (bloom Aşama 3'te eklenecek)
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", isima.Value);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void SahneyiDerlemeListesineEkle()
    {
        AssetDatabase.SaveAssets();
        // Derlemeye yalnızca Sokak sahnesi girer
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SahneYolu, true) };
    }
}
