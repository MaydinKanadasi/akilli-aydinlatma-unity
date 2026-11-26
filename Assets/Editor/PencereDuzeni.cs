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
    [MenuItem("Akıllı Aydınlatma/Yaya Yürüyüşüne Hazırla")]
    static void YayaYuruyusuneHazirla()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(YayaKurucu.PrefabYolu);
        if (prefab == null) { Debug.LogError("[PencereDuzeni] Yaya prefabı yok; önce Sahneyi Kur."); return; }
        AssetDatabase.OpenAsset(prefab); // prefab modu

        var animasyon = AnimationPenceresiniYerlestir();

        // Yandan bakış: kamera -X yönüne bakar, yayanın yüzü (+Z) ekranda sağa döner
        var sahne = SceneView.lastActiveSceneView;
        if (sahne != null)
        {
            sahne.orthographic = false;
            sahne.LookAt(new Vector3(0f, 0.9f, 0f), Quaternion.Euler(0f, -90f, 0f), 1.4f);
        }

        // Seçim Animation penceresine bir sonraki karede ulaşır; kayıt ondan sonra açılır
        EditorApplication.delayCall += () =>
        {
            var klip = AssetDatabase.LoadAssetAtPath<AnimationClip>(YayaKlibi);
            if (klip != null) animasyon.animationClip = klip;
            animasyon.frame = 0;
            animasyon.recording = true;
            Debug.Log("[PencereDuzeni] Yaya yürüyüşüne hazır: kayıt açık, kare 0.");
        };
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
