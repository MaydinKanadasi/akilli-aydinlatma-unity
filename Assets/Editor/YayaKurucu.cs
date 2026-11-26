using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Basit şekillerden eklemli yaya prefabı: Assets/Prefabs/Yaya.prefab (yaklaşık 1,75 m).
// Yürüyüş animasyonu grafik dersi gereği Animation penceresinde ELLE anahtar karelerle yapılır;
// bu betik yalnızca iskeleti ve boş bir Animator Controller'ı (Assets/Animations/Yaya.controller) hazırlar.
//
// İskelet: eklemler boş nesnelerdir ve animasyonda döndürülecek olanlar onlardır; görünen şekiller
// ("_Sekil" ile biten) eklemlerin altındadır ve döndürülmez. Her eklemin merkezi gerçek eklem noktasındadır
// (omuz, dirsek, kalça, diz), böylece döndürünce uzuv o noktanın etrafında salınır.
//
//   Yaya                  kök: Animator; ayak tabanı y = 0, yüzü +Z
//   └ Kalca               leğen (y 0,95); yürürken hafif inip kalkma bunun Position.y'si
//     ├ Govde             gövde; hafif öne eğilme / dönme
//     │ ├ Bas             boyun noktası
//     │ ├ Omuz_Sol → Dirsek_Sol
//     │ └ Omuz_Sag → Dirsek_Sag
//     ├ Bacak_Sol → Diz_Sol      (Bacak = kalça eklemi)
//     └ Bacak_Sag → Diz_Sag
//
// Yürüyüşte bacak ve kol salınımı eklemin Rotation.x'idir (yandan bakınca öne/arkaya).
// Eklem adları animasyon klibindeki yollardır: ad değişirse anahtar kareler bağını kaybeder.
// Kökün konumu animasyonla değiştirilmez: yayayı yolda SimulasyonYoneticisi ilerletir.
public static class YayaKurucu
{
    public const string PrefabYolu = "Assets/Prefabs/Yaya.prefab";
    const string ControllerYolu = "Assets/Animations/Yaya.controller";

