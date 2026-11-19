using System;
using System.Collections.Generic;

namespace GamePlayDemo
{
    [Serializable]
    public class PhaseDef
    {
        public string id;
        public string name;
        public int startDay;
        public int endDay;
        public int manual;
        public int auto;
        public int background;
        public float basket;
        public float margin;
        public int rent;
        public int truckW;
        public int truckH;
        public int coldH;
        public string[] referenceOrder;
    }

    [Serializable]
    public class ItemDef
    {
        public string id;
        public string name;
        public int w;
        public int h;
        public bool cold;
        public bool buyable;
        public float caseCost;
        public float dayPrice;
        public string[] tags;
        public int nightValue;
    }

    [Serializable]
    public class Ingredient
    {
        public string itemId;
        public int count;
        public Ingredient(string itemId, int count) { this.itemId = itemId; this.count = count; }
    }

    [Serializable]
    public class RecipeDef
    {
        public string id;
        public string name;
        public string category;
        public string station;
        public string clue;
        public string method;
        public string skewer;
        public Ingredient[] inputs;
        public string outputId;
        public int outputCount;
        public float dayPrice;
        public int nightVirtue;
        public string keyEvent;
        public string requiredTool;
        public int unlockRevenue;
        public int unlockDay;
    }

    [Serializable]
    public class DayEventDef
    {
        public string id;
        public string name;
        public string desc;
        public int customerDelta;
        public int cashDelta;
        public int minDay;
    }

    [Serializable]
    public class UpgradeDef
    {
        public string id;
        public string name;
        public string desc;
        public string category;
        public int cost;
        public int unlockDay;
    }

    [Serializable]
    public class NightRequestDef
    {
        public string id;
        public string label;
        public string tag;
        public int desired;
        public int baseVirtue;
        public string requiredItem;
        public bool isKey;
    }

    public static class DemoData
    {
        public static readonly PhaseDef[] Phases = new PhaseDef[]
        {
            new PhaseDef {
                id = "early", name = "前期", startDay = 1, endDay = 10,
                manual = 8, auto = 0, background = 0, basket = 40f, margin = .35f, rent = 60,
                truckW = 5, truckH = 4, coldH = 0,
                referenceOrder = new[] { "noodle", "drink", "snack", "oden", "icepop", "incense" }
            },
            new PhaseDef {
                id = "mid", name = "中期", startDay = 11, endDay = 20,
                manual = 6, auto = 4, background = 10, basket = 40f, margin = .45f, rent = 100,
                truckW = 6, truckH = 5, coldH = 0,
                referenceOrder = new[] { "noodle", "drink", "snack", "oden", "icepop", "incense" }
            },
            new PhaseDef {
                id = "late", name = "后期", startDay = 21, endDay = 30,
                manual = 6, auto = 6, background = 15, basket = 50f, margin = .50f, rent = 150,
                truckW = 8, truckH = 6, coldH = 2,
                referenceOrder = new[] { "noodle", "drink", "oden", "fish", "meat", "incense" }
            },
            new PhaseDef {
                id = "night", name = "夜班", startDay = 31, endDay = 40,
                manual = 6, auto = 8, background = 18, basket = 50f, margin = .52f, rent = 200,
                truckW = 8, truckH = 6, coldH = 2,
                referenceOrder = new[] { "noodle", "drink", "oden", "fish", "meat", "incense" }
            },
            new PhaseDef {
                id = "chain", name = "连锁", startDay = 41, endDay = 50,
                manual = 6, auto = 10, background = 20, basket = 55f, margin = .56f, rent = 220,
                truckW = 8, truckH = 6, coldH = 2,
                referenceOrder = new[] { "noodle", "drink", "oden", "fish", "meat", "incense" }
            },
            new PhaseDef {
                id = "closing", name = "打烊", startDay = 51, endDay = 60,
                manual = 8, auto = 12, background = 24, basket = 60f, margin = .60f, rent = 250,
                truckW = 9, truckH = 7, coldH = 2,
                referenceOrder = new[] { "noodle", "drink", "oden", "fish", "meat", "incense" }
            }
        };

        // 60 天 = 8 个完整周 + 最后 4 天（第 9 段）；周目标按 85% 预期营收。
        public static readonly int[] WeeklyTargets = { 1900, 3000, 4800, 5400, 7100, 7700, 8300, 8700, 5300 };

