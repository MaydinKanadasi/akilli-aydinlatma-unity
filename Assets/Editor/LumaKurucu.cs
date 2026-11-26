using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// LLM asistanı LUMA'nın uçan robot gövdesi, basit şekillerden: Assets/Prefabs/Luma.prefab (~1,3 m).
// 15 Aralık için asgari sürüm (CLAUDE.md, "Robot karakteri – LUMA"): lacivert gövde, fener başlığı
// biçiminde kafa, ekranda iki amber LED göz ve ağız, tepede ampul gibi parlayan anten, kafanın iki
// yanında pervaneler, altından yola düşen amber ışık. Kollar ve yüz ifadeleri sonraki sürümde.
//
// Salınma ve pervane dönüşü grafik dersi gereği Animation penceresinde ELLE anahtar karelerle yapılır;
// bu betik yalnızca iskeleti, boş bir klibi (LumaSalinma, döngü açık) ve Animator Controller'ı hazırlar.
//
//   Luma                    kök: Animator; yüzü +Z. Kök animasyonlanmaz (uçuşu ileride kod yönetir)
//   └ Govde                 salınma: Position.y (havada inip kalkma), hafif Rotation.z (yalpalama)
//     ├ Bas                 fener başlığı; ekran, gözler, ağız, anten
//     │ ├ Pervane_Sol       dönüş: Rotation.y
//     │ └ Pervane_Sag
//     └ Isik                altta, yola bakan amber spot ışık
public static class LumaKurucu
{
    public const string PrefabYolu = "Assets/Prefabs/Luma.prefab";
    const string ControllerYolu = "Assets/Animations/Luma.controller";
    public const string KlipYolu = "Assets/Animations/LumaSalinma.anim";

    // Amber ışıklar (gözler, ağız, anten ampulü): HDR, bloom eşiğini (1) aşar ve hafifçe parlar
    static readonly Color Amber = new Color(1f, 0.62f, 0.15f);

