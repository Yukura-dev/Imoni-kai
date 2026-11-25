using System;
using System.Numerics;

namespace Imoni.Core.Effects
{
    public sealed class ImoniEffectFrame
    {
        public double PillarWidth, GlowOpacity, BurstOpacity, RainbowDurationSec, SpinDurationSec, Gray;
        public double PotScale, BrothGlow, Flash, ShakeX, ShakeY;
        public double BannerScale, BannerOpacity, FinaleScale, FailDrop, Gauge;
        public bool ShowBanner, Finale, FailFinale, ResultVisible;
    }
    public struct ImoniIngredientPose
    {
        public double X, Y, Rotation, Scale;
    }
    public struct ImoniStarPose
    {
        public double CenterX, CenterY, Size, Opacity, Rotation;
        public int ColorIndex;
    }

    /// <summary>Screen coordinates from IMONI_EFFECT_SPEC; no mock-world +20 px offset.</summary>
    public static class ImoniEffectMath
    {
        public static ImoniEffectFrame Evaluate(ImoniEffectTimeline timeline)
        {
            var c = timeline.Config; var t = timeline.NowMs; var level = timeline.Level;
            var done = timeline.FinaleAtMs.HasValue;
            var fp = done ? t - timeline.FinaleAtMs.Value : -1;
            var gray = done && timeline.Failed ? Clamp01(fp / c.V("grayMs")) : 0;
            var intro = Clamp01(t / c.V("introMs"));
            var lp = (t - timeline.LevelAtMs) / c.V("bannerMs");
            var hitP = (t - timeline.LastHitMs) / c.V("potHitMs");
            var hit = Math.Max(0, 1 - hitP);
            var pw = ((c.V("pillarBase") + c.V("pillarPerLevel") * level) * intro + Math.Sin(t / c.V("pillarWobbleMs")) * c.V("pillarWobble") + (done && !timeline.Failed ? c.V("pillarFinaleAdd") : 0)) * (1 - c.V("pillarGrayReduction") * gray);
            if (done && timeline.Failed) pw += Math.Sin(t / c.V("failFlickerMs")) * c.V("failFlicker") * (1 - gray);
            var flash = Math.Max(0, 1 - (t - timeline.LevelAtMs) / c.V("levelFlashMs")) * c.V("levelFlashOpacity");
            if (done) flash = timeline.Failed ? 0 : Math.Max(flash, Math.Max(0, 1 - fp / c.V("finaleFlashMs")));
            var shake = Math.Max(0, 1 - (t - timeline.LevelAtMs) / c.V("levelShakeMs")) * c.V("levelShake");
            if (level >= c.V("hitShakeMinLevel") && t - timeline.LastHitMs < c.V("hitShakeMs")) shake += c.V("hitShake");
            if (done && fp < c.V("finaleShakeMs")) shake += c.V(timeline.Failed ? "failShake" : "successShake") * (1 - fp / c.V("finaleShakeMs"));
            var bs = lp < c.V("bannerGrowEnd") ? lp / c.V("bannerGrowEnd") * c.V("bannerPeak") : lp < c.V("bannerSettleEnd") ? c.V("bannerPeak") - (lp - c.V("bannerGrowEnd")) * c.V("bannerSettleRate") : 1;
            var fs = 1.0;
            if (done && fp < c.V("finaleGrowMs")) fs = fp / c.V("finaleGrowMs") * c.V("finalePeak");
            else if (done && fp < c.V("finaleGrowMs") + c.V("finaleSettleMs")) fs = c.V("finalePeak") - (fp - c.V("finaleGrowMs")) / c.V("finaleSettleMs") * (c.V("finalePeak") - 1);
            var lo = level == 0 ? BigInteger.Zero : timeline.Thresholds[level - 1];
            var hi = level < timeline.Thresholds.Length ? timeline.Thresholds[level] : timeline.FinalScore;
            var gauge = Percentage(timeline.Score - lo, BigInteger.Max(BigInteger.One, hi - lo));
            return new ImoniEffectFrame
            {
                PillarWidth = Math.Max(0, pw), Gray = gray,
                GlowOpacity = intro * (c.V("glowBase") + c.V("glowPerLevel") * level),
                BurstOpacity = (c.Values("burstOpacity")[level] + (done && !timeline.Failed ? c.V("burstFinaleAdd") : 0)) * (1 - gray),
                RainbowDurationSec = c.Values("rainbowDur")[level], SpinDurationSec = c.Values("spinDur")[level],
                PotScale = 1 + c.V("potHitScale") * hit, BrothGlow = c.V("brothBase") + c.V("brothPerLevel") * level + c.V("brothHit") * hit,
                Flash = flash, ShakeX = Math.Sin(t / c.V("shakeXMs")) * shake, ShakeY = Math.Cos(t / c.V("shakeYMs")) * shake,
                ShowBanner = level > 0 && !done && lp >= 0 && lp < 1,
                BannerScale = bs, BannerOpacity = lp > c.V("bannerFadeStart") ? Math.Max(0, (1 - lp) / (1 - c.V("bannerFadeStart"))) : 1,
                Finale = done && !timeline.Failed, FailFinale = done && timeline.Failed, FinaleScale = fs,
                FailDrop = done ? -c.V("failDropY") * Math.Pow(Math.Max(0, 1 - fp / c.V("failDropMs")), 2) : 0,
                Gauge = done && !timeline.Failed ? 100 : gauge, ResultVisible = timeline.ResultVisible
            };
        }