        public static int GetWeekIndex(int day)
        {
            if (day <= 56) return (day - 1) / 7;
            return 8;
        }

        public static int GetWeekStart(int week)
        {
            return week < 8 ? week * 7 + 1 : 57;
        }

        public static int GetWeekEnd(int week)
        {
            return week < 8 ? (week + 1) * 7 : 60;
        }

        public static int GetWeeklyTarget(int day)
        {
            int w = GetWeekIndex(day);
            if (w < 0) w = 0;
            if (w >= WeeklyTargets.Length) w = WeeklyTargets.Length - 1;
            return WeeklyTargets[w];
        }

        public static readonly ItemDef[] Items = new ItemDef[]
        {
            // 卡车可买的箱/原料
            new ItemDef { id="noodle", name="泡面", w=2,h=2, cold=false, buyable=true, caseCost=65, dayPrice=4.5f, tags=new[]{"warm","memory"}, nightValue=2 },
            new ItemDef { id="drink", name="饮料", w=2,h=3, cold=false, buyable=true, caseCost=54, dayPrice=3.5f, tags=new[]{"drink","sweet"}, nightValue=1 },
            new ItemDef { id="snack", name="零食", w=2,h=2, cold=false, buyable=true, caseCost=60, dayPrice=5.5f, tags=new[]{"sweet"}, nightValue=1 },
            new ItemDef { id="oden", name="关东煮", w=2,h=1, cold=false, buyable=true, caseCost=15, dayPrice=2.5f, tags=new[]{"warm"}, nightValue=2 },
            new ItemDef { id="icepop", name="冰棍", w=1,h=1, cold=false, buyable=true, caseCost=5, dayPrice=2f, tags=new[]{"sweet"}, nightValue=1 },
            new ItemDef { id="incense", name="香/糯米", w=1,h=2, cold=false, buyable=true, caseCost=9, dayPrice=3f, tags=new[]{"memory"}, nightValue=2 },
            new ItemDef { id="flour", name="面粉", w=1,h=2, cold=false, buyable=true, caseCost=20, dayPrice=0f, tags=new[]{"base"}, nightValue=0 },
            new ItemDef { id="egg", name="鸡蛋", w=1,h=1, cold=false, buyable=true, caseCost=15, dayPrice=0f, tags=new[]{"base"}, nightValue=0 },
            new ItemDef { id="sugar", name="糖/黄油", w=1,h=1, cold=false, buyable=true, caseCost=20, dayPrice=0f, tags=new[]{"sweet","base"}, nightValue=0 },
            new ItemDef { id="rice", name="米饭/海苔", w=1,h=2, cold=false, buyable=true, caseCost=10, dayPrice=0f, tags=new[]{"memory","base"}, nightValue=1 },
            new ItemDef { id="paraffin", name="石蜡", w=1,h=1, cold=false, buyable=true, caseCost=20, dayPrice=0f, tags=new[]{"craft"}, nightValue=0 },
            new ItemDef { id="fish", name="鲜鱼", w=2,h=2, cold=true, buyable=true, caseCost=80, dayPrice=16f, tags=new[]{"fresh"}, nightValue=3 },
            new ItemDef { id="meat", name="鲜肉", w=2,h=2, cold=true, buyable=true, caseCost=100, dayPrice=20f, tags=new[]{"fresh"}, nightValue=3 },
            new ItemDef { id="radish", name="萝卜", w=1,h=1, cold=false, buyable=true, caseCost=5, dayPrice=2.5f, tags=new[]{"warm"}, nightValue=1 },
            new ItemDef { id="fishball", name="鱼丸", w=1,h=1, cold=true, buyable=true, caseCost=6, dayPrice=3f, tags=new[]{"warm"}, nightValue=1 },
            new ItemDef { id="tofu", name="豆腐", w=1,h=1, cold=true, buyable=true, caseCost=5, dayPrice=2.5f, tags=new[]{"warm"}, nightValue=1 },

            // 关东煮原料（初始库存，暂不进卡车；后续按阶段解锁采购）
            new ItemDef { id="kelp", name="海带结", w=1,h=1, cold=false, buyable=false, caseCost=0, dayPrice=0f, tags=new[]{"warm","sea"}, nightValue=1 },
            new ItemDef { id="fish_tofu", name="鱼豆腐", w=1,h=1, cold=true, buyable=false, caseCost=0, dayPrice=0f, tags=new[]{"warm"}, nightValue=1 },
            new ItemDef { id="chikuwa", name="竹轮", w=1,h=1, cold=true, buyable=false, caseCost=0, dayPrice=0f, tags=new[]{"warm"}, nightValue=1 },
            new ItemDef { id="konjac", name="魔芋结", w=1,h=1, cold=false, buyable=false, caseCost=0, dayPrice=0f, tags=new[]{"warm"}, nightValue=1 },
            new ItemDef { id="scallion", name="葱", w=1,h=1, cold=false, buyable=false, caseCost=0, dayPrice=0f, tags=new[]{"green"}, nightValue=0 },
            new ItemDef { id="kombu", name="昆布", w=1,h=1, cold=false, buyable=false, caseCost=0, dayPrice=0f, tags=new[]{"sea"}, nightValue=1 },
            new ItemDef { id="soy", name="酱油", w=1,h=1, cold=false, buyable=false, caseCost=0, dayPrice=0f, tags=new[]{"seasoning"}, nightValue=0 },
            new ItemDef { id="chicken_ball", name="鸡肉丸", w=1,h=1, cold=true, buyable=false, caseCost=0, dayPrice=0f, tags=new[]{"meat"}, nightValue=1 },
            new ItemDef { id="shiitake", name="香菇", w=1,h=1, cold=false, buyable=false, caseCost=0, dayPrice=0f, tags=new[]{"mountain"}, nightValue=1 },
            new ItemDef { id="shrimp_ball", name="虾丸", w=1,h=1, cold=true, buyable=false, caseCost=0, dayPrice=0f, tags=new[]{"sea"}, nightValue=1 },
            new ItemDef { id="mochi", name="年糕", w=1,h=1, cold=false, buyable=false, caseCost=0, dayPrice=0f, tags=new[]{"sticky"}, nightValue=1 },
            new ItemDef { id="tofu_skin", name="豆腐皮", w=1,h=1, cold=false, buyable=false, caseCost=0, dayPrice=0f, tags=new[]{"soy"}, nightValue=1 },
            new ItemDef { id="beef", name="牛肉", w=1,h=1, cold=true, buyable=false, caseCost=0, dayPrice=0f, tags=new[]{"meat"}, nightValue=2 },
            new ItemDef { id="tomato", name="番茄", w=1,h=1, cold=false, buyable=false, caseCost=0, dayPrice=0f, tags=new[]{"sour"}, nightValue=1 },
            new ItemDef { id="onion", name="洋葱", w=1,h=1, cold=false, buyable=false, caseCost=0, dayPrice=0f, tags=new[]{"sweet"}, nightValue=1 },
            new ItemDef { id="chili", name="辣椒", w=1,h=1, cold=false, buyable=false, caseCost=0, dayPrice=0f, tags=new[]{"spicy"}, nightValue=0 },
            new ItemDef { id="miso", name="味噌", w=1,h=1, cold=false, buyable=false, caseCost=0, dayPrice=0f, tags=new[]{"seasoning"}, nightValue=0 },
            new ItemDef { id="bonito", name="柴鱼", w=1,h=1, cold=false, buyable=false, caseCost=0, dayPrice=0f, tags=new[]{"sea"}, nightValue=1 },

            // 事件/工具，不在卡车卖
            new ItemDef { id="rib", name="阴日肋骨", w=1,h=1, cold=false, buyable=false, caseCost=0, dayPrice=0f, tags=new[]{"rare"}, nightValue=0 },
            new ItemDef { id="mold", name="蜡烛模具", w=1,h=1, cold=false, buyable=false, caseCost=0, dayPrice=0f, tags=new[]{"tool"}, nightValue=0 },

            // 备货产物：每个关东煮串串单独成菜，不合并成笼统“关东煮”
            new ItemDef { id="oden_haifeng", name="海风萝卜串", w=0,h=0, cold=false, buyable=false, caseCost=0, dayPrice=12f, tags=new[]{"warm"}, nightValue=2 },
            new ItemDef { id="oden_langhua", name="浪花三丸", w=0,h=0, cold=false, buyable=false, caseCost=0, dayPrice=15f, tags=new[]{"warm"}, nightValue=2 },
            new ItemDef { id="oden_baiyu", name="白玉魔芋", w=0,h=0, cold=false, buyable=false, caseCost=0, dayPrice=12f, tags=new[]{"warm"}, nightValue=2 },
            new ItemDef { id="oden_yuejian", name="月见蛋", w=0,h=0, cold=false, buyable=false, caseCost=0, dayPrice=16f, tags=new[]{"warm"}, nightValue=2 },
            new ItemDef { id="oden_shanfeng", name="山风鸡肉丸", w=0,h=0, cold=false, buyable=false, caseCost=0, dayPrice=18f, tags=new[]{"warm"}, nightValue=3 },
            new ItemDef { id="oden_haijuan", name="海卷竹轮", w=0,h=0, cold=false, buyable=false, caseCost=0, dayPrice=16f, tags=new[]{"warm"}, nightValue=3 },
            new ItemDef { id="oden_huowen", name="火纹魔芋", w=0,h=0, cold=false, buyable=false, caseCost=0, dayPrice=14f, tags=new[]{"warm"}, nightValue=2 },
            new ItemDef { id="oden_fudai", name="福袋年糕", w=0,h=0, cold=false, buyable=false, caseCost=0, dayPrice=20f, tags=new[]{"warm"}, nightValue=2 },
            new ItemDef { id="oden_chitang", name="赤汤牛卷", w=0,h=0, cold=false, buyable=false, caseCost=0, dayPrice=24f, tags=new[]{"warm"}, nightValue=3 },
            new ItemDef { id="cake", name="蛋糕", w=0,h=0, cold=false, buyable=false, caseCost=0, dayPrice=16f, tags=new[]{"sweet"}, nightValue=2 },
            new ItemDef { id="bento", name="便当", w=0,h=0, cold=false, buyable=false, caseCost=0, dayPrice=20f, tags=new[]{"memory"}, nightValue=2 },
            new ItemDef { id="candle", name="招魂蜡烛", w=0,h=0, cold=false, buyable=false, caseCost=0, dayPrice=90f, tags=new[]{"key"}, nightValue=0 },
            new ItemDef { id="guide_incense", name="引路香", w=0,h=0, cold=false, buyable=false, caseCost=0, dayPrice=40f, tags=new[]{"key"}, nightValue=0 },
            new ItemDef { id="rice_cake", name="阴米糕", w=0,h=0, cold=false, buyable=false, caseCost=0, dayPrice=20f, tags=new[]{"memory"}, nightValue=2 },
        };

