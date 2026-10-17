using System;
using System.Collections.Generic;

namespace PAFishPeekPlugin;

public sealed partial class FishPeekPlugin
{
    private sealed class ExactRodFishEntry
    {
        public ExactRodFishEntry(string fishName, float weightOffset)
        {
            FishName = fishName;
            WeightOffset = weightOffset;
        }

        public string FishName { get; }
        public float WeightOffset { get; }
    }

    private sealed class ExactRodProfile
    {
        public ExactRodProfile(
            ulong uniqueUid,
            string assetName,
            string guid,
            int fishingType,
            int accessoryType,
            float rodLevel,
            float rodTier,
            float luck,
            int compatibleSeason,
            int compatibleLocation,
            string modeLabel,
            ExactRodFishEntry[] fishWhitelist)
        {
            UniqueUid = uniqueUid;
            AssetName = assetName;
            Guid = guid;
            FishingType = fishingType;
            AccessoryType = accessoryType;
            RodLevel = rodLevel;
            RodTier = rodTier;
            Luck = luck;
            CompatibleSeason = compatibleSeason;
            CompatibleLocation = compatibleLocation;
            ModeLabel = modeLabel;
            FishWhitelist = fishWhitelist;
        }

        public ulong UniqueUid { get; }
        public string AssetName { get; }
        public string Guid { get; }
        public int FishingType { get; }
        public int AccessoryType { get; }
        public float RodLevel { get; }
        public float RodTier { get; }
        public float Luck { get; }
        public int CompatibleSeason { get; }
        public int CompatibleLocation { get; }
        public string ModeLabel { get; }
        public ExactRodFishEntry[] FishWhitelist { get; }
    }

