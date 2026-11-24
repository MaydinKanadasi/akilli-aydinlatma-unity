using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AkilliAydinlatma.Tests
{
    // Aşama 2 kabul kriteri: C# kontrol mantığı, web'deki controller.ts ile birebir aynı sonucu veriyor.
    // Beklenen değerler web reposundaki scripts/test-vektorleri.ts tarafından üretilen JSON'dan okunur.
    public class KontrolMantigiTestleri
    {
        [Serializable]
        class Ayarlar
        {
            public double lampSpacingM;
            public double lookaheadSec;
            public double pedestrianMaxKmh;
            public double lampPowerW;
        }

        [Serializable]
        class Vektor
        {
            public bool isNight;
            public bool hasObject;
            public string type;
            public double speedKmh;
            public double position;
            public int[] lamps;
            public double powerW;
            public double normalPowerW;
        }

        [Serializable]
        class VektorDosyasi
        {
            public Ayarlar config;
            public Vektor[] vectors;
        }

        static VektorDosyasi Yukle()
        {
            string yol = Path.Combine(Application.dataPath, "Tests/EditMode/kontrol-vektorleri.json");
            Assert.That(File.Exists(yol), $"Test verisi bulunamadı: {yol}");
            var dosya = JsonUtility.FromJson<VektorDosyasi>(File.ReadAllText(yol));
            Assert.That(dosya?.vectors, Is.Not.Null.And.Not.Empty, "Test verisi boş");
            return dosya;
        }

        static string Yaz(int[] dizi) => "[" + string.Join(", ", dizi) + "]";

        [Test]
        public void Parametreler_WebIleAyni()
        {
            var web = Yukle().config;
            var cs = ControllerConfig.Default;
            Assert.AreEqual(web.lampSpacingM, cs.LampSpacingM, "Lamba aralığı");
            Assert.AreEqual(web.lookaheadSec, cs.LookaheadSec, "Önden aydınlatma süresi");
            Assert.AreEqual(web.pedestrianMaxKmh, cs.PedestrianMaxKmh, "Yaya/araç eşiği");
            Assert.AreEqual(web.lampPowerW, cs.LampPowerW, "Lamba gücü");
        }

        [Test]
        public void TumDurumlar_WebIleBirebirAyni()
        {
            var dosya = Yukle();
            var hatalar = new List<string>();

            foreach (var v in dosya.vectors)
            {
                TrackedObject? nesne = null;
                if (v.hasObject)
                {
                    // Kural 6: tür, C# tarafında hızdan yeniden hesaplanır ve web'inkiyle karşılaştırılır
                    var tur = KontrolMantigi.ClassifyBySpeed(v.speedKmh);
                    string turAdi = tur == ObjectType.Car ? "car" : "pedestrian";
                    if (turAdi != v.type)
                        hatalar.Add($"{v.speedKmh} km/s: tür C#={turAdi}, web={v.type}");
                    nesne = new TrackedObject(tur, v.speedKmh, v.position);
                }

                var s = KontrolMantigi.ComputeState(v.isNight, nesne);
                string durum = v.hasObject
                    ? $"{(v.isNight ? "gece" : "gündüz")}, {v.speedKmh} km/s, konum {v.position}"
                    : $"{(v.isNight ? "gece" : "gündüz")}, nesne yok";

                if (!s.Lamps.SequenceEqual(v.lamps))
                    hatalar.Add($"{durum}: lambalar C#={Yaz(s.Lamps)}, web={Yaz(v.lamps)}");
                if (Math.Abs(s.PowerW - v.powerW) > 1e-9)
                    hatalar.Add($"{durum}: güç C#={s.PowerW}, web={v.powerW}");
                if (Math.Abs(s.NormalPowerW - v.normalPowerW) > 1e-9)
                    hatalar.Add($"{durum}: normal güç C#={s.NormalPowerW}, web={v.normalPowerW}");
            }

            if (hatalar.Count > 0)
                Assert.Fail($"{dosya.vectors.Length} durumdan {hatalar.Count} farklı. İlk 20:\n" +
                            string.Join("\n", hatalar.Take(20)));

            Debug.Log($"[KontrolMantigiTestleri] {dosya.vectors.Length} durumun hepsi web ile aynı.");
        }

        [Test]
        public void DokumandakiOrnek_40KmsArac_Konum2_3()
        {
            var s = KontrolMantigi.ComputeState(true, new TrackedObject(ObjectType.Car, 40, 2.3));
            CollectionAssert.AreEqual(new[] { 20, 100, 100, 100, 40 }, s.Lamps);
            Assert.AreEqual(360, s.PowerW, 1e-9);
            Assert.AreEqual(500, s.NormalPowerW, 1e-9);
        }

        [Test]
        public void Yuvarlama_JavaScriptGibi()
        {
            Assert.AreEqual(2, KontrolMantigi.JsRound(1.5));
            Assert.AreEqual(3, KontrolMantigi.JsRound(2.5));
            Assert.AreEqual(2, KontrolMantigi.JsRound(2.3));
            // C#'ın kendi yuvarlaması farklıdır; bu yüzden JsRound kullanılıyor
            Assert.AreEqual(2.0, Math.Round(2.5));
        }

        [Test]
        public void PwmDuty_8Bit()
        {
            Assert.AreEqual(0, KontrolMantigi.PercentToDuty(0));
            Assert.AreEqual(51, KontrolMantigi.PercentToDuty(20));
            Assert.AreEqual(102, KontrolMantigi.PercentToDuty(40));
            Assert.AreEqual(255, KontrolMantigi.PercentToDuty(100));
        }
    }
}