        public static readonly RecipeDef[] Recipes = new RecipeDef[]
        {
            // 关东煮是一个品类；下面是这个品类里的串串线索
            new RecipeDef { id="oden_haifeng", name="海风萝卜串", category="关东煮", station="锅", clue="清甜的白，夹着海的风，在汤里慢慢松开。", method="昆布煮", skewer="直串/夹心", inputs=new[]{ new Ingredient("radish",1), new Ingredient("kelp",1), new Ingredient("fishball",1) }, outputId="oden_haifeng", outputCount=6, dayPrice=12, nightVirtue=2, keyEvent="", requiredTool="", unlockRevenue=0, unlockDay=1 },
            new RecipeDef { id="oden_langhua", name="浪花三丸", category="关东煮", station="锅", clue="三颗圆滚滚，弹牙的浪，一颗接一颗。", method="昆布煮", skewer="三连丸", inputs=new[]{ new Ingredient("fishball",1), new Ingredient("fish_tofu",1), new Ingredient("chikuwa",1) }, outputId="oden_langhua", outputCount=6, dayPrice=15, nightVirtue=2, keyEvent="", requiredTool="", unlockRevenue=1500, unlockDay=5 },
            new RecipeDef { id="oden_baiyu", name="白玉魔芋", category="关东煮", station="锅", clue="软与韧，清白里一点绿。", method="昆布煮", skewer="双拼", inputs=new[]{ new Ingredient("tofu",1), new Ingredient("konjac",1), new Ingredient("scallion",1) }, outputId="oden_baiyu", outputCount=6, dayPrice=12, nightVirtue=2, keyEvent="", requiredTool="", unlockRevenue=3000, unlockDay=10 },
            new RecipeDef { id="oden_yuejian", name="月见蛋", category="关东煮", station="锅", clue="金色的心，藏在褐色外衣里。", method="昆布煮", skewer="直串", inputs=new[]{ new Ingredient("egg",1), new Ingredient("kombu",1), new Ingredient("soy",1) }, outputId="oden_yuejian", outputCount=4, dayPrice=16, nightVirtue=2, keyEvent="", requiredTool="", unlockRevenue=5000, unlockDay=15 },
            new RecipeDef { id="oden_shanfeng", name="山风鸡肉丸", category="关东煮", station="锅", clue="嫩滑的肉里，藏着山里的香。", method="味噌煮", skewer="直串", inputs=new[]{ new Ingredient("chicken_ball",1), new Ingredient("scallion",1), new Ingredient("shiitake",1) }, outputId="oden_shanfeng", outputCount=6, dayPrice=18, nightVirtue=3, keyEvent="", requiredTool="", unlockRevenue=7000, unlockDay=20 },
            new RecipeDef { id="oden_haijuan", name="海卷竹轮", category="关东煮", station="锅", clue="海的味道，卷在竹筒里。", method="昆布煮", skewer="夹心/卷", inputs=new[]{ new Ingredient("chikuwa",1), new Ingredient("shrimp_ball",1), new Ingredient("kelp",1) }, outputId="oden_haijuan", outputCount=5, dayPrice=16, nightVirtue=3, keyEvent="", requiredTool="", unlockRevenue=9000, unlockDay=25 },
            new RecipeDef { id="oden_huowen", name="火纹魔芋", category="关东煮", station="锅", clue="韧的底，火在缝里。", method="辣煮", skewer="花刀", inputs=new[]{ new Ingredient("konjac",1), new Ingredient("chili",1), new Ingredient("miso",1) }, outputId="oden_huowen", outputCount=6, dayPrice=14, nightVirtue=2, keyEvent="", requiredTool="", unlockRevenue=12000, unlockDay=30 },
            new RecipeDef { id="oden_fudai", name="福袋年糕", category="关东煮", station="锅", clue="软糯的白，被金色的袋子抱住。", method="昆布煮", skewer="福袋", inputs=new[]{ new Ingredient("mochi",1), new Ingredient("tofu_skin",1), new Ingredient("kelp",1) }, outputId="oden_fudai", outputCount=4, dayPrice=20, nightVirtue=2, keyEvent="", requiredTool="", unlockRevenue=15000, unlockDay=35 },
            new RecipeDef { id="oden_chitang", name="赤汤牛卷", category="关东煮", station="锅", clue="红色的酸，裹着肉的厚。", method="番茄煮", skewer="卷串", inputs=new[]{ new Ingredient("beef",1), new Ingredient("tomato",1), new Ingredient("onion",1) }, outputId="oden_chitang", outputCount=4, dayPrice=24, nightVirtue=3, keyEvent="", requiredTool="", unlockRevenue=18000, unlockDay=40 },

            new RecipeDef { id="cake", name="蛋糕", category="蛋糕", station="烤箱", clue="柔软的金色，藏着一点甜；烤箱里慢慢鼓起。", method="烤", skewer="", inputs=new[]{ new Ingredient("flour",1), new Ingredient("egg",1), new Ingredient("sugar",1) }, outputId="cake", outputCount=1, dayPrice=16, nightVirtue=0, keyEvent="", requiredTool="", unlockRevenue=0, unlockDay=6 },
            new RecipeDef { id="bento", name="便当", category="便当", station="灶台", clue="米饭的白，压着一块热乎的蛋；盒盖一扣，就是一顿。", method="装盒", skewer="", inputs=new[]{ new Ingredient("rice",1), new Ingredient("egg",1), new Ingredient("scallion",1) }, outputId="bento", outputCount=1, dayPrice=20, nightVirtue=0, keyEvent="", requiredTool="", unlockRevenue=0, unlockDay=9 },
            new RecipeDef { id="candle", name="招魂蜡烛", category="仪式", station="仪式台", clue="一根白色的骨，裹着蜡的泪；火一点，路就亮了。", method="仪式", skewer="", inputs=new[]{ new Ingredient("rib",1), new Ingredient("paraffin",1) }, outputId="candle", outputCount=1, dayPrice=90, nightVirtue=25, keyEvent="寻人鬼", requiredTool="mold", unlockRevenue=0, unlockDay=4 },
            new RecipeDef { id="guide_incense", name="引路香", category="仪式", station="仪式台", clue="灰里的香，混着米的甜；点燃后，迷路的会闻见。", method="晾", skewer="", inputs=new[]{ new Ingredient("incense",1), new Ingredient("rice",1) }, outputId="guide_incense", outputCount=1, dayPrice=40, nightVirtue=10, keyEvent="迷路鬼", requiredTool="", unlockRevenue=0, unlockDay=4 },
            new RecipeDef { id="rice_cake", name="阴米糕", category="蒸笼", station="蒸笼", clue="阴米的黏，糖的甜，香灰的一缕；蒸开后是给饿鬼的软。", method="蒸", skewer="", inputs=new[]{ new Ingredient("rice",1), new Ingredient("sugar",1), new Ingredient("incense",1) }, outputId="rice_cake", outputCount=1, dayPrice=20, nightVirtue=12, keyEvent="饥饿鬼", requiredTool="", unlockRevenue=0, unlockDay=10 },
        };

