using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Anahtar kare çalışması için editör düzeni (kullanıcı yalnızca değerleri girsin diye).
// Anahtar kareleri bu betik KOYMAZ: ders gereği kullanıcı Animation penceresinde elle girer.
public static class PencereDuzeni
{
    const string YayaKlibi = "Assets/Animations/YayaYuru.anim";

    // Yaya prefabını açar, Animation penceresini alta yerleştirir, kökü seçer, yandan görünüme geçer,
    // YayaYuru klibinde kaydı (Record) açıp 0. kareye gider.
    // Yandan bakış: kamera -X yönüne bakar, yayanın yüzü (+Z) ekranda sağa döner
    [MenuItem("Akıllı Aydınlatma/Yaya Yürüyüşüne Hazırla")]
    static void YayaYuruyusuneHazirla() =>
        AnimasyonaHazirla(YayaKurucu.PrefabYolu, YayaKlibi, new Vector3(0f, 0.9f, 0f), Quaternion.Euler(0f, -90f, 0f), 1.4f);

    // Önden, hafif yukarıdan ve yandan bakış: pervaneler ve salınma birlikte görünür
    [MenuItem("Akıllı Aydınlatma/LUMA Animasyonuna Hazırla")]
    static void LumaAnimasyonunaHazirla() =>
        AnimasyonaHazirla(LumaKurucu.PrefabYolu, LumaKurucu.KlipYolu, new Vector3(0f, 0.3f, 0f), Quaternion.Euler(15f, 205f, 0f), 1.3f);

    static void AnimasyonaHazirla(string prefabYolu, string klipYolu, Vector3 bakisNoktasi, Quaternion bakis, float uzaklik)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabYolu);
        if (prefab == null) { Debug.LogError($"[PencereDuzeni] {prefabYolu} yok; önce Sahneyi Kur."); return; }
        AssetDatabase.OpenAsset(prefab); // prefab modu

        var animasyon = AnimationPenceresiniYerlestir();

        var sahne = SceneView.lastActiveSceneView;
        if (sahne != null)
        {
            sahne.orthographic = false;
            sahne.LookAt(bakisNoktasi, bakis, uzaklik);
        }

        // Seçim Animation penceresine bir sonraki karede ulaşır; kayıt ondan sonra açılır
        EditorApplication.delayCall += () =>
        {
            var klip = AssetDatabase.LoadAssetAtPath<AnimationClip>(klipYolu);
            if (klip != null) animasyon.animationClip = klip;
            animasyon.frame = 0;
            animasyon.recording = true;
            Debug.Log($"[PencereDuzeni] {prefab.name} animasyonuna hazır: kayıt açık, kare 0.");
        };
    }

    // Retina ekranda Game sekmesi varsayılan olarak düşük çözünürlükte çizip 2 kat büyütür ("Scale 2x"):
    // arayüz yazıları bulanıklaşır. Free Aspect menüsündeki "Low Resolution Aspect Ratios" seçeneğini kapatır
    // (GameView iç sınıf olduğu için yansımayla). Yalnızca editör görünümü; macOS derlemesini etkilemez.
    [MenuItem("Akıllı Aydınlatma/Game Görünümünü Keskinleştir")]
    static void GameGorunumunuKeskinlestir()
    {
        Type tip = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        var ozellik = tip?.GetProperty("lowResolutionForAspectRatios",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (ozellik == null) { Debug.LogError("[PencereDuzeni] GameView.lowResolutionForAspectRatios bulunamadı."); return; }
        foreach (var pencere in Resources.FindObjectsOfTypeAll(tip))
        {
            ozellik.SetValue(pencere, false);
            ((EditorWindow)pencere).Repaint();
        }
        Debug.Log("[PencereDuzeni] Game görünümü tam çözünürlükte (Low Resolution Aspect Ratios kapalı).");
    }

    [MenuItem("Akıllı Aydınlatma/Animation Penceresini Alta Yerleştir")]
    static void AnimationPenceresiniYerlestirMenu() => AnimationPenceresiniYerlestir();

    // Animation penceresini alttaki Project/Console paneline sekme olarak yerleştirir (yüzen pencere
    // Scene'i örtüyordu). Prefab modu açıksa prefabın kökünü seçer.
    static AnimationWindow AnimationPenceresiniYerlestir()
    {
        // Yüzen Animation pencereleri kapatılır: GetWindow var olanı bulursa onu yerinden oynatmaz
        foreach (var pencere in Resources.FindObjectsOfTypeAll<AnimationWindow>())
            pencere.Close();

        // Project penceresinin (ProjectBrowser, Unity'de iç sınıf) yanına sekme olarak aç
        Type project = typeof(Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
        var animasyon = project != null
            ? EditorWindow.GetWindow<AnimationWindow>("Animation", true, project)
            : EditorWindow.GetWindow<AnimationWindow>("Animation", true);

        var prefabSahnesi = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabSahnesi != null) Selection.activeGameObject = prefabSahnesi.prefabContentsRoot;
        animasyon.Focus();
        return animasyon;
    }
}
