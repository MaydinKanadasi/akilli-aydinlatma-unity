using NUnit.Framework;
using UnityEngine;

namespace AkilliAydinlatma.Tests
{
    // Simülasyon yöneticisinin web'deki useMockSimulation.ts ile aynı davrandığını denetler.
    public class SimulasyonTestleri
    {
        SimulasyonYoneticisi sim;

        [SetUp]
        public void Kur() => sim = new GameObject("TestSim").AddComponent<SimulasyonYoneticisi>();

        [TearDown]
        public void Temizle() => Object.DestroyImmediate(sim.gameObject);

        [Test]
        public void NextPosition_36Kms3SaniyedeBirLambaIlerler()
        {
            // 36 km/s = 10 m/s; 3 sn'de 30 m = 1 lamba aralığı
            var o = new TrackedObject(ObjectType.Car, 36, 2);
            Assert.AreEqual(3.0, KontrolMantigi.NextPosition(o, 3), 1e-12);
        }

        [Test]
        public void YayaAnimasyonHizi_IlerleyisleUyumlu()
        {
            // 5,04 km/s = 1,4 m/s = döngü boyu: 1x'te saniyede tam 1 döngü (klibin kendi hızı)
            Assert.AreEqual(1f, SimulasyonYoneticisi.YayaAnimasyonHizi(5.04, 1, false), 1e-5);
            // Zaman çarpanıyla doğrusal ölçeklenir
            Assert.AreEqual(5f, SimulasyonYoneticisi.YayaAnimasyonHizi(5.04, 5, false), 1e-4);
            // Duraklatınca bacaklar durur
            Assert.AreEqual(0f, SimulasyonYoneticisi.YayaAnimasyonHizi(5.04, 5, true));
        }

        [Test]
        public void Baslangic_WebIleAyni()
        {
            Assert.IsTrue(sim.IsNight);
            Assert.IsNull(sim.Nesne);
            Assert.IsFalse(sim.Paused);
            Assert.AreEqual(5, sim.TimeScale);
        }

        [Test]
        public void Spawn_TuruHizdanBelirler_VeKonum0danBaslar()
        {
            sim.Spawn(5);
            Assert.AreEqual(ObjectType.Pedestrian, sim.Nesne.Value.Type);
            Assert.AreEqual(0, sim.Nesne.Value.Position);
            sim.Spawn(50);
            Assert.AreEqual(ObjectType.Car, sim.Nesne.Value.Type);
        }

        [Test]
        public void Nesne_6yiGecinceYoldanCikar()
        {
            sim.Spawn(36); // 10 m/s → 18 sn'de 6 lamba aralığı
            sim.Advance(17.9);
            Assert.IsNotNull(sim.Nesne);
            sim.Advance(0.2);
            Assert.IsNull(sim.Nesne);
        }

        [Test]
        public void Enerji_GecedeYolBosken_AkilliSifir_Normal500W()
        {
            sim.Advance(3600); // 1 saat
            Assert.AreEqual(0, sim.SmartWh, 1e-9);
            Assert.AreEqual(500, sim.NormalWh, 1e-9);
        }

        [Test]
        public void Sensorler_HiziDogruOlcer_BuyukAdimdaBile()
        {
            // 10x'te bir karede 4 sn'lik adım (90 km/s = 100 m, üç sensörü birden geçer)
            sim.Spawn(90);
            for (int i = 0; i < 3; i++) sim.Advance(4);
            Assert.IsTrue(sim.OlculenHizKmh.HasValue, "Hız ölçülemedi");
            Assert.AreEqual(90, sim.OlculenHizKmh.Value, 1e-6);
        }

        [Test]
        public void AracFarlari_GeceYanarGunduzSoner()
        {
            var arac = new GameObject("TestArac");
            var far = new GameObject("Far").AddComponent<Light>();
            far.transform.SetParent(arac.transform);
            var farlar = arac.AddComponent<AracFarlari>();
            farlar.Baglan(new[] { far }, null, null, Color.white, Color.red);
            sim.Baglan(null, null, null, arac.transform, null);
            try
            {
                sim.SetNight(false);
                Assert.IsFalse(farlar.Yanik);
                Assert.IsFalse(far.enabled);
                sim.SetNight(true);
                Assert.IsTrue(farlar.Yanik);
                Assert.IsTrue(far.enabled);
            }
            finally { Object.DestroyImmediate(arac); }
        }

        [Test]
        public void Arayuz_TurkceBicim()
        {
            // Ondalık virgül; 1000 Wh'ten itibaren kWh (web EnergyPanel ile aynı)
            Assert.AreEqual("12,3 Wh", ArayuzPaneli.EnerjiYaz(12.34));
            Assert.AreEqual("1,23 kWh", ArayuzPaneli.EnerjiYaz(1234.5));
            // Gündüz normal sistem tüketmez: tasarruf yüzdesi yok
            Assert.AreEqual("—", ArayuzPaneli.TasarrufYaz(0, 0, "0"));
            Assert.AreEqual("%72", ArayuzPaneli.TasarrufYaz(140, 500, "0"));
            Assert.AreEqual("Araç · 40 km/s · konum 2,30",
                ArayuzPaneli.NesneYaz(new TrackedObject(ObjectType.Car, 40, 2.3)));
            Assert.AreEqual("Yolda nesne yok", ArayuzPaneli.NesneYaz(null));
            Assert.AreEqual("1:05", ArayuzPaneli.SureYaz(65.9));
            Assert.AreEqual("1:00:05", ArayuzPaneli.SureYaz(3605));
        }

        [Test]
        public void Gunduz_TumLambalarKapali()
        {
            sim.SetNight(false);
            sim.Spawn(50);
            sim.Advance(5);
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 0 }, sim.State.Lamps);
        }
    }
}