        public static readonly UpgradeDef[] Upgrades = new UpgradeDef[]
        {
            new UpgradeDef { id="shelf", name="双排货架", desc="卡车/备货容量 +1 排", category="", cost=250, unlockDay=3 },
            new UpgradeDef { id="oden_machine", name="关东煮机", desc="解锁关东煮品类与锅槽", category="关东煮", cost=300, unlockDay=2 },
            new UpgradeDef { id="sign", name="招牌", desc="白天客人 +1/天", category="", cost=350, unlockDay=4 },
            new UpgradeDef { id="ritual_table", name="仪式台", desc="解锁仪式品类（蜡烛/引路香）", category="仪式", cost=350, unlockDay=4 },
            new UpgradeDef { id="cake_machine", name="蛋糕机", desc="解锁蛋糕品类", category="蛋糕", cost=600, unlockDay=6 },
            new UpgradeDef { id="bento_counter", name="便当柜", desc="解锁便当品类", category="便当", cost=800, unlockDay=9 },
            new UpgradeDef { id="steamer", name="蒸笼", desc="解锁蒸笼品类（阴米糕）", category="蒸笼", cost=500, unlockDay=10 },
            new UpgradeDef { id="fish_counter", name="鱼摊/冰柜", desc="解锁鲜鱼进货/售卖，多 1 排冷藏", category="", cost=900, unlockDay=12 },
            new UpgradeDef { id="meat_counter", name="肉摊", desc="解锁鲜肉进货/售卖", category="", cost=1200, unlockDay=16 },
            new UpgradeDef { id="auto_checkout", name="自动收银机", desc="每天 +4 个自动单", category="", cost=2200, unlockDay=18 },
            new UpgradeDef { id="vending", name="自动售货机", desc="每天 +100 被动营业额", category="", cost=1800, unlockDay=22 },
            new UpgradeDef { id="staff", name="兼职员工", desc="补货/清理自动化（后续接）", category="", cost=1500, unlockDay=25 },
            new UpgradeDef { id="cold_storage", name="冷库", desc="冷藏 +2 排", category="", cost=3000, unlockDay=28 },
            new UpgradeDef { id="ledger", name="账本/抽屉", desc="故事物品/账本 UI（后续接）", category="", cost=1500, unlockDay=30 },
            new UpgradeDef { id="second_register", name="第二收银台", desc="高峰可并行 2 位手动客", category="", cost=5000, unlockDay=35 },
            new UpgradeDef { id="ghost_lamp", name="鬼市灯", desc="夜班 +1 单（后续接）", category="", cost=2000, unlockDay=40 }
        };

