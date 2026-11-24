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
        public void Gunduz_TumLambalarKapali()
        {
            sim.SetNight(false);
            sim.Spawn(50);
            sim.Advance(5);
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 0 }, sim.State.Lamps);
        }
    }
}
