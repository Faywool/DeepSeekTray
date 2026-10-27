using System;
using System.Collections.Generic;
using DeepSeekTray.Models;

namespace DeepSeekTray.Services;

/// <summary>红色 &lt; 黄色 &lt; 绿色，数值只用于“取最紧急”的比较。</summary>
public enum TierLevel
{
    Unknown = 0,
    Red = 1,
    Yellow = 2,
    Green = 3,
}

/// <summary>纯函数的分级逻辑，方便自检。</summary>
public static class BalanceTier
{
    public static TierLevel Classify(decimal total, TierSettings tiers)
    {
        if (total >= tiers.GreenFrom)
        {
            return TierLevel.Green;
        }

        if (total >= tiers.YellowFrom)
        {
            return TierLevel.Yellow;
        }

        return TierLevel.Red;
    }

    public static string HexOf(TierLevel level, TierSettings tiers) => level switch
    {
        TierLevel.Green => tiers.GreenColor,
        TierLevel.Yellow => tiers.YellowColor,
        TierLevel.Red => tiers.RedColor,
        _ => "#9CA3AF",
    };

    /// <summary>多个账号时取最紧急的一档（红 &gt; 黄 &gt; 绿），全部未知则 Unknown。</summary>
    public static TierLevel Worst(IEnumerable<TierLevel> levels)
    {
        var result = TierLevel.Unknown;

        foreach (var level in levels)
        {
            if (level == TierLevel.Unknown)
            {
                continue;
            }

            if (result == TierLevel.Unknown || level < result)
            {
                result = level;
            }
        }

        return result;
    }
}