        public static UpgradeDef GetUpgrade(string id)
        {
            for (int i = 0; i < Upgrades.Length; i++) if (Upgrades[i].id == id) return Upgrades[i];
            return null;
        }

        public static readonly NightRequestDef[] Requests = new NightRequestDef[]
        {
            new NightRequestDef { id="warm", label="暖和", tag="warm", desired=1, baseVirtue=6, requiredItem="", isKey=false },
            new NightRequestDef { id="sweet", label="想甜的", tag="sweet", desired=1, baseVirtue=5, requiredItem="", isKey=false },
            new NightRequestDef { id="memory", label="念想", tag="memory", desired=1, baseVirtue=8, requiredItem="", isKey=false },
            new NightRequestDef { id="group2", label="两个人", tag="group", desired=2, baseVirtue=12, requiredItem="", isKey=false },
            new NightRequestDef { id="group3", label="一家三口", tag="group", desired=3, baseVirtue=18, requiredItem="", isKey=false },
            new NightRequestDef { id="key_candle", label="寻人鬼：要一根招魂蜡烛", tag="key", desired=1, baseVirtue=25, requiredItem="candle", isKey=true },
            new NightRequestDef { id="key_incense", label="迷路鬼：要一束引路香", tag="key", desired=1, baseVirtue=10, requiredItem="guide_incense", isKey=true },
            new NightRequestDef { id="key_ricecake", label="饥饿鬼：要一块阴米糕", tag="key", desired=1, baseVirtue=12, requiredItem="rice_cake", isKey=true },
        };