    // Sahne kurulumu çağırır: prefab yoksa oluşturur, varsa olduğu gibi kullanır
    public static GameObject Hazirla()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabYolu);
        return prefab != null ? prefab : Olustur();
    }

    [MenuItem("Akıllı Aydınlatma/LUMA Prefabını Yeniden Oluştur")]
    static void MenudenYenidenOlustur()
    {
        if (File.Exists(PrefabYolu) && !EditorUtility.DisplayDialog("LUMA prefabı",
                "LUMA prefabı koddaki iskeletle baştan oluşturulacak; prefabda elle yapılan değişiklikler silinir. " +
                "Animasyon klibi ve Animator Controller korunur. Devam edilsin mi?",
                "Yeniden oluştur", "Vazgeç"))
            return;
        Olustur();
        Debug.Log($"[LumaKurucu] Prefab oluşturuldu: {PrefabYolu}. Sahnede görmek için Sahneyi Kur'u çalıştırın.");
    }

    static GameObject Olustur()
    {
        var govdeMat = SahneKurucu.Malzeme("LumaGovde", new Color(0.07f, 0.11f, 0.3f), metalik: 0.5f, puruzsuzluk: 0.6f);
        var ekranMat = SahneKurucu.Malzeme("LumaEkran", new Color(0.02f, 0.02f, 0.04f), puruzsuzluk: 0.9f);
        var amberMat = SahneKurucu.Malzeme("LumaAmber", Amber, isima: Amber * 4f);
        var pervaneMat = SahneKurucu.Malzeme("LumaPervane", new Color(0.6f, 0.62f, 0.66f), metalik: 0.8f, puruzsuzluk: 0.5f);

        var kok = new GameObject("Luma");
        var govde = Eklem("Govde", kok.transform, Vector3.zero);
        Sekil(PrimitiveType.Capsule, "Govde_Sekil", govde, Vector3.zero, new Vector3(0.5f, 0.35f, 0.4f), govdeMat);
        // Gövdenin altındaki parlayan halka: ışığın çıktığı yer
        Sekil(PrimitiveType.Sphere, "IsikCikisi_Sekil", govde, new Vector3(0f, -0.33f, 0f), new Vector3(0.2f, 0.05f, 0.2f), amberMat);

        // Fener başlığı biçiminde kafa ve yüz ekranı (yüzü +Z)
        var bas = Eklem("Bas", govde, new Vector3(0f, 0.5f, 0f));
        Sekil(PrimitiveType.Cylinder, "Bas_Sekil", bas, Vector3.zero, new Vector3(0.5f, 0.2f, 0.5f), govdeMat);
        Sekil(PrimitiveType.Cube, "Ekran_Sekil", bas, new Vector3(0f, 0f, 0.2f), new Vector3(0.36f, 0.24f, 0.12f), ekranMat);
        Sekil(PrimitiveType.Sphere, "Goz_Sol_Sekil", bas, new Vector3(-0.08f, 0.03f, 0.26f), Vector3.one * 0.07f, amberMat);
        Sekil(PrimitiveType.Sphere, "Goz_Sag_Sekil", bas, new Vector3(0.08f, 0.03f, 0.26f), Vector3.one * 0.07f, amberMat);
        Sekil(PrimitiveType.Cube, "Agiz_Sekil", bas, new Vector3(0f, -0.06f, 0.26f), new Vector3(0.12f, 0.02f, 0.02f), amberMat);
        Sekil(PrimitiveType.Cylinder, "Anten_Sekil", bas, new Vector3(0f, 0.32f, 0f), new Vector3(0.025f, 0.12f, 0.025f), pervaneMat);
        Sekil(PrimitiveType.Sphere, "Ampul_Sekil", bas, new Vector3(0f, 0.47f, 0f), Vector3.one * 0.1f, amberMat);

        // Pervaneler: kafanın iki yanında, kısa bir kolun ucunda; dönüş ekseni yukarı (Rotation.y)
        foreach (var (taraf, x) in new[] { ("Sol", -1f), ("Sag", 1f) })
        {
            Sekil(PrimitiveType.Cube, $"PervaneKolu_{taraf}_Sekil", bas, new Vector3(x * 0.34f, 0.05f, 0f), new Vector3(0.2f, 0.04f, 0.05f), govdeMat);
            var pervane = Eklem($"Pervane_{taraf}", bas, new Vector3(x * 0.46f, 0.12f, 0f));
            Sekil(PrimitiveType.Cylinder, $"PervaneGobek_{taraf}_Sekil", pervane, Vector3.zero, new Vector3(0.07f, 0.04f, 0.07f), pervaneMat);
            Sekil(PrimitiveType.Cube, $"Kanat1_{taraf}_Sekil", pervane, new Vector3(0f, 0.04f, 0f), new Vector3(0.5f, 0.015f, 0.06f), pervaneMat);
            Sekil(PrimitiveType.Cube, $"Kanat2_{taraf}_Sekil", pervane, new Vector3(0f, 0.04f, 0f), new Vector3(0.06f, 0.015f, 0.5f), pervaneMat);
        }

        // Altından yola düşen amber ışık (gölgesiz: sokak lambalarının gölge alanını paylaşmasın)
        var isikGo = Eklem("Isik", govde, new Vector3(0f, -0.36f, 0f));
        isikGo.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var isik = isikGo.gameObject.AddComponent<Light>();
        isik.type = LightType.Spot;
        isik.color = Amber;
        isik.intensity = 60f;
        isik.range = 25f;
        isik.spotAngle = 45f;
        isik.innerSpotAngle = 30f;
        isik.shadows = LightShadows.None;

        var animator = kok.AddComponent<Animator>();
        animator.runtimeAnimatorController = AnimasyonHazirla();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabYolu));
        var prefab = PrefabUtility.SaveAsPrefabAsset(kok, PrefabYolu);
        Object.DestroyImmediate(kok);
        AssetDatabase.SaveAssets();
        return prefab;
    }

    // Controller ve boş, döngülü LumaSalinma klibi (varsayılan durum). Var olanlar korunur.
    static AnimatorController AnimasyonHazirla()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ControllerYolu));
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerYolu)
                         ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerYolu);
        if (controller.layers[0].stateMachine.states.Length > 0) return controller;

        var klip = AssetDatabase.LoadAssetAtPath<AnimationClip>(KlipYolu);
        if (klip == null)
        {
            klip = new AnimationClip { frameRate = 60f };
            var ayar = AnimationUtility.GetAnimationClipSettings(klip);
            ayar.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(klip, ayar);
            AssetDatabase.CreateAsset(klip, KlipYolu);
        }
        controller.AddMotion(klip);
        return controller;
    }

    static Transform Eklem(string ad, Transform ebeveyn, Vector3 yerelKonum)
    {
        var t = new GameObject(ad).transform;
        t.SetParent(ebeveyn, false);
        t.localPosition = yerelKonum;
        return t;
    }

    static void Sekil(PrimitiveType tur, string ad, Transform eklem, Vector3 yerelKonum, Vector3 olcek, Material malzeme)
    {
        var go = SahneKurucu.Ilkel(tur, ad, eklem, malzeme);
        go.transform.localPosition = yerelKonum;
        go.transform.localScale = olcek;
    }
}