        public static ImoniIngredientPose Ingredient(ImoniEffectItem item, double elapsedMs, ImoniEffectConfig config)
        {
            var p = Clamp01((elapsedMs - item.SpawnMs) / item.FallMs);
            return new ImoniIngredientPose
            {
                X = config.V("ingredientX") + item.JitterX * (1 - p),
                Y = config.V("ingredientY") + config.V("ingredientTravelY") * (config.V("ingredientLinear") * p + config.V("ingredientQuadratic") * p * p),
                Rotation = item.Rotation + item.Spin * p, Scale = config.V("ingredientScale") - config.V("ingredientScaleDrop") * p
            };
        }

        public static ImoniStarPose Star(ImoniEffectBurst burst, int index, int level, double elapsedMs, ImoniEffectConfig config)
        {
            var q = Clamp01((elapsedMs - burst.AtMs) / config.V(burst.Big ? "bigBurstMs" : "smallBurstMs"));
            double angle, distance, centerY, size, rotation;
            if (burst.Big)
            {
                angle = Math.PI * 2 * index / config.V("bigStarCount");
                distance = config.V("bigStarStartDistance") + config.V("bigStarTravel") * q;
                centerY = config.V("potY") + config.V("bigStarYScale") * Math.Sin(angle) * distance;
                size = config.V("bigStarSize"); rotation = 360 * q;
            }
            else
            {
                var count = SmallStarCount(level, config);
                angle = -Math.PI / 2 + (index - (count - 1) / 2.0) * Math.PI / (count + 1) + burst.Seed % config.V("smallStarSeedRemainder");
                distance = config.V("smallStarStartDistance") + q * (config.V("smallStarTravel") + config.V("smallStarTravelPerLevel") * level);
                centerY = config.V("potY") - config.V("smallStarYInset") + Math.Sin(angle) * distance + config.V("smallStarGravity") * q * q;
                size = config.V("smallStarSize") + config.V("smallStarSizePerLevel") * level; rotation = burst.Seed + config.V("smallStarRotation") * q;
            }
            return new ImoniStarPose { CenterX = burst.X + Math.Cos(angle) * distance, CenterY = centerY, Size = size, Rotation = rotation, Opacity = 1 - q, ColorIndex = (index + (burst.Big ? 0 : (int)Math.Round(burst.Seed))) % (int)config.V("starColors") };
        }

        public static int SmallStarCount(int level, ImoniEffectConfig config) => (int)config.V("smallStarCount") + Math.Min(level, (int)config.V("smallStarCountMaxLevel"));
        public static double RingDiameter(ImoniEffectBurst burst, double elapsedMs, ImoniEffectConfig config) => config.V("ringStart") + config.V("ringTravel") * Clamp01((elapsedMs - burst.AtMs) / config.V("bigBurstMs"));
        private static double Clamp01(double value) => Math.Max(0, Math.Min(1, value));
        private static double Percentage(BigInteger numerator, BigInteger denominator) => (double)BigInteger.Min(1000000, BigInteger.Max(0, numerator * 1000000 / denominator)) / 10000;
    }
}