        public static readonly DayEventDef[] DayEvents = new DayEventDef[]
        {
            new DayEventDef { id="students", name="放学潮", desc="附近学校放学，白天客人 +2。", customerDelta=2, cashDelta=0, minDay=3 },
            new DayEventDef { id="rain", name="暴雨", desc="雨太大，白天客人 -2。", customerDelta=-2, cashDelta=0, minDay=2 },
            new DayEventDef { id="rebate", name="供应商返利", desc="供应商抹零，现金 +50。", customerDelta=0, cashDelta=50, minDay=5 },
            new DayEventDef { id="inspection", name="卫生检查", desc="临时检查，现金 -40。", customerDelta=0, cashDelta=-40, minDay=7 },
            new DayEventDef { id="festival", name="庙会", desc="街口庙会，白天客人 +3。", customerDelta=3, cashDelta=0, minDay=9 },
            new DayEventDef { id="regular", name="熟客带人", desc="熟客带了朋友，客人 +1，现金 +20。", customerDelta=1, cashDelta=20, minDay=11 },
            new DayEventDef { id="outage", name="停电", desc="冰柜跳闸，客人 -1，现金 -20。", customerDelta=-1, cashDelta=-20, minDay=13 },
            new DayEventDef { id="wallet", name="落下的零钱", desc="客人落下零钱，现金 +30。", customerDelta=0, cashDelta=30, minDay=15 },
            new DayEventDef { id="delivery", name="送错货", desc="送错一箱货，现金 -30。", customerDelta=0, cashDelta=-30, minDay=17 },
            new DayEventDef { id="heat", name="热浪", desc="热浪带来冷饮客，客人 +1，现金 +20。", customerDelta=1, cashDelta=20, minDay=19 },
            new DayEventDef { id="ghost", name="夜班灯闪", desc="灯闪了两下，今晚的夜班似乎更“热闹”。", customerDelta=0, cashDelta=0, minDay=21 },
            new DayEventDef { id="landlord", name="房东路过", desc="房东来转了一圈，现金 -10。", customerDelta=0, cashDelta=-10, minDay=23 }
        };

        public static DayEventDef GetDayEvent(string id)
        {
            for (int i = 0; i < DayEvents.Length; i++) if (DayEvents[i].id == id) return DayEvents[i];
            return null;
        }

        public static PhaseDef GetPhase(int day)
        {
            for (int i = 0; i < Phases.Length; i++)
            {
                if (day >= Phases[i].startDay && day <= Phases[i].endDay) return Phases[i];
            }
            return Phases[Phases.Length - 1];
        }

        public static ItemDef GetItem(string id)
        {
            for (int i = 0; i < Items.Length; i++) if (Items[i].id == id) return Items[i];
            return null;
        }

        public static RecipeDef GetRecipe(string id)
        {
            for (int i = 0; i < Recipes.Length; i++) if (Recipes[i].id == id) return Recipes[i];
            return null;
        }

        public static NightRequestDef GetRequest(string id)
        {
            for (int i = 0; i < Requests.Length; i++) if (Requests[i].id == id) return Requests[i];
            return null;
        }
    }
}