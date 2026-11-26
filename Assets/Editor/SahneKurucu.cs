using System.Collections.Generic;
using System.IO;
using AkilliAydinlatma;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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

    // Spot ışık ayarları. Şiddet %100'deki değerdir; LambaKontrol PWM yüzdesiyle doğrusal ölçekler.
    // Işık 7,75 m yükseklikte; ters kare düşüşüyle yere ulaşan aydınlık şiddet / yükseklik² kadardır.
    // 26 Kasım: 110° / 25 m / 150'de iki lamba ortasındaki yaya (karşı kaldırım) %100 lambaya dikeyden
    // ~65° açıda ve ~18 m uzakta kalıyor, koninin dışında olduğu için hiç ışık almıyordu. Geniş koni ve
    // uzun menzil ("D" ayarı, kullanıcı Play'de M ile karşılaştırıp seçti): ışık havuzları birleşir, yaya
    // lambalar arasında da aydınlık kalır. Denenen ara ayar "M": 220 / 40 m / 150° / iç oran 0,7
    // (havuzlar daha belirgin, lamba ortasında yaya daha loş).
    const float IsikSiddeti = 250f;
    const float IsikMenzili = 45f;
    const float IsikAcisi = 165f;
    const float IsikIcOrani = 0.85f; // iç koni / dış koni: iç konide tam şiddet, kenara doğru yumuşak düşüş // iç koni / dış koni: iç konide tam şiddet, kenara doğru yumuşak düşüş
    // %100'deki armatür parlaması (emission). HDR: 1'den büyük değerler bloom eşiğini (1) aşar ve
    // armatürün çevresine ışık taşar. PWM ile doğrusal ölçeklenir: %20'de 1 (hale yok denecek kadar),
    // %100'de 5 (belirgin hale). 20 denendi: haleler ışık havuzlarını örtüyordu.
    static readonly Color ArmaturIsimasi = new Color(1f, 0.85f, 0.6f) * 5f;

    // Son işlem (post-processing): URP Global Volume ile bloom ve tonemapping
    const string VolumeProfiliYolu = "Assets/Settings/SokakVolumeProfili.asset";
    const float BloomEsigi = 1f;     // bu parlaklığın üstündeki pikseller taşar
    const float BloomSiddeti = 0.8f;
    const float BloomYayilimi = 0.65f; // taşmanın ne kadar geniş yayılacağı (0–1)
    const float PozlamaEV = 1f; // ACES sahneyi koyulaştırır; +1 EV görüntüyü iki kat aydınlatır

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
        var lambalar = LambalariKur(kok.transform, malzeme);
        var sensorler = SensorleriKur(kok.transform, malzeme);
        var (yaya, arac) = NesneleriKur(kok.transform, malzeme);
        var ay = AyIsigiKur(kok.transform);
        KameraKur(kok.transform);
        var gokyuzu = GeceOrtamiAyarla(ay);
        GorselEfektleriKur(kok.transform);

        // Simülasyon yöneticisi, gece/gündüz geçişi ve geçici geliştirici paneli
        var simGo = new GameObject("Simulasyon");
        simGo.transform.SetParent(kok.transform, false);
        var ortam = simGo.AddComponent<OrtamGecisi>();
        ortam.Baglan(ay, gokyuzu);
        simGo.AddComponent<SimulasyonYoneticisi>().Baglan(lambalar, sensorler, yaya, arac, ortam);
        simGo.AddComponent<GelistiriciPaneli>();

        Directory.CreateDirectory(Path.GetDirectoryName(SahneYolu));
        EditorSceneManager.SaveScene(sahne, SahneYolu);
        SahneyiDerlemeListesineEkle();
    }

    // ---------- Sahne parçaları ----------

    static void ZeminKur(Transform kok, Dictionary<string, Material> m)
    {
        var zemin = Kutu("Zemin", kok, m["Zemin"]);
        zemin.transform.position = new Vector3(YolUzunlugu / 2f, -0.05f, 0f);
        // Kamera 48° aşağı baktığı için kadrajın üstü ~170 m ileriyi görür: zemin kenarı görünmesin diye geniş
        zemin.transform.localScale = new Vector3(600f, 0.1f, 600f);
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

    static LambaKontrol[] LambalariKur(Transform kok, Dictionary<string, Material> m)
    {
        var lambalar = new GameObject("Lambalar").transform;
        lambalar.SetParent(kok, false);
        var kontroller = new LambaKontrol[LambaSayisi];

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

            // Aşağı bakan spot ışık; yumuşak gölgeli (5 spot gölgesi PC_RPAsset'teki 4096'lık alana sığar)
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
            isik.innerSpotAngle = IsikAcisi * IsikIcOrani;
            isik.shadows = LightShadows.Soft;
            isik.lightmapBakeType = LightmapBakeType.Realtime;

            // Işık şiddeti ve armatür parlaması simülasyonda PWM yüzdesine göre ayarlanır
            kontroller[i - 1] = lamba.gameObject.AddComponent<LambaKontrol>();
            kontroller[i - 1].Baglan(isik, armatur.GetComponent<Renderer>(), IsikSiddeti, ArmaturIsimasi);
        }
        return kontroller;
    }

    // Algılama çizgileri: yol girişinde (konum 0) ve her lambanın hizasında (konum 1..5)
    static SensorAlani[] SensorleriKur(Transform kok, Dictionary<string, Material> m)
    {
        var klasor = new GameObject("Sensorler").transform;
        klasor.SetParent(kok, false);
        var sensorler = new SensorAlani[LambaSayisi + 1];
        for (int k = 0; k <= LambaSayisi; k++)
        {
            var cizgi = Kutu($"Sensor_{k}", klasor, m["Sensor"]);
            cizgi.transform.position = new Vector3(k * LambaAraligi, 0.022f, 0f);
            cizgi.transform.localScale = new Vector3(0.5f, 0.01f, YolGenisligi);
            sensorler[k] = cizgi.AddComponent<SensorAlani>();
            sensorler[k].Baglan(k, cizgi.GetComponent<Renderer>());
        }
        return sensorler;
    }

    // Simülasyon yoldaki nesnenin türüne göre yaya ya da aracı görünür yapar ve X konumunu günceller.
    static (Transform yaya, Transform arac) NesneleriKur(Transform kok, Dictionary<string, Material> m)
    {
        var klasor = new GameObject("Nesneler").transform;
        klasor.SetParent(kok, false);

        // Yaya: eklemli prefab (YayaKurucu; yürüyüş anahtar kareleri prefabın Animator'ında).
        // Direklerin karşısındaki kaldırımda yürür; prefabın yüzü +Z, 90° döndürülerek yürüyüş yönüne (+X) bakar.
        var yaya = (GameObject)PrefabUtility.InstantiatePrefab(YayaKurucu.Hazirla(), klasor);
        yaya.name = "Yaya";
        yaya.transform.position = new Vector3(0f, KaldirimYuksekligi, -(YolGenisligi / 2f + KaldirimGenisligi / 2f));
        yaya.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        yaya.SetActive(false);

        // Araç sağ şeritte +X yönünde gider (4,2 × 1,8 × 1,4 m kutu)
        var arac = Kutu("Arac", klasor, m["Arac"]);
        arac.transform.position = new Vector3(0f, 0.72f, -YolGenisligi / 4f);
        arac.transform.localScale = new Vector3(4.2f, 1.4f, 1.8f);
        arac.SetActive(false);

        return (yaya.transform, arac.transform);
    }

    static Light AyIsigiKur(Transform kok)
    {
        var ay = new GameObject("AyIsigi");
        ay.transform.SetParent(kok, false);
        // Gece ay, gündüz güneş: renk, şiddet ve açı OrtamGecisi'nde (GeceOrtamiAyarla gece değerlerini yazar)
        var isik = ay.AddComponent<Light>();
        isik.type = LightType.Directional;
        isik.shadows = LightShadows.Soft;
        RenderSettings.sun = isik;
        return isik;
    }

    static Camera KameraKur(Transform kok)
    {
        var kameraGo = new GameObject("Kamera") { tag = "MainCamera" };
        kameraGo.transform.SetParent(kok, false);
        // Yolun ortasına (90 m) çapraz yukarıdan bakar; 5 lambanın hepsi görünür.
        // Yukarıdan yan bakış (26 Kasım'da önizlemelerle seçildi): lambalar eşit aralıklı ve aynı boyda
        // görünür, kısılma düzeni yan yana okunur; ekranın üstü geliştirici paneline kalır.
        // Sinematik çapraz açılar Aşama 4'teki anahtar kareli kamera turunda.
        // Gölge mesafesi (PC_RPAsset, 200 m) kameranın en uzak lambaya uzaklığından büyük olmalı.
        kameraGo.transform.position = new Vector3(YolUzunlugu / 2f, 55f, -48f);
        kameraGo.transform.LookAt(new Vector3(YolUzunlugu / 2f, 0f, 2f));
        var kamera = kameraGo.AddComponent<Camera>();
        kamera.fieldOfView = 60f;
        kamera.nearClipPlane = 0.3f;
        kamera.farClipPlane = 600f;
        kamera.clearFlags = CameraClearFlags.Skybox; // gökyüzü: Gokyuzu.mat (Skybox/Procedural)
        kamera.backgroundColor = new Color(0.02f, 0.03f, 0.08f); // gökyüzü yoksa yedek renk
        // Son işlem (bloom, tonemapping) kamerada açık olmalı; ince direklerin kenarları için SMAA
        var urpKamera = kamera.GetUniversalAdditionalCameraData();
        urpKamera.renderPostProcessing = true;
        urpKamera.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        kameraGo.AddComponent<AudioListener>();
        return kamera;
    }

    // Sahnenin tamamına etki eden Global Volume. Profil varlık olarak kaydedilir; ayarlar her kurulumda
    // koddaki değerlere döner (Play'de Inspector'dan Bloom'u kapatmak gibi denemeler kalıcı olmaz).
    static void GorselEfektleriKur(Transform kok)
    {
        var go = new GameObject("GlobalVolume");
        go.transform.SetParent(kok, false);
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1; // projenin varsayılan profilinin (bloom 0, tonemapping yok) üstünde
        volume.sharedProfile = VolumeProfiliHazirla();
    }

    static VolumeProfile VolumeProfiliHazirla()
    {
        var profil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfiliYolu);
        if (profil == null)
        {
            profil = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profil, VolumeProfiliYolu);
        }

        // Bloom: eşiği aşan parlak pikseller (armatürler) çevresine yumuşak bir hale yayar
        var bloom = Bilesen<Bloom>(profil);
        bloom.threshold.Override(BloomEsigi);
        bloom.intensity.Override(BloomSiddeti);
        bloom.scatter.Override(BloomYayilimi);
        bloom.highQualityFiltering.Override(true);

        // Tonemapping: HDR parlaklıkları ekrana sığdırır; ACES film benzeri kontrast verir
        var ton = Bilesen<Tonemapping>(profil);
        ton.mode.Override(TonemappingMode.ACES);

        // Pozlama (exposure): fotoğraf makinesindeki gibi tüm görüntünün parlaklığı, EV cinsinden
        var renk = Bilesen<ColorAdjustments>(profil);
        renk.postExposure.Override(PozlamaEV);

        EditorUtility.SetDirty(profil);
        return profil;
    }

    // Profilde bileşen yoksa ekler (profil dosyasının içine alt varlık olarak); her zaman etkin bırakır
    static T Bilesen<T>(VolumeProfile profil) where T : VolumeComponent
    {
        if (!profil.TryGet(out T bilesen))
        {
            bilesen = profil.Add<T>();
            bilesen.name = typeof(T).Name;
            bilesen.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(bilesen, profil);
        }
        bilesen.active = true;
        EditorUtility.SetDirty(bilesen);
        return bilesen;
    }

    // Sahne gece açılır. Gökyüzü Unity'nin hazır Skybox/Procedural shader'ı: güneş yönü RenderSettings.sun'dan
    // gelir, atmosfer saçılmasını hesaplar. Ortam ışığı düz renk (Flat): geçişte her kare hesaplama gerektirmez.
    static Material GeceOrtamiAyarla(Light ay)
    {
        string yol = $"{MalzemeKlasoru}/Gokyuzu.mat";
        var gokyuzu = AssetDatabase.LoadAssetAtPath<Material>(yol);
        if (gokyuzu == null)
        {
            gokyuzu = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(gokyuzu, yol);
        }
        gokyuzu.SetFloat("_SunSize", 0.03f);
        gokyuzu.SetColor("_GroundColor", new Color(0.2f, 0.22f, 0.2f));
        OrtamGecisi.GeceDegerleriniYaz(gokyuzu, ay);
        EditorUtility.SetDirty(gokyuzu);

        RenderSettings.skybox = gokyuzu;
        RenderSettings.ambientMode = AmbientMode.Flat;
        return gokyuzu;
    }

    // ---------- Yardımcılar ----------

    static GameObject Kutu(string ad, Transform ebeveyn, Material malzeme) =>
        Ilkel(PrimitiveType.Cube, ad, ebeveyn, malzeme);

    static GameObject Silindir(string ad, Transform ebeveyn, Material malzeme) =>
        Ilkel(PrimitiveType.Cylinder, ad, ebeveyn, malzeme);

    internal static GameObject Ilkel(PrimitiveType tur, string ad, Transform ebeveyn, Material malzeme)
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
            ["Armatur"] = Malzeme("Armatur", new Color(0.9f, 0.85f, 0.7f), isima: ArmaturIsimasi),
            // Sensör çizgisi: emission açık ama siyah; algılayınca SensorAlani yeşile çevirir
            ["Sensor"] = Malzeme("Sensor", new Color(0.1f, 0.12f, 0.1f), isima: Color.black),
            ["Arac"] = Malzeme("Arac", new Color(0.75f, 0.12f, 0.12f), metalik: 0.4f, puruzsuzluk: 0.6f),
        };
    }

    // Yaya malzemeleri YayaKurucu'da; o da bu metodu kullanır
    internal static Material Malzeme(string ad, Color renk, float metalik = 0f, float puruzsuzluk = 0.1f, Color? isima = null)
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
            // Emission: armatürün kendi kendine parlaması (HDR; bloom GorselEfektleriKur'da)
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