    // Sahne kurulumu çağırır: prefab yoksa oluşturur, varsa olduğu gibi kullanır
    public static GameObject Hazirla()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabYolu);
        return prefab != null ? prefab : Olustur();
    }

    [MenuItem("Akıllı Aydınlatma/Yaya Prefabını Yeniden Oluştur")]
    static void MenudenYenidenOlustur()
    {
        if (File.Exists(PrefabYolu) && !EditorUtility.DisplayDialog("Yaya prefabı",
                "Yaya prefabı koddaki iskeletle baştan oluşturulacak; prefabda elle yapılan değişiklikler silinir. " +
                "Animasyon klipleri ve Animator Controller korunur. Devam edilsin mi?",
                "Yeniden oluştur", "Vazgeç"))
            return;
        Olustur();
        Debug.Log($"[YayaKurucu] Prefab oluşturuldu: {PrefabYolu}. Sahnede görmek için Sahneyi Kur'u çalıştırın.");
    }

    // Terminalden: Unity -batchmode -quit -projectPath . -executeMethod YayaKurucu.KomutSatirindanOlustur
    public static void KomutSatirindanOlustur()
    {
        Olustur();
        Debug.Log($"[YayaKurucu] Batchmode: prefab oluşturuldu: {PrefabYolu}");
    }

    static GameObject Olustur()
    {
        var ceket = SahneKurucu.Malzeme("Yaya", new Color(0.95f, 0.55f, 0.15f)); // turuncu (Aşama 2'deki renk)
        var pantolon = SahneKurucu.Malzeme("YayaPantolon", new Color(0.12f, 0.16f, 0.28f));
        var ten = SahneKurucu.Malzeme("YayaTen", new Color(0.85f, 0.65f, 0.5f), puruzsuzluk: 0.3f);
        var ayakkabi = SahneKurucu.Malzeme("YayaAyakkabi", new Color(0.08f, 0.08f, 0.08f), puruzsuzluk: 0.4f);

        var kok = new GameObject("Yaya");

        var kalca = Eklem("Kalca", kok.transform, new Vector3(0f, 0.95f, 0f));
        Sekil(PrimitiveType.Cube, "Kalca_Sekil", kalca, Vector3.zero, new Vector3(0.32f, 0.16f, 0.18f), pantolon);

        // Gövde ve baş
        var govde = Eklem("Govde", kalca, Vector3.zero);
        Sekil(PrimitiveType.Cube, "Govde_Sekil", govde, new Vector3(0f, 0.32f, 0f), new Vector3(0.36f, 0.5f, 0.2f), ceket);
        var bas = Eklem("Bas", govde, new Vector3(0f, 0.58f, 0f));
        Sekil(PrimitiveType.Sphere, "Bas_Sekil", bas, new Vector3(0f, 0.11f, 0f), Vector3.one * 0.22f, ten);
        // Burun: yüzün hangi yöne baktığı (+Z) Animation penceresinde de belli olsun
        Sekil(PrimitiveType.Cube, "Burun_Sekil", bas, new Vector3(0f, 0.11f, 0.11f), new Vector3(0.04f, 0.05f, 0.05f), ten);

        // Kollar: omuz → üst kol, dirsek → alt kol ve el. Sol = -X (yüzü +Z olan karakterin solu)
        foreach (var (taraf, x) in new[] { ("Sol", -0.23f), ("Sag", 0.23f) })
        {
            var omuz = Eklem($"Omuz_{taraf}", govde, new Vector3(x, 0.53f, 0f));
            Sekil(PrimitiveType.Capsule, $"UstKol_{taraf}_Sekil", omuz, new Vector3(0f, -0.15f, 0f), new Vector3(0.1f, 0.15f, 0.1f), ceket);
            var dirsek = Eklem($"Dirsek_{taraf}", omuz, new Vector3(0f, -0.3f, 0f));
            Sekil(PrimitiveType.Capsule, $"AltKol_{taraf}_Sekil", dirsek, new Vector3(0f, -0.13f, 0f), new Vector3(0.09f, 0.13f, 0.09f), ceket);
            Sekil(PrimitiveType.Sphere, $"El_{taraf}_Sekil", dirsek, new Vector3(0f, -0.29f, 0f), Vector3.one * 0.09f, ten);
        }

        // Bacaklar: kalça eklemi (y 0,92) → uyluk, diz (y 0,47) → baldır ve ayak; ayak tabanı y = 0
        foreach (var (taraf, x) in new[] { ("Sol", -0.1f), ("Sag", 0.1f) })
        {
            var bacak = Eklem($"Bacak_{taraf}", kalca, new Vector3(x, -0.03f, 0f));
            Sekil(PrimitiveType.Capsule, $"Uyluk_{taraf}_Sekil", bacak, new Vector3(0f, -0.225f, 0f), new Vector3(0.15f, 0.225f, 0.15f), pantolon);
            var diz = Eklem($"Diz_{taraf}", bacak, new Vector3(0f, -0.45f, 0f));
            Sekil(PrimitiveType.Capsule, $"Baldir_{taraf}_Sekil", diz, new Vector3(0f, -0.21f, 0f), new Vector3(0.12f, 0.21f, 0.12f), pantolon);
            Sekil(PrimitiveType.Cube, $"Ayak_{taraf}_Sekil", diz, new Vector3(0f, -0.44f, 0.05f), new Vector3(0.11f, 0.06f, 0.25f), ayakkabi);
        }

        // Animator: klip yok; kullanıcı Animation penceresinde "Create" ile ilk klibi bu controller'a ekler
        var animator = kok.AddComponent<Animator>();
        animator.runtimeAnimatorController = ControllerHazirla();
        animator.applyRootMotion = false; // yürüyüş yerinde; ilerleme simülasyondan
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabYolu));
        var prefab = PrefabUtility.SaveAsPrefabAsset(kok, PrefabYolu);
        Object.DestroyImmediate(kok);
        AssetDatabase.SaveAssets();
        return prefab;
    }

    // Var olan controller korunur (içindeki elle yapılmış klipler kaybolmasın diye)
    static AnimatorController ControllerHazirla()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerYolu);
        if (controller != null) return controller;
        Directory.CreateDirectory(Path.GetDirectoryName(ControllerYolu));
        return AnimatorController.CreateAnimatorControllerAtPath(ControllerYolu);
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
