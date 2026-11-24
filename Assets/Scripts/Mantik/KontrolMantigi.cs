using System;

// Web arayüzündeki src/logic/controller.ts dosyasının birebir C# karşılığı.
// Aynı kurallar, aynı parametreler, aynı işlem sırası ve aynı sayı tipi (double) kullanılır; böylece
// aynı girdide JavaScript ile aynı sonuç çıkar. Değişiklik yapılırsa iki dosya birlikte güncellenir ve
// test verisi web reposunda yeniden üretilir:
//   npm run -s test-vektorleri > ~/Projects/akilli-aydinlatma-unity/Assets/Tests/EditMode/kontrol-vektorleri.json
//
// Bu dosya UnityEngine'e bağlı değildir; yalnızca hesap yapar.
namespace AkilliAydinlatma
{
    // ObjectType
    public enum ObjectType { Pedestrian, Car }

    // ControllerConfig
    public sealed class ControllerConfig
    {
        public double LampSpacingM = 30;     // iki lamba arası mesafe (m)
        public double LookaheadSec = 3;      // araç kaç saniye sonra ulaşacağı lambaları tam yaksın
        public double PedestrianMaxKmh = 7;  // bu hızın üstü araç sayılır
        public double LampPowerW = 100;      // bir lambanın %100'deki gücü (W)

        public static readonly ControllerConfig Default = new ControllerConfig();
    }

    // TrackedObject
    public readonly struct TrackedObject
    {
        public readonly ObjectType Type;
        public readonly double SpeedKmh;
        // Lamba cinsinden konum: 1 = 1. lambanın hizası, 0 = yola giriş, 6'yı geçince yoldan çıkar
        public readonly double Position;

        public TrackedObject(ObjectType type, double speedKmh, double position)
        {
            Type = type;
            SpeedKmh = speedKmh;
            Position = position;
        }
    }

    // SystemState (source alanı veri gönderilirken eklenir)
    public readonly struct SystemState
    {
        public readonly bool IsNight;
        public readonly TrackedObject? Object; // yolda nesne yoksa null
        public readonly int[] Lamps;           // 5 lambanın parlaklığı, % (0–100)
        public readonly double PowerW;         // akıllı sistemin anlık gücü
        public readonly double NormalPowerW;   // tüm lambalar %100 yansaydı çekilecek güç

        public SystemState(bool isNight, TrackedObject? obj, int[] lamps, double powerW, double normalPowerW)
        {
            IsNight = isNight;
            Object = obj;
            Lamps = lamps;
            PowerW = powerW;
            NormalPowerW = normalPowerW;
        }
    }

    public static class KontrolMantigi
    {
        public const int LampCount = 5;

        // ESP32 LEDC PWM ayarları (donanım tarafıyla aynı olmalı)
        public const int PwmFreqHz = 5000;
        public const int PwmBits = 8;
        public const int DutyMax = (1 << PwmBits) - 1; // 8 bit için 255

        // Kural 6: yaya/araç ayrımı hız eşiğiyle
        public static ObjectType ClassifyBySpeed(double speedKmh, ControllerConfig config = null)
        {
            config ??= ControllerConfig.Default;
            return speedKmh > config.PedestrianMaxKmh ? ObjectType.Car : ObjectType.Pedestrian;
        }

        // Kural 4: araç için önde %100 yanacak lamba sayısı ("Aralık / Hız" mantığı)
        static int FullLampsAhead(TrackedObject obj, ControllerConfig config)
        {
            if (obj.Type == ObjectType.Pedestrian) return 0;
            double speedMs = obj.SpeedKmh / 3.6;
            double lamps = Math.Ceiling((speedMs * config.LookaheadSec) / config.LampSpacingM);
            return (int)Math.Min(Math.Max(lamps, 0), LampCount - 1);
        }

        // JavaScript Math.round ile aynı: .5 her zaman yukarı yuvarlanır.
        // (C#'taki Math.Round ve Unity'deki Mathf.Round .5'i çift sayıya yuvarlar: Round(2.5) = 2)
        public static int JsRound(double x) => (int)Math.Floor(x + 0.5);

        public static int[] ComputeLampLevels(bool isNight, TrackedObject? obj, ControllerConfig config = null)
        {
            config ??= ControllerConfig.Default;
            var levels = new int[LampCount];

            // Kural 1 ve 2: gündüz ya da yolda nesne yoksa tüm lambalar kapalı
            if (!isNight || obj == null) return levels;

            var o = obj.Value;
            int current = JsRound(o.Position); // nesnenin en yakın olduğu lamba
            int fullAhead = FullLampsAhead(o, config);

            for (int i = 0; i < LampCount; i++)
            {
                int d = i + 1 - current; // lambanın nesneye göre yeri: + önde, - arkada
                if (d == 0) levels[i] = 100;                  // Kural 3: bulunduğu lamba
                else if (d == -1) levels[i] = 20;             // Kural 5: hemen arkadaki
                else if (d < -1) levels[i] = 0;               // Kural 5: daha gerisi
                else if (d <= fullAhead) levels[i] = 100;     // Kural 4: aracın yakında ulaşacağı lambalar
                else if (d == fullAhead + 1) levels[i] = 40;
                else if (d == fullAhead + 2) levels[i] = 20;
                else levels[i] = 0;
            }
            return levels;
        }

        public static SystemState ComputeState(bool isNight, TrackedObject? obj, ControllerConfig config = null)
        {
            config ??= ControllerConfig.Default;
            int[] lamps = ComputeLampLevels(isNight, obj, config);
            // LED gücü PWM yüzdesiyle yaklaşık doğru orantılı kabul ediliyor
            double powerW = 0;
            foreach (int level in lamps) powerW += (level / 100.0) * config.LampPowerW;
            double normalPowerW = isNight ? LampCount * config.LampPowerW : 0;
            return new SystemState(isNight, obj, lamps, powerW, normalPowerW);
        }

        // Yüzdeyi ESP32 LEDC'nin 8 bitlik duty değerine çevirir (0–255); web'deki PwmPanel ile aynı
        public static int PercentToDuty(int percent) => JsRound((percent / 100.0) * DutyMax);
    }
}