    private static readonly ExactRodProfile[] BuiltInExactRodProfiles =
    {
        new ExactRodProfile(
            422642321743126528UL,
            "AE_005259_Bamboo_Rod",
            "d1092c42213b4f1479f83d43d19a5c52",
            1,
            1,
            2.000000f,
            1.000000f,
            2.000000f,
            -1,
            63,
            "河边",
            new ExactRodFishEntry[]
            {
                new ExactRodFishEntry("牡丹鱼", 1.000000f),
                new ExactRodFishEntry("蓝眼泪", 1.000000f),
                new ExactRodFishEntry("宝气鱼", 1.000000f),
                new ExactRodFishEntry("飞机头", 1.000000f),
                new ExactRodFishEntry("狼蛛蟹", 1.000000f),
                new ExactRodFishEntry("金蛙", 1.000000f),
                new ExactRodFishEntry("烟花鱼", 1.000000f),
                new ExactRodFishEntry("华丽鳅", 1.000000f),
                new ExactRodFishEntry("拳击虾", 1.000000f),
                new ExactRodFishEntry("幻光鱼", 1.000000f),
                new ExactRodFishEntry("夜明珠", 1.000000f),
                new ExactRodFishEntry("鳅鳅鱼", 1.000000f),
                new ExactRodFishEntry("黑鲤鱼", 1.000000f),
                new ExactRodFishEntry("粗红线", 1.000000f),
                new ExactRodFishEntry("粉丝虾", 1.000000f),
                new ExactRodFishEntry("拟岩鱼", 1.000000f),
                new ExactRodFishEntry("流浪剑客", 1.000000f),
                new ExactRodFishEntry("光斑鱼", 1.000000f),
                new ExactRodFishEntry("大师龟", 1.000000f),
            }),
        new ExactRodProfile(
            422639219812048896UL,
            "AE_005259_Wooden_01_Rod",
            "1e138fc3cdca4d247b509f4e79f3eeee",
            2,
            1,
            1.000000f,
            1.000000f,
            2.000000f,
            -1,
            63,
            "河边",
            new ExactRodFishEntry[]
            {
                new ExactRodFishEntry("蓝眼泪", 1.000000f),
                new ExactRodFishEntry("宝气鱼", 1.000000f),
                new ExactRodFishEntry("飞机头", 1.000000f),
                new ExactRodFishEntry("狼蛛蟹", 1.000000f),
                new ExactRodFishEntry("金蛙", 1.000000f),
                new ExactRodFishEntry("华丽鳅", 1.000000f),
                new ExactRodFishEntry("鳅鳅鱼", 1.000000f),
                new ExactRodFishEntry("黑鲤鱼", 1.000000f),
                new ExactRodFishEntry("粗红线", 1.000000f),
                new ExactRodFishEntry("粉丝虾", 1.000000f),
                new ExactRodFishEntry("大师龟", 1.000000f),
            }),
        new ExactRodProfile(
            422639827365371904UL,
            "AE_005259_Wooden_02_Rod",
            "26289e0ec74edf14e9c1203121edf5d9",
            1,
            1,
            3.000000f,
            1.000000f,
            2.000000f,
            -1,
            63,
            "河边",
            new ExactRodFishEntry[]
            {
                new ExactRodFishEntry("牡丹鱼", 1.000000f),
                new ExactRodFishEntry("甲方满意鱼", 1.000000f),
                new ExactRodFishEntry("蓝眼泪", 1.000000f),
                new ExactRodFishEntry("宝气鱼", 1.000000f),
                new ExactRodFishEntry("飞机头", 1.000000f),
                new ExactRodFishEntry("狼蛛蟹", 1.000000f),
                new ExactRodFishEntry("金蛙", 1.000000f),
                new ExactRodFishEntry("烟花鱼", 1.000000f),
                new ExactRodFishEntry("芭蕾蓬蓬鱼", 1.000000f),
                new ExactRodFishEntry("教练鱼", 1.000000f),
                new ExactRodFishEntry("华丽鳅", 1.000000f),
                new ExactRodFishEntry("拳击虾", 1.000000f),
                new ExactRodFishEntry("幻光鱼", 1.000000f),
                new ExactRodFishEntry("平凡鱼", 1.000000f),
                new ExactRodFishEntry("夜明珠", 1.000000f),
                new ExactRodFishEntry("保洁鱼", 1.000000f),
                new ExactRodFishEntry("鳅鳅鱼", 1.000000f),
                new ExactRodFishEntry("黑鲤鱼", 1.000000f),
                new ExactRodFishEntry("钱袋", 1.000000f),
                new ExactRodFishEntry("粗红线", 1.000000f),
                new ExactRodFishEntry("粉丝虾", 1.000000f),
                new ExactRodFishEntry("拟岩鱼", 1.000000f),
                new ExactRodFishEntry("流浪剑客", 1.000000f),
                new ExactRodFishEntry("戒指", 1.000000f),
                new ExactRodFishEntry("光斑鱼", 1.000000f),
                new ExactRodFishEntry("大师龟", 1.000000f),
            }),
        new ExactRodProfile(
            422638075589140480UL,
            "AE_005266_Bamboo_Rod",
            "407e03f5a537cd141a46fc65b699cae7",
            1,
            2,
            5.000000f,
            1.000000f,
            5.000000f,
            -1,
            63,
            "河边",
            new ExactRodFishEntry[]
            {
                new ExactRodFishEntry("熔岩鱼", 1.000000f),
                new ExactRodFishEntry("鬼刀鱼", 1.000000f),
                new ExactRodFishEntry("呆呆鲶", 1.000000f),
                new ExactRodFishEntry("娃娃鱼", 1.000000f),
                new ExactRodFishEntry("极道鱼", 1.000000f),
                new ExactRodFishEntry("雷鳗", 1.000000f),
                new ExactRodFishEntry("不明化石", 1.000000f),
                new ExactRodFishEntry("美髯公", 1.000000f),
                new ExactRodFishEntry("陶艺鱼", 1.000000f),
                new ExactRodFishEntry("鳄霸鱼", 1.000000f),
                new ExactRodFishEntry("海底豚", 1.000000f),
                new ExactRodFishEntry("发财鲤", 1.000000f),
                new ExactRodFishEntry("果冻鱼", 1.000000f),
                new ExactRodFishEntry("狗头鱼", 1.000000f),
                new ExactRodFishEntry("鬼牙鱼", 1.000000f),
                new ExactRodFishEntry("鬼虎鱼", 1.000000f),
                new ExactRodFishEntry("水法老", 1.000000f),
                new ExactRodFishEntry("辐射鲈", 1.000000f),
                new ExactRodFishEntry("骗子鱼", 1.000000f),
                new ExactRodFishEntry("试剂瓶", 1.000000f),
                new ExactRodFishEntry("冰河鲟", 1.000000f),
                new ExactRodFishEntry("拷贝鱼", 1.000000f),
                new ExactRodFishEntry("地包天鱼", 1.000000f),
                new ExactRodFishEntry("奇怪的东西", 1.000000f),
                new ExactRodFishEntry("黄鸭叫", 1.000000f),
            }),
        new ExactRodProfile(
            407669110798581760UL,
            "AE_005266_Wooden_01_Rod",
            "5f7516523f750784987bc326695299be",
            1,
            2,
            2.000000f,
            1.000000f,
            5.000000f,
            -1,
            63,
            "河边",
            new ExactRodFishEntry[]
            {
                new ExactRodFishEntry("鬼刀鱼", 1.000000f),
                new ExactRodFishEntry("极道鱼", 1.000000f),
                new ExactRodFishEntry("美髯公", 1.000000f),
                new ExactRodFishEntry("陶艺鱼", 1.000000f),
                new ExactRodFishEntry("海底豚", 1.000000f),
                new ExactRodFishEntry("狗头鱼", 1.000000f),
                new ExactRodFishEntry("鬼牙鱼", 1.000000f),
                new ExactRodFishEntry("鬼虎鱼", 1.000000f),
                new ExactRodFishEntry("水法老", 1.000000f),
                new ExactRodFishEntry("辐射鲈", 1.000000f),
                new ExactRodFishEntry("试剂瓶", 1.000000f),
                new ExactRodFishEntry("地包天鱼", 1.000000f),
                new ExactRodFishEntry("黄鸭叫", 1.000000f),
            }),
        new ExactRodProfile(
            408338777283772416UL,
            "AE_005266_Wooden_02_Rod",
            "6f7e093609617b648bba8e8854a47de0",
            1,
            2,
            3.000000f,
            1.000000f,
            5.000000f,
            -1,
            63,
            "河边",
            new ExactRodFishEntry[]
            {
                new ExactRodFishEntry("熔岩鱼", 1.000000f),
                new ExactRodFishEntry("鬼刀鱼", 1.000000f),
                new ExactRodFishEntry("呆呆鲶", 1.000000f),
                new ExactRodFishEntry("极道鱼", 1.000000f),
                new ExactRodFishEntry("美髯公", 1.000000f),
                new ExactRodFishEntry("陶艺鱼", 1.000000f),
                new ExactRodFishEntry("鳄霸鱼", 1.000000f),
                new ExactRodFishEntry("海底豚", 1.000000f),
                new ExactRodFishEntry("狗头鱼", 1.000000f),
                new ExactRodFishEntry("鬼牙鱼", 1.000000f),
                new ExactRodFishEntry("鬼虎鱼", 1.000000f),
                new ExactRodFishEntry("水法老", 1.000000f),
                new ExactRodFishEntry("辐射鲈", 1.000000f),
                new ExactRodFishEntry("骗子鱼", 1.000000f),
                new ExactRodFishEntry("试剂瓶", 1.000000f),
                new ExactRodFishEntry("冰河鲟", 1.000000f),
                new ExactRodFishEntry("拷贝鱼", 1.000000f),
                new ExactRodFishEntry("地包天鱼", 1.000000f),
                new ExactRodFishEntry("黄鸭叫", 1.000000f),
            }),
        new ExactRodProfile(
            408338723957391360UL,
            "AE_005266_Wooden_03_Rod",
            "cb41d0feec89eb0449dc63a6f656b5d0",
            1,
            2,
            4.000000f,
            1.000000f,
            5.000000f,
            -1,
            63,
            "河边",
            new ExactRodFishEntry[]
            {
                new ExactRodFishEntry("熔岩鱼", 1.000000f),
                new ExactRodFishEntry("鬼刀鱼", 1.000000f),
                new ExactRodFishEntry("呆呆鲶", 1.000000f),
                new ExactRodFishEntry("娃娃鱼", 1.000000f),
                new ExactRodFishEntry("极道鱼", 1.000000f),
                new ExactRodFishEntry("美髯公", 1.000000f),
                new ExactRodFishEntry("陶艺鱼", 1.000000f),
                new ExactRodFishEntry("鳄霸鱼", 1.000000f),
                new ExactRodFishEntry("海底豚", 1.000000f),
                new ExactRodFishEntry("发财鲤", 1.000000f),
                new ExactRodFishEntry("果冻鱼", 1.000000f),
                new ExactRodFishEntry("狗头鱼", 1.000000f),
                new ExactRodFishEntry("鬼牙鱼", 1.000000f),
                new ExactRodFishEntry("鬼虎鱼", 1.000000f),
                new ExactRodFishEntry("水法老", 1.000000f),
                new ExactRodFishEntry("辐射鲈", 1.000000f),
                new ExactRodFishEntry("骗子鱼", 1.000000f),
                new ExactRodFishEntry("试剂瓶", 1.000000f),
                new ExactRodFishEntry("冰河鲟", 1.000000f),
                new ExactRodFishEntry("拷贝鱼", 1.000000f),
                new ExactRodFishEntry("地包天鱼", 1.000000f),
                new ExactRodFishEntry("奇怪的东西", 1.000000f),
                new ExactRodFishEntry("黄鸭叫", 1.000000f),
            }),
        new ExactRodProfile(
            448030675934408704UL,
            "AE_005475_01_Rod",
            "a1a099688ef06de4ea3eb7d26dbb17a7",
            1,
            2,
            5.000000f,
            1.000000f,
            6.000000f,
            -1,
            63,
            "河边",
            new ExactRodFishEntry[]
            {
                new ExactRodFishEntry("熔岩鱼", 1.000000f),
                new ExactRodFishEntry("鬼刀鱼", 1.000000f),
                new ExactRodFishEntry("呆呆鲶", 1.000000f),
                new ExactRodFishEntry("娃娃鱼", 1.000000f),
                new ExactRodFishEntry("极道鱼", 1.000000f),
                new ExactRodFishEntry("雷鳗", 1.000000f),
                new ExactRodFishEntry("不明化石", 1.000000f),
                new ExactRodFishEntry("美髯公", 1.000000f),
                new ExactRodFishEntry("陶艺鱼", 1.000000f),
                new ExactRodFishEntry("鳄霸鱼", 1.000000f),
                new ExactRodFishEntry("海底豚", 1.000000f),
                new ExactRodFishEntry("发财鲤", 1.000000f),
                new ExactRodFishEntry("果冻鱼", 1.000000f),
                new ExactRodFishEntry("狗头鱼", 1.000000f),
                new ExactRodFishEntry("鬼牙鱼", 1.000000f),
                new ExactRodFishEntry("鬼虎鱼", 1.000000f),
                new ExactRodFishEntry("水法老", 1.000000f),
                new ExactRodFishEntry("辐射鲈", 1.000000f),
                new ExactRodFishEntry("骗子鱼", 1.000000f),
                new ExactRodFishEntry("试剂瓶", 1.000000f),
                new ExactRodFishEntry("冰河鲟", 1.000000f),
                new ExactRodFishEntry("拷贝鱼", 1.000000f),
                new ExactRodFishEntry("地包天鱼", 1.000000f),
                new ExactRodFishEntry("奇怪的东西", 1.000000f),
                new ExactRodFishEntry("黄鸭叫", 1.000000f),
            }),
        new ExactRodProfile(
            448031783608152064UL,
            "AE_005475_02_Rod",
            "64dcdad6f89347046ae750d5811a441a",
            1,
            1,
            3.000000f,
            1.000000f,
            3.000000f,
            -1,
            63,
            "河边",
            new ExactRodFishEntry[]
            {
                new ExactRodFishEntry("牡丹鱼", 1.000000f),
                new ExactRodFishEntry("甲方满意鱼", 1.000000f),
                new ExactRodFishEntry("蓝眼泪", 1.000000f),
                new ExactRodFishEntry("宝气鱼", 1.000000f),
                new ExactRodFishEntry("飞机头", 1.000000f),
                new ExactRodFishEntry("狼蛛蟹", 1.000000f),
                new ExactRodFishEntry("金蛙", 1.000000f),
                new ExactRodFishEntry("烟花鱼", 1.000000f),
                new ExactRodFishEntry("芭蕾蓬蓬鱼", 1.000000f),
                new ExactRodFishEntry("教练鱼", 1.000000f),
                new ExactRodFishEntry("华丽鳅", 1.000000f),
                new ExactRodFishEntry("拳击虾", 1.000000f),
                new ExactRodFishEntry("幻光鱼", 1.000000f),
                new ExactRodFishEntry("平凡鱼", 1.000000f),
                new ExactRodFishEntry("夜明珠", 1.000000f),
                new ExactRodFishEntry("保洁鱼", 1.000000f),
                new ExactRodFishEntry("鳅鳅鱼", 1.000000f),
                new ExactRodFishEntry("黑鲤鱼", 1.000000f),
                new ExactRodFishEntry("钱袋", 1.000000f),
                new ExactRodFishEntry("粗红线", 1.000000f),
                new ExactRodFishEntry("粉丝虾", 1.000000f),
                new ExactRodFishEntry("拟岩鱼", 1.000000f),
                new ExactRodFishEntry("流浪剑客", 1.000000f),
                new ExactRodFishEntry("戒指", 1.000000f),
                new ExactRodFishEntry("光斑鱼", 1.000000f),
                new ExactRodFishEntry("大师龟", 1.000000f),
            }),
        new ExactRodProfile(
            500424479735091200UL,
            "AE_006045_Rod",
            "9908ea46235fbdf4a830d28b77ae037f",
            1,
            1,
            4.000000f,
            1.000000f,
            3.000000f,
            -1,
            63,
            "河边",
            new ExactRodFishEntry[]
            {
                new ExactRodFishEntry("视觉系", 1.000000f),
                new ExactRodFishEntry("牡丹鱼", 1.000000f),
                new ExactRodFishEntry("甲方满意鱼", 1.000000f),
                new ExactRodFishEntry("蓝眼泪", 1.000000f),
                new ExactRodFishEntry("宝气鱼", 1.000000f),
                new ExactRodFishEntry("飞机头", 1.000000f),
                new ExactRodFishEntry("狼蛛蟹", 1.000000f),
                new ExactRodFishEntry("金蛙", 1.000000f),
                new ExactRodFishEntry("烟花鱼", 1.000000f),
                new ExactRodFishEntry("芭蕾蓬蓬鱼", 1.000000f),
                new ExactRodFishEntry("教练鱼", 1.000000f),
                new ExactRodFishEntry("华丽鳅", 1.000000f),
                new ExactRodFishEntry("拳击虾", 1.000000f),
                new ExactRodFishEntry("幻光鱼", 1.000000f),
                new ExactRodFishEntry("猪鼻鱼", 1.000000f),
                new ExactRodFishEntry("平凡鱼", 1.000000f),
                new ExactRodFishEntry("夜明珠", 1.000000f),
                new ExactRodFishEntry("保洁鱼", 1.000000f),
                new ExactRodFishEntry("鳅鳅鱼", 1.000000f),
                new ExactRodFishEntry("黑鲤鱼", 1.000000f),
                new ExactRodFishEntry("钱袋", 1.000000f),
                new ExactRodFishEntry("粗红线", 1.000000f),
                new ExactRodFishEntry("抱抱蜥", 1.000000f),
                new ExactRodFishEntry("粉丝虾", 1.000000f),
                new ExactRodFishEntry("拟岩鱼", 1.000000f),
                new ExactRodFishEntry("流浪剑客", 1.000000f),
                new ExactRodFishEntry("戒指", 1.000000f),
                new ExactRodFishEntry("光斑鱼", 1.000000f),
                new ExactRodFishEntry("大师龟", 1.000000f),
                new ExactRodFishEntry("冰溜子鱼", 1.000000f),
            }),
        new ExactRodProfile(
            500378720290025472UL,
            "AE_006072_Rod",
            "93fb966ce6abd4d408b009e303db1395",
            1,
            2,
            6.000000f,
            1.000000f,
            6.000000f,
            -1,
            63,
            "河边",
            new ExactRodFishEntry[]
            {
                new ExactRodFishEntry("熔岩鱼", 1.000000f),
                new ExactRodFishEntry("鬼刀鱼", 1.000000f),
                new ExactRodFishEntry("呆呆鲶", 1.000000f),
                new ExactRodFishEntry("娃娃鱼", 1.000000f),
                new ExactRodFishEntry("极道鱼", 1.000000f),
                new ExactRodFishEntry("雷鳗", 1.000000f),
                new ExactRodFishEntry("罗汉鱼", 1.000000f),
                new ExactRodFishEntry("不明化石", 1.000000f),
                new ExactRodFishEntry("美髯公", 1.000000f),
                new ExactRodFishEntry("陶艺鱼", 1.000000f),
                new ExactRodFishEntry("鳄霸鱼", 1.000000f),
                new ExactRodFishEntry("海底豚", 1.000000f),
                new ExactRodFishEntry("发财鲤", 1.000000f),
                new ExactRodFishEntry("果冻鱼", 1.000000f),
                new ExactRodFishEntry("狗头鱼", 1.000000f),
                new ExactRodFishEntry("鬼牙鱼", 1.000000f),
                new ExactRodFishEntry("剑龙鱼", 1.000000f),
                new ExactRodFishEntry("鬼虎鱼", 1.000000f),
                new ExactRodFishEntry("水法老", 1.000000f),
                new ExactRodFishEntry("辐射鲈", 1.000000f),
                new ExactRodFishEntry("骗子鱼", 1.000000f),
                new ExactRodFishEntry("试剂瓶", 1.000000f),
                new ExactRodFishEntry("冰河鲟", 1.000000f),
                new ExactRodFishEntry("拷贝鱼", 1.000000f),
                new ExactRodFishEntry("地包天鱼", 1.000000f),
                new ExactRodFishEntry("奇怪的东西", 1.000000f),
                new ExactRodFishEntry("不明的机械", 1.000000f),
                new ExactRodFishEntry("黄鸭叫", 1.000000f),
            }),
        new ExactRodProfile(
            482930209441169408UL,
            "AE_006096_Rod",
            "686962e61ceca3f4fa76d348ad21d6e0",
            4,
            1,
            1.000000f,
            1.000000f,
            2.000000f,
            8,
            64,
            "冰洞",
            new ExactRodFishEntry[]
            {
                new ExactRodFishEntry("甲方满意鱼", 1.000000f),
                new ExactRodFishEntry("蓝眼泪", 1.000000f),
                new ExactRodFishEntry("宝气鱼", 1.000000f),
                new ExactRodFishEntry("飞机头", 1.000000f),
                new ExactRodFishEntry("狼蛛蟹", 1.000000f),
                new ExactRodFishEntry("金蛙", 1.000000f),
                new ExactRodFishEntry("芭蕾蓬蓬鱼", 1.000000f),
                new ExactRodFishEntry("路易鱼", 1.000000f),
                new ExactRodFishEntry("华丽鳅", 1.000000f),
                new ExactRodFishEntry("拳击虾", 1.000000f),
                new ExactRodFishEntry("泥猴子", 1.000000f),
                new ExactRodFishEntry("幻光鱼", 1.000000f),
                new ExactRodFishEntry("鳅鳅鱼", 1.000000f),
                new ExactRodFishEntry("黑鲤鱼", 1.000000f),
                new ExactRodFishEntry("粗红线", 1.000000f),
                new ExactRodFishEntry("粉丝虾", 1.000000f),
                new ExactRodFishEntry("拟岩鱼", 1.000000f),
                new ExactRodFishEntry("大师龟", 1.000000f),
            }),
        new ExactRodProfile(
            482941174820012032UL,
            "AE_006097_Rod",
            "66c5fce4feac1cf49983f78d392e00e6",
            4,
            2,
            2.000000f,
            1.000000f,
            5.000000f,
            8,
            64,
            "冰洞",
            new ExactRodFishEntry[]
            {
                new ExactRodFishEntry("筷子鳝", 1.000000f),
                new ExactRodFishEntry("鬼刀鱼", 1.000000f),
                new ExactRodFishEntry("七星刀", 1.000000f),
                new ExactRodFishEntry("极道鱼", 1.000000f),
                new ExactRodFishEntry("陶艺鱼", 1.000000f),
                new ExactRodFishEntry("嘟嘟鱼", 1.000000f),
                new ExactRodFishEntry("鬼牙鱼", 1.000000f),
                new ExactRodFishEntry("鬼虎鱼", 1.000000f),
                new ExactRodFishEntry("水法老", 1.000000f),
                new ExactRodFishEntry("辐射鲈", 1.000000f),
                new ExactRodFishEntry("试剂瓶", 1.000000f),
                new ExactRodFishEntry("地包天鱼", 1.000000f),
                new ExactRodFishEntry("黄鸭叫", 1.000000f),
            }),
        new ExactRodProfile(
            543057737782149120UL,
            "AE_006123_Rod",
            "aa46e33ed1310734595e1248fecc5157",
            4,
            2,
            3.000000f,
            1.000000f,
            5.000000f,
            7,
            128,
            "池塘",
            new ExactRodFishEntry[]
            {
                new ExactRodFishEntry("鬼刀鱼", 1.000000f),
                new ExactRodFishEntry("极道鱼", 1.000000f),
                new ExactRodFishEntry("陶艺鱼", 1.000000f),
                new ExactRodFishEntry("鬼牙鱼", 1.000000f),
                new ExactRodFishEntry("鬼虎鱼", 1.000000f),
                new ExactRodFishEntry("水法老", 1.000000f),
                new ExactRodFishEntry("辐射鲈", 1.000000f),
                new ExactRodFishEntry("试剂瓶", 1.000000f),
                new ExactRodFishEntry("地包天鱼", 1.000000f),
                new ExactRodFishEntry("黄鸭叫", 1.000000f),
                new ExactRodFishEntry("嘟嘟鱼", 1.000000f),
                new ExactRodFishEntry("七星刀", 1.000000f),
                new ExactRodFishEntry("筷子鳝", 1.000000f),
                new ExactRodFishEntry("奇虾", 1.000000f),
            }),
        new ExactRodProfile(
            543057548392546304UL,
            "AE_006124_Rod",
            "40769e4d753c0364e999a96fa297f04a",
            4,
            1,
            2.000000f,
            1.000000f,
            2.000000f,
            1,
            128,
            "池塘",
            new ExactRodFishEntry[]
            {
                new ExactRodFishEntry("甲方满意鱼", 1.000000f),
                new ExactRodFishEntry("蓝眼泪", 1.000000f),
                new ExactRodFishEntry("宝气鱼", 1.000000f),
                new ExactRodFishEntry("宇航鱼", 1.000000f),
                new ExactRodFishEntry("飞机头", 1.000000f),
                new ExactRodFishEntry("狼蛛蟹", 1.000000f),
                new ExactRodFishEntry("金蛙", 1.000000f),
                new ExactRodFishEntry("芭蕾蓬蓬鱼", 1.000000f),
                new ExactRodFishEntry("华丽鳅", 1.000000f),
                new ExactRodFishEntry("拳击虾", 1.000000f),
                new ExactRodFishEntry("幻光鱼", 1.000000f),
                new ExactRodFishEntry("鳅鳅鱼", 1.000000f),
                new ExactRodFishEntry("黑鲤鱼", 1.000000f),
                new ExactRodFishEntry("粗红线", 1.000000f),
                new ExactRodFishEntry("阿宝鱼", 1.000000f),
                new ExactRodFishEntry("粉丝虾", 1.000000f),
                new ExactRodFishEntry("拟岩鱼", 1.000000f),
                new ExactRodFishEntry("势利眼", 1.000000f),
                new ExactRodFishEntry("大师龟", 1.000000f),
            }),
        new ExactRodProfile(
            605503497974329344UL,
            "AE_006182_Rod",
            "3476cc02423300d4bacc22353faeb6c2",
            1,
            2,
            6.000000f,
            1.000000f,
            6.000000f,
            -1,
            63,
            "河边",
            new ExactRodFishEntry[]
            {
                new ExactRodFishEntry("熔岩鱼", 1.000000f),
                new ExactRodFishEntry("鬼刀鱼", 1.000000f),
                new ExactRodFishEntry("呆呆鲶", 1.000000f),
                new ExactRodFishEntry("娃娃鱼", 1.000000f),
                new ExactRodFishEntry("极道鱼", 1.000000f),
                new ExactRodFishEntry("雷鳗", 1.000000f),
                new ExactRodFishEntry("罗汉鱼", 1.000000f),
                new ExactRodFishEntry("不明化石", 1.000000f),
                new ExactRodFishEntry("吉利鱼", 1.000000f),
                new ExactRodFishEntry("美髯公", 1.000000f),
                new ExactRodFishEntry("河马", 1.000000f),
                new ExactRodFishEntry("陶艺鱼", 1.000000f),
                new ExactRodFishEntry("鳄霸鱼", 1.000000f),
                new ExactRodFishEntry("海底豚", 1.000000f),
                new ExactRodFishEntry("发财鲤", 1.000000f),
                new ExactRodFishEntry("榴莲龟", 1.000000f),
                new ExactRodFishEntry("果冻鱼", 1.000000f),
                new ExactRodFishEntry("狗头鱼", 1.000000f),
                new ExactRodFishEntry("鬼牙鱼", 1.000000f),
                new ExactRodFishEntry("剑龙鱼", 1.000000f),
                new ExactRodFishEntry("鬼虎鱼", 1.000000f),
                new ExactRodFishEntry("水法老", 1.000000f),
                new ExactRodFishEntry("辐射鲈", 1.000000f),
                new ExactRodFishEntry("骗子鱼", 1.000000f),
                new ExactRodFishEntry("试剂瓶", 1.000000f),
                new ExactRodFishEntry("冰河鲟", 1.000000f),
                new ExactRodFishEntry("拷贝鱼", 1.000000f),
                new ExactRodFishEntry("地包天鱼", 1.000000f),
                new ExactRodFishEntry("奇怪的东西", 1.000000f),
                new ExactRodFishEntry("不明的机械", 1.000000f),
                new ExactRodFishEntry("黄鸭叫", 1.000000f),
                new ExactRodFishEntry("流星鱼", 1.000000f),
            }),
        new ExactRodProfile(
            604737477932249088UL,
            "AE_006183_Rod",
            "8f78c797b977379489aca74d62f3ba9d",
            4,
            1,
            2.000000f,
            1.000000f,
            2.000000f,
            6,
            128,
            "池塘",
            new ExactRodFishEntry[]
            {
                new ExactRodFishEntry("甲方满意鱼", 1.000000f),
                new ExactRodFishEntry("蓝眼泪", 1.000000f),
                new ExactRodFishEntry("宝气鱼", 1.000000f),
                new ExactRodFishEntry("宇航鱼", 1.000000f),
                new ExactRodFishEntry("天线鱼", 1.000000f),
                new ExactRodFishEntry("飞机头", 1.000000f),
                new ExactRodFishEntry("狼蛛蟹", 1.000000f),
                new ExactRodFishEntry("刃鱼", 1.000000f),
                new ExactRodFishEntry("金蛙", 1.000000f),
                new ExactRodFishEntry("芭蕾蓬蓬鱼", 1.000000f),
                new ExactRodFishEntry("华丽鳅", 1.000000f),
                new ExactRodFishEntry("拳击虾", 1.000000f),
                new ExactRodFishEntry("幻光鱼", 1.000000f),
                new ExactRodFishEntry("鳅鳅鱼", 1.000000f),
                new ExactRodFishEntry("黑鲤鱼", 1.000000f),
                new ExactRodFishEntry("粗红线", 1.000000f),
                new ExactRodFishEntry("粉丝虾", 1.000000f),
                new ExactRodFishEntry("拟岩鱼", 1.000000f),
                new ExactRodFishEntry("大师龟", 1.000000f),
                new ExactRodFishEntry("路易鱼", 1.000000f),
            }),
    };

    private static readonly Dictionary<ulong, ExactRodProfile> BuiltInExactRodProfilesByUid = BuildBuiltInExactRodProfilesByUid();
    private static readonly Dictionary<string, ExactRodProfile> BuiltInExactRodProfilesByGuid = BuildBuiltInExactRodProfilesByGuid();
    private static readonly Dictionary<string, ExactRodProfile> BuiltInExactRodProfilesByAssetName = BuildBuiltInExactRodProfilesByAssetName();

    private static Dictionary<ulong, ExactRodProfile> BuildBuiltInExactRodProfilesByUid()
    {
        var map = new Dictionary<ulong, ExactRodProfile>();
        foreach (ExactRodProfile profile in BuiltInExactRodProfiles)
        {
            if (profile.UniqueUid != 0)
            {
                map[profile.UniqueUid] = profile;
            }
        }

        return map;
    }

    private static Dictionary<string, ExactRodProfile> BuildBuiltInExactRodProfilesByGuid()
    {
        var map = new Dictionary<string, ExactRodProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (ExactRodProfile profile in BuiltInExactRodProfiles)
        {
            if (!string.IsNullOrWhiteSpace(profile.Guid))
            {
                map[profile.Guid] = profile;
            }
        }

        return map;
    }

    private static Dictionary<string, ExactRodProfile> BuildBuiltInExactRodProfilesByAssetName()
    {
        var map = new Dictionary<string, ExactRodProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (ExactRodProfile profile in BuiltInExactRodProfiles)
        {
            if (!string.IsNullOrWhiteSpace(profile.AssetName))
            {
                map[profile.AssetName] = profile;
            }
        }

        return map;
    }

    private static bool TryGetBuiltInExactRodProfile(ulong uniqueUid, string? guid, string? assetName, out ExactRodProfile? profile)
    {
        profile = null;
        if (uniqueUid != 0 && BuiltInExactRodProfilesByUid.TryGetValue(uniqueUid, out profile))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(guid) && BuiltInExactRodProfilesByGuid.TryGetValue(guid.Trim(), out profile))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(assetName) && BuiltInExactRodProfilesByAssetName.TryGetValue(assetName.Trim(), out profile))
        {
            return true;
        }

        return false;
    }
}

