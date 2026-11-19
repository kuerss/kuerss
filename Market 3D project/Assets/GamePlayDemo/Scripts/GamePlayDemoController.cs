using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace GamePlayDemo
{
    public class GamePlayDemoController : MonoBehaviour
    {
        [Header("Optional font override")]
        public Font uiFont;

        Canvas canvas;
        Font font;
        Text topText;
        Text topWeekText;
        Text logText;
        RectTransform contentRoot;
        GameObject dayPanel, truckPanel, prepPanel, nightPanel;
        GameObject weekPanel;
        GameObject shopPanel;
        Text shopInfo;
        RectTransform shopList;
        Button closeShopButton;
        Text weekText;
        Button continueWeekButton;

        Text dayInfo, truckInfo, prepInfo, nightInfo, truckHint, prepHint, nightHint;
        Text orderInfo, receiptText, productLabel, barcodeHint;
        Button serveButton, serveAllButton, endDayButton, taoistButton;
        Button rotateLeftButton, rotateRightButton, scanButton, payButton, nextCustomerButton;
        Button autoTruckButton, doneTruckButton;
        Button craftButton, donePrepButton;
        Button dealButton, nextNightButton, doneNightButton;
        RectTransform truckGridRoot, truckItemList, prepRecipeList, prepIngredientList, methodRoot, offerList, productRt;
        Image productImage, scanZone;
        GameObject productCard, barcodeGo;

        readonly List<ItemDef> orderItems = new List<ItemDef>();
        readonly List<int> orderQty = new List<int>();
        readonly List<string> receiptLines = new List<string>();
        bool orderActive;
        int productIndex;
        float productAngle;
        float receiptTotal;

        readonly Dictionary<string, int> inv = new Dictionary<string, int>();
        readonly Dictionary<string, int> prepared = new Dictionary<string, int>();
        readonly List<string> offer = new List<string>();
        readonly List<NightRequestDef> nightOrders = new List<NightRequestDef>();
        readonly List<string> logs = new List<string>();
        readonly Dictionary<string, Image> truckItemImages = new Dictionary<string, Image>();
        readonly Dictionary<string, Image> offerImages = new Dictionary<string, Image>();
        readonly List<Button> gridCells = new List<Button>();
        readonly List<Text> gridTexts = new List<Text>();

        int day = 1;
        float cash = 700f;
        int virtue = 0;
        int customersLeft = 0;
        int customersTotal = 0;
        int coldRows = 0;
        float totalRevenue = 0f;
        DayEventDef dayEvent;
        float dayRevenue = 0f;
        int weekIndex = 0;
        int weekTarget = 1900;
        float weekRevenue = 0f;
        float rentMultiplier = 1f;
        int badWeekStreak = 0;
        bool weekPending = false;
        bool endingShown = false;
        bool dayActive = false;
        bool taoistSold = false;
        PhaseDef phase;
        string[,] truckCells;
        string selectedTruckItem;
        int truckW, truckH;

        RecipeDef activeRecipe;
        readonly List<string> prepSlots = new List<string>();
        readonly Text[] prepSlotTexts = new Text[3];
        readonly Button[] prepSlotButtons = new Button[3];
        readonly Dictionary<string, Button> methodButtons = new Dictionary<string, Button>();
        readonly Dictionary<string, Button> categoryButtons = new Dictionary<string, Button>();
        readonly HashSet<string> unlocked = new HashSet<string>();
        readonly HashSet<string> upgrades = new HashSet<string>();
        string selectedMethod = "";
        string prepCategory = "关东煮";
        static readonly string[] PrepCategories = { "关东煮", "蛋糕", "便当", "仪式", "蒸笼" };
        static readonly string[] OdenIngredients = {
            "radish","kelp","fishball","fish_tofu","chikuwa","tofu","konjac","scallion","egg","kombu","soy",
            "chicken_ball","shiitake","shrimp_ball","mochi","tofu_skin","beef","tomato","onion","chili","miso"
        };

        int nightIndex = 0;

        void Start()
        {
            BuildUI();
            InitInventory();
            StartDay();
        }

        // ---------------- UI construction ----------------

        void BuildUI()
        {
            font = GetFont();

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(transform, false);
            }

            // background
            MakePanel(canvas.transform, "BG", new Color(.06f, .07f, .09f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // top bar
            var top = MakePanel(canvas.transform, "Top", new Color(.10f, .12f, .15f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -56), Vector2.zero);
            topText = MakeText(top.transform, "TopText", "", 18, TextAnchor.MiddleLeft, Color.white, new Vector2(20, 8), new Vector2(1000, 40));
            topWeekText = MakeText(top.transform, "TopWeek", "", 15, TextAnchor.MiddleRight, new Color(.82f, .86f, .9f), new Vector2(1000, 8), new Vector2(580, 40));
            topWeekText.gameObject.SetActive(false);

            // tabs
            float tx = 20f;
            MakeTab("白天", ref tx, () => ShowPanel(dayPanel));
            MakeTab("进货", ref tx, () => ShowPanel(truckPanel));
            MakeTab("备货", ref tx, OpenPrep);
            MakeTab("夜班", ref tx, () => ShowPanel(nightPanel));
            MakeTab("升级", ref tx, OpenShop);
            MakeButton(canvas.transform, "跳过阶段", () => DebugSkipPhase(), new Vector2(1380, 62), new Vector2(180, 36));

            // content
            var content = MakePanel(canvas.transform, "Content", new Color(.09f, .10f, .12f), new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 80), new Vector2(0, -112));
            contentRoot = content.GetComponent<RectTransform>();

            BuildDayPanel();
            BuildTruckPanel();
            BuildPrepPanel();
            BuildNightPanel();

            shopPanel = MakePanel(contentRoot, "ShopPanel", new Color(.10f, .12f, .15f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            shopInfo = MakeText(shopPanel.transform, "ShopInfo", "", 17, TextAnchor.UpperLeft, Color.white, new Vector2(20, 16), new Vector2(1400, 60));
            var shopListGo = MakePanel(shopPanel.transform, "ShopList", new Color(.07f, .08f, .10f), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Place(shopListGo.GetComponent<RectTransform>(), new Vector2(20, 90), new Vector2(1400, 520));
            shopList = shopListGo.GetComponent<RectTransform>();
            var svlg = shopListGo.AddComponent<VerticalLayoutGroup>();
            svlg.spacing = 3; svlg.childControlHeight = false; svlg.childForceExpandHeight = false;
            svlg.childControlWidth = true; svlg.childForceExpandWidth = true;
            closeShopButton = MakeButton(shopPanel.transform, "关闭升级 → 去备货", CloseShop, new Vector2(20, 628), new Vector2(260, 44));
            shopPanel.SetActive(false);

            weekPanel = MakePanel(contentRoot, "WeekPanel", new Color(.04f, .05f, .07f, 1f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            weekText = MakeText(weekPanel.transform, "WeekText", "", 19, TextAnchor.UpperLeft, Color.white, new Vector2(70, 55), new Vector2(1460, 470));
            continueWeekButton = MakeButton(weekPanel.transform, "继续", ContinueWeek, new Vector2(650, 590), new Vector2(300, 52));
            weekPanel.SetActive(false);

            // log
            var log = MakePanel(canvas.transform, "Log", new Color(.05f, .06f, .07f), new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 80));
            logText = MakeText(log.transform, "LogText", "", 13, TextAnchor.UpperLeft, new Color(.75f, .8f, .85f), new Vector2(16, 8), new Vector2(1568, 64));

            ShowPanel(dayPanel);
        }

        void MakeTab(string label, ref float x, Action action)
        {
            MakeButton(canvas.transform, label, action, new Vector2(x, 62), new Vector2(110, 36));
            x += 118f;
        }

        void BuildDayPanel()
        {
            dayPanel = MakePanel(contentRoot, "DayPanel", new Color(.11f, .13f, .16f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dayInfo = MakeText(dayPanel.transform, "DayInfo", "", 18, TextAnchor.UpperLeft, Color.white, new Vector2(20, 14), new Vector2(1200, 62));

            orderInfo = MakeText(dayPanel.transform, "OrderInfo", "", 16, TextAnchor.UpperLeft, Color.white, new Vector2(20, 80), new Vector2(430, 210));
            receiptText = MakeText(dayPanel.transform, "Receipt", "", 15, TextAnchor.UpperLeft, new Color(.8f, .85f, .9f), new Vector2(20, 300), new Vector2(430, 230));

            // product card
            productCard = MakePanel(dayPanel.transform, "ProductCard", new Color(.22f, .26f, .30f), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            productRt = productCard.GetComponent<RectTransform>();
            Place(productRt, new Vector2(560, 100), new Vector2(240, 260));
            productRt.pivot = new Vector2(.5f, .5f);
            productRt.anchoredPosition = new Vector2(560 + 120, -(100 + 130));
            productImage = productCard.GetComponent<Image>();
            productImage.raycastTarget = true;
            var drag = productCard.AddComponent<ProductDrag>();
            drag.onDrag = RotateProduct;

            productLabel = MakeText(productCard.transform, "ProductLabel", "", 26, TextAnchor.MiddleCenter, Color.white, Vector2.zero, new Vector2(240, 260));
            productLabel.rectTransform.anchorMin = Vector2.zero;
            productLabel.rectTransform.anchorMax = Vector2.one;
            productLabel.rectTransform.offsetMin = Vector2.zero;
            productLabel.rectTransform.offsetMax = Vector2.zero;

            barcodeGo = MakePanel(productCard.transform, "Barcode", Color.white, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Place(barcodeGo.GetComponent<RectTransform>(), new Vector2(30, 218), new Vector2(180, 26));
            var bars = MakeText(barcodeGo.transform, "Bars", "||||||||||||", 18, TextAnchor.MiddleCenter, Color.black, Vector2.zero, new Vector2(180, 26));
            bars.rectTransform.anchorMin = Vector2.zero;
            bars.rectTransform.anchorMax = Vector2.one;
            bars.rectTransform.offsetMin = Vector2.zero;
            bars.rectTransform.offsetMax = Vector2.zero;
            barcodeGo.SetActive(false);

            // scan zone under the card
            var zoneGo = MakePanel(dayPanel.transform, "ScanZone", new Color(.25f, .28f, .32f), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Place(zoneGo.GetComponent<RectTransform>(), new Vector2(520, 390), new Vector2(540, 24));
            scanZone = zoneGo.GetComponent<Image>();
            zoneGo.SetActive(false);
            var zoneText = MakeText(zoneGo.transform, "ZoneText", "扫描区：把条形码转到下方并摆正", 12, TextAnchor.MiddleCenter, new Color(.8f, .85f, .9f), Vector2.zero, new Vector2(540, 24));
            zoneText.rectTransform.anchorMin = Vector2.zero;
            zoneText.rectTransform.anchorMax = Vector2.one;
            zoneText.rectTransform.offsetMin = Vector2.zero;
            zoneText.rectTransform.offsetMax = Vector2.zero;

            barcodeHint = MakeText(dayPanel.transform, "BarcodeHint", "", 14, TextAnchor.MiddleLeft, new Color(.8f, .85f, .9f), new Vector2(520, 420), new Vector2(540, 32));

            rotateLeftButton = MakeButton(dayPanel.transform, "左转 15°", () => RotateProduct(15f), new Vector2(520, 462), new Vector2(110, 40));
            rotateRightButton = MakeButton(dayPanel.transform, "右转 15°", () => RotateProduct(-15f), new Vector2(638, 462), new Vector2(110, 40));
            scanButton = MakeButton(dayPanel.transform, "扫码", ScanProduct, new Vector2(756, 462), new Vector2(140, 40));
            payButton = MakeButton(dayPanel.transform, "收款 / 完成本单", PayOrder, new Vector2(904, 462), new Vector2(160, 40));
            nextCustomerButton = MakeButton(dayPanel.transform, "下一位", NextCustomer, new Vector2(1072, 462), new Vector2(120, 40));

            serveButton = MakeButton(dayPanel.transform, "接待下一位（扫码）", StartOrder, new Vector2(20, 560), new Vector2(210, 42));
            serveAllButton = MakeButton(dayPanel.transform, "快速跳过白天（测试）", ServeAll, new Vector2(238, 560), new Vector2(230, 42));
            endDayButton = MakeButton(dayPanel.transform, "结束白天 → 进货", EndDay, new Vector2(476, 560), new Vector2(260, 42));
            taoistButton = MakeButton(dayPanel.transform, "道士：卖招魂蜡烛 +90", SellTaoist, new Vector2(744, 560), new Vector2(300, 42));

            productCard.SetActive(false);
            scanButton.interactable = false;
            payButton.interactable = false;
            nextCustomerButton.interactable = false;
            SetScanControls(false);
            nextCustomerButton.gameObject.SetActive(false);
        }

        void SetScanControls(bool show)
        {
            if (rotateLeftButton != null) rotateLeftButton.gameObject.SetActive(show);
            if (rotateRightButton != null) rotateRightButton.gameObject.SetActive(show);
            if (scanButton != null) scanButton.gameObject.SetActive(show);
            if (payButton != null) payButton.gameObject.SetActive(show);
        }

        void BuildTruckPanel()
        {
            truckPanel = MakePanel(contentRoot, "TruckPanel", new Color(.11f, .13f, .16f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            truckInfo = MakeText(truckPanel.transform, "TruckInfo", "", 17, TextAnchor.UpperLeft, Color.white, new Vector2(20, 16), new Vector2(1300, 64));

            var listGo = MakePanel(truckPanel.transform, "TruckItems", new Color(.08f, .09f, .11f), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Place(listGo.GetComponent<RectTransform>(), new Vector2(20, 88), new Vector2(360, 620));
            truckItemList = listGo.GetComponent<RectTransform>();
            var vlg = listGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 3; vlg.childControlHeight = false; vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;

            var gridGo = new GameObject("TruckGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGo.transform.SetParent(truckPanel.transform, false);
            truckGridRoot = gridGo.GetComponent<RectTransform>();
            Place(truckGridRoot, new Vector2(400, 88), new Vector2(520, 380));
            var grid = gridGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(46, 46);
            grid.spacing = new Vector2(4, 4);
            grid.padding = new RectOffset(4, 4, 4, 4);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;

            autoTruckButton = MakeButton(truckPanel.transform, "按参考订单装车", AutoPack, new Vector2(400, 500), new Vector2(220, 44));
            doneTruckButton = MakeButton(truckPanel.transform, "完成进货 → 备货", DoneTruck, new Vector2(640, 500), new Vector2(220, 44));
            truckHint = MakeText(truckPanel.transform, "TruckHint", "", 14, TextAnchor.UpperLeft, new Color(.7f, .75f, .8f), new Vector2(400, 556), new Vector2(1000, 100));
        }

        void BuildPrepPanel()
        {
            prepPanel = MakePanel(contentRoot, "PrepPanel", new Color(.11f, .13f, .16f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            prepInfo = MakeText(prepPanel.transform, "PrepInfo", "", 17, TextAnchor.UpperLeft, Color.white, new Vector2(20, 16), new Vector2(1300, 60));

            // category row: 先确定品类，再出线索
            for (int i = 0; i < PrepCategories.Length; i++)
            {
                string cat = PrepCategories[i];
                var btn = MakeButton(prepPanel.transform, cat, () => SelectCategory(cat), new Vector2(20 + i * 125, 78), new Vector2(115, 30));
                categoryButtons[cat] = btn;
                btn.GetComponentInChildren<Text>().fontSize = 13;
            }

            var listGo = MakePanel(prepPanel.transform, "PrepRecipes", new Color(.08f, .09f, .11f), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Place(listGo.GetComponent<RectTransform>(), new Vector2(20, 118), new Vector2(400, 290));
            prepRecipeList = listGo.GetComponent<RectTransform>();
            var rgrid = listGo.AddComponent<GridLayoutGroup>();
            rgrid.cellSize = new Vector2(195, 54);
            rgrid.spacing = new Vector2(2, 2);
            rgrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            rgrid.constraintCount = 2;

            var ingGo = MakePanel(prepPanel.transform, "PrepIngredients", new Color(.08f, .09f, .11f), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Place(ingGo.GetComponent<RectTransform>(), new Vector2(20, 418), new Vector2(400, 200));
            prepIngredientList = ingGo.GetComponent<RectTransform>();
            var igrid = ingGo.AddComponent<GridLayoutGroup>();
            igrid.cellSize = new Vector2(95, 30);
            igrid.spacing = new Vector2(3, 3);
            igrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            igrid.constraintCount = 4;

            // three pot slots, no timing
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var slot = MakeButton(prepPanel.transform, "空", () => ClearPrepSlot(idx), new Vector2(450 + i * 150, 118), new Vector2(130, 110));
                prepSlotButtons[i] = slot;
                prepSlotTexts[i] = slot.GetComponentInChildren<Text>();
                prepSlotTexts[i].fontSize = 16;
            }

            // method row is rebuilt per category
            var mroot = MakePanel(prepPanel.transform, "PrepMethods", new Color(0, 0, 0, 0), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Place(mroot.GetComponent<RectTransform>(), new Vector2(450, 243), new Vector2(500, 36));
            mroot.GetComponent<Image>().raycastTarget = false;
            methodRoot = mroot.GetComponent<RectTransform>();

            craftButton = MakeButton(prepPanel.transform, "试做 / 下锅", CraftButton, new Vector2(450, 292), new Vector2(220, 48));
            donePrepButton = MakeButton(prepPanel.transform, "完成备货 → 夜班", DonePrep, new Vector2(690, 292), new Vector2(220, 48));
            prepHint = MakeText(prepPanel.transform, "PrepHint", "", 14, TextAnchor.UpperLeft, new Color(.7f, .75f, .8f), new Vector2(450, 348), new Vector2(1000, 220));
            craftButton.interactable = false;

            RefreshCategoryButtons();
            RefreshCategoryColors();
            BuildMethodButtons();
            BuildPrepRecipes();
            BuildPrepIngredients();
            UpdatePrepInfo();
        }

        void BuildNightPanel()
        {
            nightPanel = MakePanel(contentRoot, "NightPanel", new Color(.11f, .13f, .16f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            nightInfo = MakeText(nightPanel.transform, "NightInfo", "", 18, TextAnchor.UpperLeft, Color.white, new Vector2(20, 16), new Vector2(1200, 80));

            var listGo = MakePanel(nightPanel.transform, "OfferList", new Color(.08f, .09f, .11f), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Place(listGo.GetComponent<RectTransform>(), new Vector2(20, 110), new Vector2(420, 400));
            offerList = listGo.GetComponent<RectTransform>();
            var glg = listGo.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(200, 28);
            glg.spacing = new Vector2(3, 3);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 2;

            dealButton = MakeButton(nightPanel.transform, "成交", Deal, new Vector2(20, 530), new Vector2(200, 48));
            nextNightButton = MakeButton(nightPanel.transform, "下一位", NextNight, new Vector2(240, 530), new Vector2(200, 48));
            doneNightButton = MakeButton(nightPanel.transform, "结束夜晚 → 下一天", FinishNight, new Vector2(460, 530), new Vector2(260, 48));
            nightHint = MakeText(nightPanel.transform, "NightHint", "", 14, TextAnchor.UpperLeft, new Color(.7f, .75f, .8f), new Vector2(470, 110), new Vector2(1000, 380));
        }

        // ---------------- UI helpers ----------------

        Font GetFont()
        {
            if (uiFont != null) return uiFont;
            try
            {
                var f = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 18);
                if (f != null) return f;
            }
            catch { }
            var builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtin == null) builtin = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return builtin;
        }

        GameObject MakePanel(Transform parent, string name, Color color, Vector2 amin, Vector2 amax, Vector2 omin, Vector2 omax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = amin; rt.anchorMax = amax;
            rt.offsetMin = omin; rt.offsetMax = omax;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return go;
        }

        Image MakeImage(Transform parent, string name, Color color, Vector2 amin, Vector2 amax, Vector2 omin, Vector2 omax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = amin; rt.anchorMax = amax;
            rt.offsetMin = omin; rt.offsetMax = omax;
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        Text MakeText(Transform parent, string name, string text, int size, TextAnchor anchor, Color color, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            Place(rt, pos, sizeDelta);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        Button MakeButton(Transform parent, string label, Action onClick, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            Place(rt, pos, size);
            var img = go.GetComponent<Image>();
            img.color = new Color(.18f, .24f, .30f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var t = MakeText(go.transform, "Label", label, 15, TextAnchor.MiddleCenter, Color.white, Vector2.zero, size);
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = Vector2.zero;
            t.rectTransform.offsetMax = Vector2.zero;
            t.raycastTarget = false;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(pos.x, -pos.y);
            rt.sizeDelta = size;
        }

        void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject);
        }

        // ---------------- state ----------------

        void InitInventory()
        {
            AddInv("noodle", 10); AddInv("drink", 10); AddInv("snack", 5);
            AddInv("oden", 10); AddInv("icepop", 5); AddInv("incense", 5);
            AddInv("radish", 5); AddInv("fishball", 5); AddInv("tofu", 5);
            AddInv("kelp", 3); AddInv("fish_tofu", 3); AddInv("chikuwa", 3);
            AddInv("konjac", 3); AddInv("scallion", 3); AddInv("kombu", 3);
            AddInv("soy", 3); AddInv("chicken_ball", 3); AddInv("shiitake", 3);
            AddInv("shrimp_ball", 3); AddInv("mochi", 3); AddInv("tofu_skin", 3);
            AddInv("beef", 3); AddInv("tomato", 3); AddInv("onion", 3);
            AddInv("chili", 3); AddInv("miso", 3); AddInv("bonito", 3);
            AddInv("flour", 2); AddInv("egg", 2); AddInv("sugar", 2);
            AddInv("rice", 2); AddInv("paraffin", 2);
            AddInv("rib", 1); AddInv("mold", 1);
        }

        int GetInv(string id) { int v; return inv.TryGetValue(id, out v) ? v : 0; }
        int GetPrep(string id) { int v; return prepared.TryGetValue(id, out v) ? v : 0; }
        void AddInv(string id, int n) { if (!inv.ContainsKey(id)) inv[id] = 0; inv[id] += n; }
        void AddPrep(string id, int n) { if (!prepared.ContainsKey(id)) prepared[id] = 0; prepared[id] += n; }

        void StartDay()
        {
            phase = DemoData.GetPhase(day);
            weekIndex = DemoData.GetWeekIndex(day);
            weekTarget = DemoData.GetWeeklyTarget(day);
            customersLeft = phase.manual + phase.auto + phase.background;
            if (HasUpgrade("sign")) customersLeft += 1;
            if (HasUpgrade("auto_checkout")) customersLeft += 4;
            dayEvent = PickDayEvent();
            if (dayEvent != null)
            {
                customersLeft += dayEvent.customerDelta;
                if (customersLeft < 1) customersLeft = 1;
                cash += dayEvent.cashDelta;
            }
            customersTotal = customersLeft;
            dayRevenue = 0f;
            dayActive = true;
            taoistSold = false;
            orderActive = false;
            productIndex = 0;
            receiptTotal = 0f;
            orderItems.Clear();
            orderQty.Clear();
            receiptLines.Clear();
            productCard.SetActive(false);
            scanZone.gameObject.SetActive(false);
            rotateLeftButton.interactable = false;
            rotateRightButton.interactable = false;
            scanButton.interactable = false;
            payButton.interactable = false;
            nextCustomerButton.interactable = false;
            serveButton.interactable = true;
            serveAllButton.interactable = true;
            SetScanControls(false);
            nextCustomerButton.gameObject.SetActive(false);
            barcodeHint.text = "";
            orderInfo.text = (dayEvent != null ? "今日事件：" + dayEvent.name + " · " + dayEvent.desc + "\n" : "") +
                "点“接待下一位（扫码）”：货品随机角度；把条形码转到扫描区，逐件扫码，扫完收款。";
            receiptText.text = "";
            ShowPanel(dayPanel);
            RefreshTop();
            UpdateDayInfo();
            UpdateTaoistButton();
            Log("第 " + day + " 天 · " + phase.name + "：今天 " + customersLeft + " 位客人。");
            if (dayEvent != null) Log("今日事件：" + dayEvent.name + "（" + dayEvent.desc + "）");
        }

        DayEventDef PickDayEvent()
        {
            var pool = new List<DayEventDef>();
            for (int i = 0; i < DemoData.DayEvents.Length; i++)
                if (day >= DemoData.DayEvents[i].minDay) pool.Add(DemoData.DayEvents[i]);
            if (pool.Count == 0) return null;
            if (UnityEngine.Random.value > .35f) return null;
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        void ServeCustomer()
        {
            if (!dayActive || orderActive || customersLeft <= 0) return;
            float sale = phase.basket;
            string sold = "";
            string[] dayItems = { "oden_haifeng", "oden_langhua", "oden_baiyu", "oden_yuejian", "oden_shanfeng", "oden_haijuan", "oden_huowen", "oden_fudai", "oden_chitang", "cake", "bento" };
            for (int i = 0; i < dayItems.Length; i++)
            {
                if (GetPrep(dayItems[i]) > 0)
                {
                    prepared[dayItems[i]] -= 1;
                    var item = DemoData.GetItem(dayItems[i]);
                    sale += item != null ? item.dayPrice : 0f;
                    sold = "，卖出" + (item != null ? item.name : dayItems[i]);
                    break;
                }
            }
            cash += sale;
            dayRevenue += sale;
            customersLeft--;
            Log("快速接待一位：+" + sale.ToString("0") + sold);
            RefreshTop();
            UpdateDayInfo();
            UpdateTaoistButton();
            if (customersLeft <= 0) Log("白天客人接待完，点“结束白天 → 进货”。");
        }

        void ServeAll()
        {
            if (orderActive) { Log("先完成当前这单（扫码/收款）。"); return; }
            int guard = 0;
            while (customersLeft > 0 && guard++ < 100) ServeCustomer();
        }

        void StartOrder()
        {
            if (!dayActive || orderActive || customersLeft <= 0) return;
            orderItems.Clear();
            orderQty.Clear();
            int count = UnityEngine.Random.Range(3, 6);
            int guard = 0;
            while (orderItems.Count < count && guard++ < 30)
            {
                var item = PickSellableItem();
                if (item == null) break;
                if (orderItems.Contains(item)) continue;
                orderItems.Add(item);
                orderQty.Add(GetPrep(item.id) > 0 ? 1 : UnityEngine.Random.Range(2, 5));
            }
            if (orderItems.Count == 0)
            {
                var fallback = DemoData.GetItem("noodle");
                if (fallback != null) { orderItems.Add(fallback); orderQty.Add(1); }
            }
            orderActive = true;
            productIndex = 0;
            receiptTotal = 0f;
            receiptLines.Clear();
            serveButton.interactable = false;
            serveAllButton.interactable = false;
            payButton.interactable = false;
            nextCustomerButton.interactable = false;
            scanButton.interactable = false;
            productCard.SetActive(true);
            scanZone.gameObject.SetActive(true);
            rotateLeftButton.interactable = true;
            rotateRightButton.interactable = true;
            SetScanControls(true);
            nextCustomerButton.gameObject.SetActive(false);
            ShowProduct();
            UpdateReceipt();
            Log("客人来结账：" + orderItems.Count + " 件，先摆正条形码再扫码。");
        }

        ItemDef PickSellableItem()
        {
            var pool = new List<ItemDef>();
            for (int i = 0; i < DemoData.Items.Length; i++)
            {
                var item = DemoData.Items[i];
                if (item.dayPrice <= 0f) continue;
                bool key = false;
                for (int t = 0; t < item.tags.Length; t++) if (item.tags[t] == "key") key = true;
                if (key) continue;
                if (GetInv(item.id) <= 0 && GetPrep(item.id) <= 0) continue;
                pool.Add(item);
            }
            if (pool.Count == 0) return null;
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        float RandomProductAngle()
        {
            float[] angles = { 90f, 180f, -90f, 45f, -45f, 135f, -135f };
            return angles[UnityEngine.Random.Range(0, angles.Length)];
        }

        void ShowProduct()
        {
            if (!orderActive) return;
            if (productIndex >= orderItems.Count)
            {
                productCard.SetActive(false);
                scanZone.gameObject.SetActive(false);
                rotateLeftButton.interactable = false;
                rotateRightButton.interactable = false;
                scanButton.interactable = false;
                payButton.interactable = true;
                nextCustomerButton.interactable = false;
                barcodeHint.text = "本单已扫完，点“收款 / 完成本单”。";
                orderInfo.text = "客人订单 " + orderItems.Count + " 件\n已全部扫码\n合计 ¥" + receiptTotal.ToString("0");
                return;
            }
            var item = orderItems[productIndex];
            int qty = orderQty[productIndex];
            productCard.SetActive(true);
            productLabel.text = item.name + " x" + qty;
            productAngle = RandomProductAngle();
            UpdateProductVisual();
            payButton.interactable = false;
            nextCustomerButton.interactable = false;
        }

        void UpdateProductVisual()
        {
            if (!orderActive || productIndex >= orderItems.Count) return;
            var item = orderItems[productIndex];
            int qty = orderQty[productIndex];
            float d = Mathf.Abs(Mathf.DeltaAngle(0f, productAngle));
            bool aligned = d <= 12f;
            productRt.localEulerAngles = new Vector3(0f, 0f, productAngle);
            productImage.color = aligned ? new Color(.20f, .32f, .24f) : new Color(.22f, .26f, .30f);
            barcodeGo.SetActive(aligned);
            scanZone.color = aligned ? new Color(.25f, .62f, .38f) : new Color(.25f, .28f, .32f);
            scanButton.interactable = aligned;
            scanButton.GetComponent<Image>().color = aligned ? new Color(.25f, .55f, .35f) : new Color(.18f, .24f, .30f);
            productLabel.text = item.name + " x" + qty + (aligned ? "" : (d > 90f ? "（背面）" : "（侧面）"));
            barcodeHint.text = aligned
                ? "条形码对准扫描区：点“扫码”。"
                : "把条形码转到下方并摆正；用左右转或拖动货品。";
            orderInfo.text = "客人订单 " + (productIndex + 1) + "/" + orderItems.Count +
                "\n当前：" + item.name + " x" + qty + "  ¥" + (item.dayPrice * qty).ToString("0.0") +
                "\n已扫：" + receiptTotal.ToString("0") + " 元";
        }

        void RotateProduct(float delta)
        {
            if (!orderActive || productIndex >= orderItems.Count) return;
            productAngle = Mathf.Repeat(productAngle + delta + 180f, 360f) - 180f;
            UpdateProductVisual();
        }

        void ScanProduct()
        {
            if (!orderActive || productIndex >= orderItems.Count) return;
            float d = Mathf.Abs(Mathf.DeltaAngle(0f, productAngle));
            if (d > 12f) { Log("条形码没对准，继续转。"); return; }
            var item = orderItems[productIndex];
            int qty = orderQty[productIndex];
            receiptTotal += item.dayPrice * qty;
            receiptLines.Add(item.name + " x" + qty + "  ¥" + (item.dayPrice * qty).ToString("0.0"));
            productIndex++;
            Log("扫码：" + item.name + " x" + qty + " +" + (item.dayPrice * qty).ToString("0.0") + " 元");
            UpdateReceipt();
            ShowProduct();
        }

        void UpdateReceipt()
        {
            string s = "已扫：\n";
            if (receiptLines.Count == 0) s += "（还没扫）\n";
            for (int i = 0; i < receiptLines.Count; i++) s += receiptLines[i] + "\n";
            s += "\n合计 ¥" + receiptTotal.ToString("0");
            receiptText.text = s;
        }

        void PayOrder()
        {
            if (!orderActive) return;
            if (productIndex < orderItems.Count) { Log("还有商品没扫码。"); return; }
            cash += receiptTotal;
            dayRevenue += receiptTotal;
            for (int i = 0; i < orderItems.Count; i++)
                if (GetPrep(orderItems[i].id) > 0) prepared[orderItems[i].id] -= orderQty[i];
            customersLeft--;
            orderActive = false;
            productCard.SetActive(false);
            scanZone.gameObject.SetActive(false);
            rotateLeftButton.interactable = false;
            rotateRightButton.interactable = false;
            scanButton.interactable = false;
            payButton.interactable = false;
            nextCustomerButton.interactable = customersLeft > 0;
            serveButton.interactable = customersLeft > 0;
            serveAllButton.interactable = true;
            barcodeHint.text = "";
            orderInfo.text = "本单完成，收款 ¥" + receiptTotal.ToString("0");
            SetScanControls(false);
            nextCustomerButton.gameObject.SetActive(customersLeft > 0);
            Log("收款 +" + receiptTotal.ToString("0") + " 元。");
            RefreshTop();
            UpdateDayInfo();
            UpdateTaoistButton();
            if (customersLeft <= 0) Log("白天客人接待完，点“结束白天 → 进货”。");
            else Log("本单完成，点“下一位”。");
        }

        void NextCustomer()
        {
            if (!dayActive || orderActive || customersLeft <= 0) return;
            StartOrder();
        }

        void EndDay()
        {
            if (!dayActive) return;
            if (orderActive) { Log("先完成当前这单（扫码/收款）。"); return; }
            if (HasUpgrade("vending"))
            {
                dayRevenue += 100f;
                cash += 100f;
                Log("自动售货机被动收入 +100。");
            }
            cash -= phase.rent * rentMultiplier;
            dayActive = false;
            Log("房租 -" + (phase.rent * rentMultiplier).ToString("0") +
                (rentMultiplier != 1f ? "（x" + rentMultiplier.ToString("0.00") + "）" : "") +
                "；现金 " + cash.ToString("0") + "。");
            InitTruck();
        }

        void SellTaoist()
        {
            if (taoistSold) return;
            if (GetPrep("candle") <= 0) { Log("道士来了，但你没有招魂蜡烛。"); return; }
            prepared["candle"] -= 1;
            cash += 90f;
            taoistSold = true;
            Log("道士买走一根招魂蜡烛：+90 现金。");
            RefreshTop();
            UpdateTaoistButton();
        }

        void UpdateTaoistButton()
        {
            bool show = dayActive && day % 7 == 4 && !taoistSold && GetPrep("candle") > 0;
            taoistButton.gameObject.SetActive(show);
        }

        void UpdateDayInfo()
        {
            dayInfo.text = "第 " + day + " 天 · " + phase.name +
                "    客人剩余 " + customersLeft + " / " + customersTotal +
                "    今日营业额 " + dayRevenue.ToString("0") +
                "    现金 " + cash.ToString("0") + "    阴德 " + virtue + "\n" +
                "手动 " + phase.manual + " / 自动 " + phase.auto + " / 背景 " + phase.background +
                "    客单价 " + phase.basket.ToString("0") + "    房租 " + phase.rent + "\n" +
                "W" + (weekIndex + 1) + " 本周营收 " + weekRevenue.ToString("0") + " / " + weekTarget +
                "    剩余 " + (DemoData.GetWeekEnd(weekIndex) - day + 1) + " 天" +
                "    累计营收 " + totalRevenue.ToString("0");
        }

        // ---------------- truck ----------------

        void InitTruck()
        {
            phase = DemoData.GetPhase(day);
            truckW = phase.truckW + (HasUpgrade("shelf") ? 1 : 0);
            truckH = phase.truckH;
            coldRows = phase.coldH;
            if (HasUpgrade("fish_counter")) coldRows = Mathf.Max(coldRows, 1);
            if (HasUpgrade("meat_counter")) coldRows = Mathf.Max(coldRows, 1);
            if (HasUpgrade("cold_storage")) coldRows += 1;
            if (coldRows > truckH) coldRows = truckH;
            truckCells = new string[truckW, truckH];
            selectedTruckItem = null;
            activeRecipe = null;
            prepSlots.Clear();
            selectedMethod = "";
            ShowPanel(truckPanel);
            BuildTruckGrid();
            BuildTruckItems();
            UpdateTruckInfo();
            Log("18:20 卡车：车厢 " + truckW + "x" + truckH + "，先装明天白天的货。");
        }

        void BuildTruckGrid()
        {
            ClearChildren(truckGridRoot);
            gridCells.Clear();
            gridTexts.Clear();
            var grid = truckGridRoot.GetComponent<GridLayoutGroup>();
            grid.constraintCount = truckW;
            for (int y = 0; y < truckH; y++)
            {
                for (int x = 0; x < truckW; x++)
                {
                    int cx = x, cy = y;
                    var btn = MakeButton(truckGridRoot, "", () => OnTruckCell(cx, cy), Vector2.zero, new Vector2(46, 46));
                    var img = btn.GetComponent<Image>();
                    bool cold = coldRows > 0 && y >= truckH - coldRows;
                    img.color = cold ? new Color(.16f, .30f, .42f) : new Color(.20f, .23f, .27f);
                    var txt = btn.GetComponentInChildren<Text>();
                    txt.fontSize = 12;
                    txt.text = "";
                    gridCells.Add(btn);
                    gridTexts.Add(txt);
                }
            }
        }

        void BuildTruckItems()
        {
            ClearChildren(truckItemList);
            truckItemImages.Clear();
            for (int i = 0; i < DemoData.Items.Length; i++)
            {
                var item = DemoData.Items[i];
                if (!item.buyable) continue;
                if (item.id == "fish" && !HasUpgrade("fish_counter")) continue;
                if (item.id == "meat" && !HasUpgrade("meat_counter")) continue;
                if (item.cold && coldRows <= 0) continue;
                string id = item.id;
                var btn = MakeButton(truckItemList, item.name + "  " + item.w + "x" + item.h + "  ¥" + item.caseCost.ToString("0"),
                    () => SelectTruckItem(id), Vector2.zero, new Vector2(350, 30));
                var le = btn.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 30;
                le.preferredWidth = 350;
                truckItemImages[id] = btn.GetComponent<Image>();
            }
            RefreshTruckItemColors();
        }

        void SelectTruckItem(string id)
        {
            selectedTruckItem = id;
            RefreshTruckItemColors();
            UpdateTruckInfo();
        }

        void RefreshTruckItemColors()
        {
            foreach (var kv in truckItemImages)
            {
                bool sel = kv.Key == selectedTruckItem;
                kv.Value.color = sel ? new Color(.55f, .45f, .15f) : new Color(.18f, .24f, .30f);
            }
        }

        void OnTruckCell(int x, int y)
        {
            if (string.IsNullOrEmpty(selectedTruckItem))
            {
                Log("先在左边选一个箱子。");
                return;
            }
            var item = DemoData.GetItem(selectedTruckItem);
            if (item == null) return;
            if (!CanPlace(item, x, y)) return;
            PlaceItem(item, x, y);
        }

        bool CanPlace(ItemDef item, int x, int y, bool quiet = false)
        {
            if (x < 0 || y < 0 || x + item.w > truckW || y + item.h > truckH) { if (!quiet) Log("放不下：超出车厢。"); return false; }
            if (item.cold && (coldRows <= 0 || y < truckH - coldRows)) { if (!quiet) Log("鲜货必须放冷藏区（底部蓝格）。"); return false; }
            for (int yy = y; yy < y + item.h; yy++)
                for (int xx = x; xx < x + item.w; xx++)
                    if (!string.IsNullOrEmpty(truckCells[xx, yy])) { if (!quiet) Log("这个位置被占了。"); return false; }
            if (cash < item.caseCost) { if (!quiet) Log("现金不够。"); return false; }
            return true;
        }

        void PlaceItem(ItemDef item, int x, int y, bool quiet = false)
        {
            for (int yy = y; yy < y + item.h; yy++)
                for (int xx = x; xx < x + item.w; xx++)
                    truckCells[xx, yy] = item.id;
            cash -= item.caseCost;
            AddInv(item.id, 10);
            if (!quiet) Log("装车：" + item.name + " 一箱（10 件），-" + item.caseCost.ToString("0"));
            selectedTruckItem = null;
            RefreshTruckItemColors();
            UpdateTruckCells();
            UpdateTruckInfo();
            RefreshTop();
        }

        void UpdateTruckCells()
        {
            for (int y = 0; y < truckH; y++)
            {
                for (int x = 0; x < truckW; x++)
                {
                    int idx = y * truckW + x;
                    if (idx >= gridTexts.Count) continue;
                    string id = truckCells[x, y];
                    var item = string.IsNullOrEmpty(id) ? null : DemoData.GetItem(id);
                    gridTexts[idx].text = item != null ? item.name : "";
                    var img = gridCells[idx].GetComponent<Image>();
                    bool cold = coldRows > 0 && y >= truckH - coldRows;
                    img.color = item != null
                        ? (item.cold ? new Color(.22f, .48f, .38f) : new Color(.28f, .40f, .28f))
                        : (cold ? new Color(.16f, .30f, .42f) : new Color(.20f, .23f, .27f));
                }
            }
        }

        void AutoPack()
        {
            var order = phase.referenceOrder;
            for (int i = 0; i < order.Length; i++)
            {
                var item = DemoData.GetItem(order[i]);
                if (item == null) continue;
                bool placed = false;
                for (int y = 0; y < truckH && !placed; y++)
                {
                    for (int x = 0; x < truckW && !placed; x++)
                    {
                        if (CanPlace(item, x, y, true)) { PlaceItem(item, x, y, true); placed = true; }
                    }
                }
                if (!placed) Log("参考订单装不下：" + item.name);
            }
            Log("按参考订单装车完成。");
            UpdateTruckInfo();
        }

        void UpdateTruckInfo()
        {
            int used = 0;
            for (int y = 0; y < truckH; y++)
                for (int x = 0; x < truckW; x++)
                    if (!string.IsNullOrEmpty(truckCells[x, y])) used++;
            string sel = string.IsNullOrEmpty(selectedTruckItem) ? "未选" : (DemoData.GetItem(selectedTruckItem) != null ? DemoData.GetItem(selectedTruckItem).name : selectedTruckItem);
            truckInfo.text = "18:20 卡车 · " + phase.name + " · 车厢 " + truckW + "x" + truckH +
                " · 已占 " + used + " 格 · 现金 " + cash.ToString("0") + " · 当前选择：" + sel;
            truckHint.text = "点左侧箱子 → 点车厢格放置；箱子按 10 件入库。鲜鱼/鲜肉只能放底部冷藏区。\n" +
                "“按参考订单装车”是保底；装不下就少带，第二天缺货少赚。";
        }

        void DoneTruck()
        {
            if (day == 1)
            {
                ShowPanel(nightPanel);
                StartNight();
                Log("第一天不备货：先进夜班，第二天再买设备。");
                return;
            }
            if (day == 2 && !HasUpgrade("oden_machine"))
            {
                OpenShop();
                shopInfo.text = "第二天：先买“关东煮机”（¥300），备货里才会出现关东煮线索。";
                Log("第二天：先升级关东煮机，再备货。");
                return;
            }
            OpenPrep();
            Log("进货完成，进入备货。");
        }

        // ---------------- upgrades / shop ----------------

        bool HasUpgrade(string id) { return upgrades.Contains(id); }

        string CategoryUpgrade(string cat)
        {
            if (cat == "关东煮") return "oden_machine";
            if (cat == "蛋糕") return "cake_machine";
            if (cat == "便当") return "bento_counter";
            if (cat == "仪式") return "ritual_table";
            if (cat == "蒸笼") return "steamer";
            return "";
        }

        bool IsCategoryUnlocked(string cat)
        {
            string id = CategoryUpgrade(cat);
            return !string.IsNullOrEmpty(id) && HasUpgrade(id);
        }

        string FirstUnlockedCategory()
        {
            for (int i = 0; i < PrepCategories.Length; i++)
                if (IsCategoryUnlocked(PrepCategories[i])) return PrepCategories[i];
            return "";
        }

        void RefreshCategoryButtons()
        {
            foreach (var kv in categoryButtons)
                kv.Value.gameObject.SetActive(IsCategoryUnlocked(kv.Key));
        }

        void OpenPrep()
        {
            if (string.IsNullOrEmpty(prepCategory) || !IsCategoryUnlocked(prepCategory))
                prepCategory = FirstUnlockedCategory();
            if (string.IsNullOrEmpty(prepCategory))
            {
                OpenShop();
                Log("还没有备货设备：先升级关东煮机。");
                return;
            }
            ShowPanel(prepPanel);
            RefreshCategoryButtons();
            BuildMethodButtons();
            BuildPrepRecipes();
            BuildPrepIngredients();
            UpdatePrepInfo();
        }

        void OpenShop()
        {
            ShowPanel(shopPanel);
            BuildShopRows();
            RefreshTop();
        }

        void CloseShop()
        {
            OpenPrep();
        }

        void BuildShopRows()
        {
            ClearChildren(shopList);
            int owned = 0;
            for (int i = 0; i < DemoData.Upgrades.Length; i++)
            {
                var def = DemoData.Upgrades[i];
                string id = def.id;
                bool has = HasUpgrade(id);
                if (has) owned++;
                string status = has ? "[已购买]" : day < def.unlockDay ? "第 " + def.unlockDay + " 天解锁" : "¥" + def.cost;
                var btn = MakeButton(shopList, def.name + " · " + status + " · " + def.desc,
                    () => BuyUpgrade(id), Vector2.zero, new Vector2(1380, 30));
                var le = btn.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 30;
                le.preferredWidth = 1380;
                btn.GetComponentInChildren<Text>().fontSize = 12;
                if (has) btn.GetComponent<Image>().color = new Color(.14f, .22f, .18f);
                else if (day < def.unlockDay) btn.GetComponent<Image>().color = new Color(.14f, .15f, .18f);
            }
            shopInfo.text = "店铺升级 · 现金 ¥" + cash.ToString("0") + " · 已购买 " + owned + "/" + DemoData.Upgrades.Length +
                " · 点条目购买；关东煮机第二天必买，仪式台首周解锁。";
        }

        void BuyUpgrade(string id)
        {
            var def = DemoData.GetUpgrade(id);
            if (def == null) return;
            if (HasUpgrade(id)) { Log("已经买过了。"); return; }
            if (day < def.unlockDay) { Log("第 " + def.unlockDay + " 天才解锁。" ); return; }
            if (cash < def.cost) { Log("现金不够，需要 ¥" + def.cost + "。"); return; }
            cash -= def.cost;
            upgrades.Add(id);
            Log("升级：" + def.name + " -¥" + def.cost + "。");
            RefreshTop();
            if (!string.IsNullOrEmpty(def.category))
            {
                prepCategory = def.category;
                RefreshCategoryButtons();
                OpenPrep();
            }
            else
            {
                BuildShopRows();
            }
        }


        void BuildPrepRecipes()
        {
            ClearChildren(prepRecipeList);
            if (!IsCategoryUnlocked(prepCategory)) return;
            for (int i = 0; i < DemoData.Recipes.Length; i++)
            {
                var r = DemoData.Recipes[i];
                if (r.category != prepCategory) continue;
                string id = r.id;
                bool known = unlocked.Contains(id);
                if (!known && (totalRevenue < r.unlockRevenue || day < r.unlockDay)) continue;
                if (!known && !HasAllIngredients(r)) continue;
                string label = known ? "[已解锁] " + r.name : "?? " + r.clue;
                var btn = MakeButton(prepRecipeList, label, () => SelectRecipe(id), Vector2.zero, new Vector2(195, 54));
                btn.GetComponentInChildren<Text>().fontSize = 10;
            }
        }

        bool HasAllIngredients(RecipeDef r)
        {
            for (int i = 0; i < r.inputs.Length; i++)
                if (GetInv(r.inputs[i].itemId) < r.inputs[i].count) return false;
            if (!string.IsNullOrEmpty(r.requiredTool) && GetInv(r.requiredTool) <= 0) return false;
            return true;
        }

        void SelectCategory(string cat)
        {
            if (!IsCategoryUnlocked(cat)) { Log("这个品类还没解锁，先去升级。"); return; }
            prepCategory = cat;
            activeRecipe = null;
            prepSlots.Clear();
            BuildMethodButtons();
            BuildPrepRecipes();
            BuildPrepIngredients();
            UpdatePrepInfo();
            RefreshCategoryColors();
            Log("品类：" + cat + "。选一条线索，再选原料和手法。");
        }

        void RefreshCategoryColors()
        {
            foreach (var kv in categoryButtons)
                kv.Value.GetComponent<Image>().color = kv.Key == prepCategory
                    ? new Color(.55f, .45f, .15f)
                    : new Color(.18f, .24f, .30f);
        }

        static string[] MethodsForCategory(string cat)
        {
            if (cat == "关东煮") return new[] { "昆布煮", "味噌煮", "辣煮", "番茄煮" };
            if (cat == "蛋糕") return new[] { "烤" };
            if (cat == "便当") return new[] { "装盒" };
            if (cat == "仪式") return new[] { "仪式", "晾" };
            if (cat == "蒸笼") return new[] { "蒸" };
            return new[] { "串煮" };
        }

        static string[] IngredientsForCategory(string cat)
        {
            if (cat == "关东煮") return OdenIngredients;
            if (cat == "蛋糕") return new[] { "flour", "egg", "sugar" };
            if (cat == "便当") return new[] { "rice", "fish", "egg" };
            if (cat == "仪式") return new[] { "rib", "paraffin", "mold", "incense", "rice" };
            if (cat == "蒸笼") return new[] { "rice", "sugar", "incense" };
            return new string[0];
        }

        void BuildMethodButtons()
        {
            ClearChildren(methodRoot);
            methodButtons.Clear();
            selectedMethod = "";
            if (!IsCategoryUnlocked(prepCategory)) return;
            string[] methods = MethodsForCategory(prepCategory);
            for (int i = 0; i < methods.Length; i++)
            {
                string m = methods[i];
                var btn = MakeButton(methodRoot, m, () => SelectMethod(m), new Vector2(i * 95, 0), new Vector2(88, 36));
                methodButtons[m] = btn;
                btn.GetComponentInChildren<Text>().fontSize = 12;
            }
            RefreshMethodColors();
        }

        void BuildPrepIngredients()
        {
            ClearChildren(prepIngredientList);
            if (activeRecipe == null) return;
            string[] ids = IngredientsForCategory(prepCategory);
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                if (GetInv(id) <= 0) continue;
                var item = DemoData.GetItem(id);
                var btn = MakeButton(prepIngredientList, (item != null ? item.name : id) + "(" + GetInv(id) + ")", () => AddPrepIngredient(id), Vector2.zero, new Vector2(95, 30));
                btn.GetComponentInChildren<Text>().fontSize = 11;
            }
        }

        void SelectRecipe(string id)
        {
            activeRecipe = DemoData.GetRecipe(id);
            prepSlots.Clear();
            BuildMethodButtons();
            BuildPrepIngredients();
            UpdatePrepInfo();
            Log(activeRecipe != null ? "线索：" + activeRecipe.clue : "线索不存在。");
        }

        void AddPrepIngredient(string id)
        {
            if (activeRecipe == null) { Log("先选一条线索。"); return; }
            if (prepSlots.Count >= 3) { Log("锅槽最多 3 格，点槽位清空。"); return; }
            if (GetInv(id) <= 0) { Log("这个原料没有了。"); return; }
            prepSlots.Add(id);
            UpdatePrepInfo();
        }

        void ClearPrepSlot(int index)
        {
            if (index < 0 || index >= prepSlots.Count) return;
            prepSlots.RemoveAt(index);
            UpdatePrepInfo();
        }

        void SelectMethod(string method)
        {
            selectedMethod = method;
            RefreshMethodColors();
            UpdatePrepInfo();
        }

        void RefreshMethodColors()
        {
            foreach (var kv in methodButtons)
                kv.Value.GetComponent<Image>().color = kv.Key == selectedMethod
                    ? new Color(.55f, .45f, .15f)
                    : new Color(.18f, .24f, .30f);
        }

        bool Matches(RecipeDef r)
        {
            if (r == null || r.method != selectedMethod) return false;
            int total = 0;
            for (int i = 0; i < r.inputs.Length; i++) total += r.inputs[i].count;
            if (prepSlots.Count != total) return false;
            for (int i = 0; i < r.inputs.Length; i++)
            {
                var ing = r.inputs[i];
                int current = 0;
                for (int j = 0; j < prepSlots.Count; j++) if (prepSlots[j] == ing.itemId) current++;
                if (current != ing.count) return false;
            }
            if (!string.IsNullOrEmpty(r.requiredTool) && GetInv(r.requiredTool) <= 0) return false;
            return true;
        }

        bool CanAttempt()
        {
            return activeRecipe != null && prepSlots.Count > 0 && !string.IsNullOrEmpty(selectedMethod);
        }

        string NextClueHint()
        {
            int bestRevenue = int.MaxValue;
            int bestDay = int.MaxValue;
            for (int i = 0; i < DemoData.Recipes.Length; i++)
            {
                var r = DemoData.Recipes[i];
                if (r.category != prepCategory) continue;
                if (unlocked.Contains(r.id)) continue;
                if (totalRevenue >= r.unlockRevenue && day >= r.unlockDay) return "下一线索已解锁：点左边线索试做。";
                if (r.unlockRevenue < bestRevenue) { bestRevenue = r.unlockRevenue; bestDay = r.unlockDay; }
            }
            if (bestRevenue == int.MaxValue) return "本品类线索已全部解锁。";
            return "下一线索：累计营收 ¥" + bestRevenue + " / 第 " + bestDay + " 天。当前累计 ¥" + totalRevenue.ToString("0") + "。";
        }

        void UpdatePrepSlots()
        {
            for (int i = 0; i < prepSlotTexts.Length; i++)
            {
                if (prepSlotTexts[i] == null) continue;
                if (i < prepSlots.Count)
                {
                    var item = DemoData.GetItem(prepSlots[i]);
                    prepSlotTexts[i].text = item != null ? item.name : prepSlots[i];
                    prepSlotButtons[i].GetComponent<Image>().color = new Color(.25f, .48f, .32f);
                }
                else
                {
                    prepSlotTexts[i].text = "空";
                    prepSlotButtons[i].GetComponent<Image>().color = new Color(.18f, .24f, .30f);
                }
            }
            craftButton.interactable = CanAttempt();
        }

        void UpdatePrepInfo()
        {
            RefreshMethodColors();
            if (!IsCategoryUnlocked(prepCategory))
            {
                prepInfo.text = "备货：还没有设备。先去“升级”买关东煮机。";
                prepHint.text = "第一天不备货；第二天买关东煮机后，备货里才会出现关东煮线索。\\n" +
                    "后续按天数解锁蛋糕机、便当柜、仪式台、蒸笼、鱼摊、肉摊等。";
                craftButton.interactable = false;
                UpdatePrepSlots();
                return;
            }
            if (activeRecipe == null)
            {
                prepInfo.text = "品类：" + prepCategory + " · 先选一条线索。菜谱不会直接显示名字。";
                prepHint.text = NextClueHint() + "\n" +
                    "点左边线索 → 点下面原料放进 3 个锅槽 → 选手法 → 试做。不匹配材料退回，没有 timing，不会失败。";
                craftButton.interactable = false;
                UpdatePrepSlots();
                return;
            }
            string skewerInfo = string.IsNullOrEmpty(activeRecipe.skewer) ? "" : "（" + activeRecipe.skewer + "）";
            prepInfo.text = "品类：" + prepCategory + " · " +
                (unlocked.Contains(activeRecipe.id) ? "已解锁：" + activeRecipe.name + skewerInfo + " · " : "线索：") + activeRecipe.clue;
            string selected = "";
            for (int i = 0; i < prepSlots.Count; i++)
            {
                var item = DemoData.GetItem(prepSlots[i]);
                selected += (i > 0 ? " + " : "") + (item != null ? item.name : prepSlots[i]);
            }
            if (string.IsNullOrEmpty(selected)) selected = "（还没放）";
            string method = string.IsNullOrEmpty(selectedMethod) ? "未选" : selectedMethod;
            prepHint.text = "已选：" + selected + "（" + prepSlots.Count + "/3） · 手法/汤底：" + method + "\n" +
                "点原料放进锅，选手法，点“试做 / 下锅”。不匹配材料退回，没有 timing，不会失败。";
            UpdatePrepSlots();
        }

        void ConsumeInputs(RecipeDef r)
        {
            for (int i = 0; i < r.inputs.Length; i++)
            {
                var id = r.inputs[i].itemId;
                inv[id] = GetInv(id) - r.inputs[i].count;
            }
        }

        void CraftButton()
        {
            if (activeRecipe == null) { Log("先选一条线索。"); return; }
            if (!CanAttempt()) { Log("先放原料、选手法。"); return; }
            if (!Matches(activeRecipe)) { Log("不对/差一点，材料退回。"); return; }
            ConsumeInputs(activeRecipe);
            AddPrep(activeRecipe.outputId, activeRecipe.outputCount);
            unlocked.Add(activeRecipe.id);
            var item = DemoData.GetItem(activeRecipe.outputId);
            Log("试做成功：" + (item != null ? item.name : activeRecipe.outputId) + " x" + activeRecipe.outputCount +
                (string.IsNullOrEmpty(activeRecipe.skewer) ? "" : "（" + activeRecipe.skewer + "）"));
            prepSlots.Clear();
            selectedMethod = "";
            BuildPrepIngredients();
            BuildPrepRecipes();
            UpdatePrepInfo();
            RefreshTop();
        }

        void DonePrep()
        {
            if (prepSlots.Count > 0) { Log("先把锅里的原料做成成品，或点槽位清空。"); return; }
            ShowPanel(nightPanel);
            StartNight();
            Log("备货结束，进入夜班。");
        }

        // ---------------- night ----------------

        void StartNight()
        {
            nightOrders.Clear();
            nightIndex = 0;
            offer.Clear();
            int count = (day % 3 == 0) ? 2 : 1;
            if (day % 3 == 0)
            {
                NightRequestDef key = null;
                int k = (day / 3) % 3;
                if (k == 0) key = DemoData.GetRequest("key_candle");
                else if (k == 1) key = DemoData.GetRequest("key_incense");
                else key = DemoData.GetRequest("key_ricecake");
                if (key != null) nightOrders.Add(key);
            }
            while (nightOrders.Count < count)
            {
                int r = UnityEngine.Random.Range(0, 5);
                nightOrders.Add(DemoData.Requests[r]);
            }
            ShowPanel(nightPanel);
            ShowNightOrder();
        }

        void ShowNightOrder()
        {
            if (nightIndex >= nightOrders.Count)
            {
                nightInfo.text = "今晚的单子处理完了。";
                ClearChildren(offerList);
                dealButton.gameObject.SetActive(false);
                nextNightButton.gameObject.SetActive(false);
                doneNightButton.gameObject.SetActive(true);
                nightHint.text = "点“结束夜晚 → 下一天”。";
                return;
            }
            doneNightButton.gameObject.SetActive(false);
            dealButton.gameObject.SetActive(true);
            nextNightButton.gameObject.SetActive(false);
            var req = nightOrders[nightIndex];
            nightInfo.text = "夜班单 " + (nightIndex + 1) + " / " + nightOrders.Count + "：" + req.label +
                (req.isKey ? "（关键物品）" : " · 需要 " + req.tag + " ≥ " + req.desired + " · 基础阴德 " + req.baseVirtue);
            offer.Clear();
            BuildOfferButtons();
            dealButton.GetComponentInChildren<Text>().text = req.isKey ? "给它" : "成交";
            dealButton.interactable = req.isKey ? GetPrep(req.requiredItem) > 0 : false;
            nightHint.text = req.isKey
                ? "关键物品订单：有对应备货才能接；没有就点“下一位”放弃，故事暂停。"
                : "点左边物品加入出价（1-3 件），成交后按标签/价值算阴德。";
        }

        void BuildOfferButtons()
        {
            ClearChildren(offerList);
            offerImages.Clear();
            var req = nightOrders[nightIndex];
            if (req.isKey)
            {
                var item = DemoData.GetItem(req.requiredItem);
                string name = item != null ? item.name : req.requiredItem;
                var btn = MakeButton(offerList, name + "（有 " + GetPrep(req.requiredItem) + "）", null, Vector2.zero, new Vector2(200, 28));
                btn.GetComponentInChildren<Text>().fontSize = 11;
                btn.interactable = false;
                return;
            }
            var seen = new List<string>();
            foreach (var kv in prepared) if (kv.Value > 0 && DemoData.GetItem(kv.Key) != null && DemoData.GetItem(kv.Key).nightValue > 0) seen.Add(kv.Key);
            foreach (var kv in inv) if (kv.Value > 0 && DemoData.GetItem(kv.Key) != null && DemoData.GetItem(kv.Key).nightValue > 0 && !seen.Contains(kv.Key)) seen.Add(kv.Key);
            for (int i = 0; i < seen.Count; i++)
            {
                string id = seen[i];
                var item = DemoData.GetItem(id);
                int count = GetPrep(id) + GetInv(id);
                var btn = MakeButton(offerList, item.name + "  价值" + item.nightValue + "  有" + count, () => ToggleOffer(id), Vector2.zero, new Vector2(200, 28));
                btn.GetComponentInChildren<Text>().fontSize = 11;
                offerImages[id] = btn.GetComponent<Image>();
            }
            RefreshOfferColors();
        }

        void ToggleOffer(string id)
        {
            if (offer.Contains(id)) offer.Remove(id);
            else if (offer.Count < 3) offer.Add(id);
            RefreshOfferColors();
            dealButton.interactable = offer.Count > 0;
        }

        void RefreshOfferColors()
        {
            foreach (var kv in offerImages)
                kv.Value.color = offer.Contains(kv.Key) ? new Color(.55f, .45f, .15f) : new Color(.18f, .24f, .30f);
        }

        int OfferValue(string id)
        {
            var item = DemoData.GetItem(id);
            return item != null ? item.nightValue : 0;
        }

        void Deal()
        {
            if (nightIndex >= nightOrders.Count) return;
            var req = nightOrders[nightIndex];
            if (req.isKey)
            {
                if (GetPrep(req.requiredItem) <= 0) { Log("没有 " + req.requiredItem + "。"); return; }
                prepared[req.requiredItem] -= 1;
                virtue += req.baseVirtue;
                Log("关键订单完成：" + req.label + "，+" + req.baseVirtue + " 阴德。");
                NextNight();
                return;
            }
            if (offer.Count == 0) { Log("先选至少一件出价。"); return; }
            int total = 0;
            bool tagMatch = false;
            for (int i = 0; i < offer.Count; i++)
            {
                var item = DemoData.GetItem(offer[i]);
                if (item == null) continue;
                total += item.nightValue;
                for (int t = 0; t < item.tags.Length; t++) if (item.tags[t] == req.tag) tagMatch = true;
            }
            bool success = req.tag == "group"
                ? (offer.Count >= req.desired && total >= req.desired)
                : (tagMatch && total >= req.desired);
            if (!success)
            {
                Log("没成交：标签/价值不够。物品不消耗，客人走了。");
                NextNight();
                return;
            }
            int reward = req.baseVirtue + Mathf.Min(3, total - req.desired);
            virtue += reward;
            for (int i = 0; i < offer.Count; i++)
            {
                string id = offer[i];
                if (GetPrep(id) > 0) prepared[id] -= 1;
                else if (GetInv(id) > 0) inv[id] -= 1;
            }
            Log("成交：" + req.label + "，+" + reward + " 阴德。");
            NextNight();
        }

        void NextNight()
        {
            nightIndex++;
            offer.Clear();
            ShowNightOrder();
            RefreshTop();
        }

        void FinishNight()
        {
            if (weekPending) return;
            weekRevenue += dayRevenue;
            totalRevenue += dayRevenue;
            if (day >= 60) { ShowWeekSettlement(true); return; }
            int end = DemoData.GetWeekEnd(DemoData.GetWeekIndex(day));
            if (day >= end) { ShowWeekSettlement(false); return; }
            day++;
            StartDay();
            Log("新的一天：" + day + " 号。");
        }

        void ShowWeekSettlement(bool finalWeek)
        {
            weekPending = true;
            int week = DemoData.GetWeekIndex(day);
            int start = DemoData.GetWeekStart(week);
            int end = DemoData.GetWeekEnd(week);
            int target = DemoData.GetWeeklyTarget(day);
            float ratio = target > 0 ? weekRevenue / target : 0f;

            float nextMult;
            string result;
            string landlord;
            if (ratio >= 1.1f)
            {
                nextMult = .85f; result = "超额完成（≥110%）"; landlord = "哼，这周像样。下周房租给你减 15%。";
                badWeekStreak = 0;
            }
            else if (ratio >= 1.0f)
            {
                nextMult = 1f; result = "达标（100-109%）"; landlord = "勉强过关。下周照旧。";
                badWeekStreak = 0;
            }
            else if (ratio >= .9f)
            {
                nextMult = 1.10f; result = "差一点（90-99%）"; landlord = "差一点。下周房租加 10%，别再让我提醒。";
                badWeekStreak = 0;
            }
            else if (ratio >= .75f)
            {
                nextMult = 1.25f; result = "不达标（75-89%）"; landlord = "你当我开善堂？下周房租加 25%。";
                badWeekStreak++;
            }
            else
            {
                nextMult = 1.50f; result = "严重不达标（<75%）"; landlord = "最后通牒。下周房租加 50%；再这样，店就别开了。";
                badWeekStreak++;
            }

            if (!finalWeek && badWeekStreak >= 2)
            {
                nextMult = Mathf.Max(nextMult, 1.25f);
                landlord += "\n连续两周不达标：房东把最低涨幅锁在 +25%。";
            }

            rentMultiplier = nextMult;
            string extra = finalWeek ? "这是最后一周。" : "下周房租 x" + nextMult.ToString("0.00") + "。";
            weekText.text =
                "第 " + (week + 1) + " 周结算（第 " + start + "-" + end + " 天）\n\n" +
                "本周营收：¥" + weekRevenue.ToString("0") + "\n" +
                "周目标：¥" + target + "\n" +
                "结果：" + result + "（" + (ratio * 100f).ToString("0") + "%）\n\n" +
                "房东：" + landlord + "\n\n" +
                extra;
            continueWeekButton.GetComponentInChildren<Text>().text = finalWeek ? "查看结局" : "继续 → 第 " + (day + 1) + " 天";
            weekPanel.SetActive(true);
            RefreshTop();
            Log("W" + (week + 1) + " 结算：" + weekRevenue.ToString("0") + " / " + target + "（" + (ratio * 100f).ToString("0") + "%）。");
        }

        void ContinueWeek()
        {
            if (endingShown) { weekPanel.SetActive(false); return; }
            if (day >= 60) { ShowEnding(); return; }
            day++;
            weekRevenue = 0f;
            weekPending = false;
            weekPanel.SetActive(false);
            StartDay();
            Log("新的一周开始：第 " + day + " 天。");
        }

        void ShowEnding()
        {
            endingShown = true;
            weekPanel.SetActive(true);
            string ending;
            if (cash >= 20000f) ending = "买下店铺：你把卷帘门换成自己的名字，房东第一次没话说。";
            else if (virtue >= 120) ending = "阴德圆满：账本合上，店里的灯留给了那些还会回来的人。";
            else ending = "普通打烊：店转手了，但你活下来了。下一段日子再说。";
            weekText.text =
                "六十天，打烊。\n\n" +
                "结局：" + ending + "\n\n" +
                "现金：¥" + cash.ToString("0") + "    阴德：" + virtue + "\n" +
                "总营收：¥" + totalRevenue.ToString("0") + "    解锁菜谱：" + unlocked.Count + "    备货：" + PrepSummary() + "\n\n" +
                "这间店替人记了六十天的事。";
            continueWeekButton.GetComponentInChildren<Text>().text = "结束";
            weekPending = false;
            Log("60 天结束：" + ending);
        }

        // ---------------- debug / tabs ----------------

        void DebugSkipPhase()
        {
            if (day <= 10) day = 11;
            else if (day <= 20) day = 21;
            else if (day <= 30) day = 31;
            else if (day <= 40) day = 41;
            else if (day <= 50) day = 51;
            else day = 60;
            weekRevenue = 0f;
            weekPending = false;
            endingShown = false;
            badWeekStreak = 0;
            rentMultiplier = 1f;
            if (weekPanel != null) weekPanel.SetActive(false);
            StartDay();
            Log("跳到第 " + day + " 天（" + phase.name + "）。");
        }

        void ShowPanel(GameObject p)
        {
            if (dayPanel != null) dayPanel.SetActive(p == dayPanel);
            if (truckPanel != null) truckPanel.SetActive(p == truckPanel);
            if (prepPanel != null) prepPanel.SetActive(p == prepPanel);
            if (nightPanel != null) nightPanel.SetActive(p == nightPanel);
            if (shopPanel != null) shopPanel.SetActive(p == shopPanel);
            if (p == prepPanel) BuildPrepRecipes();
            if (p == shopPanel) BuildShopRows();
            RefreshTop();
        }

        void RefreshTop()
        {
            if (topText == null) return;
            topText.text = "第 " + day + " 天 · " + (phase != null ? phase.name : "") +
                " · 现金 ¥" + cash.ToString("0") + " · 阴德 " + virtue +
                " · 客人 " + customersLeft + " · 备货 " + PrepSummary() +
                " · W" + (weekIndex + 1) + " " + weekRevenue.ToString("0") + "/" + weekTarget;
            if (topWeekText != null)
            {
                topWeekText.text = "W" + (weekIndex + 1) + " ¥" + weekRevenue.ToString("0") + " / " + weekTarget;
            }
        }

        string PrepSummary()
        {
            string s = "";
            foreach (var kv in prepared)
            {
                if (kv.Value <= 0) continue;
                var item = DemoData.GetItem(kv.Key);
                if (item == null) continue;
                if (s.Length > 0) s += " ";
                s += item.name + "x" + kv.Value;
            }
            return string.IsNullOrEmpty(s) ? "-" : s;
        }

        void Log(string msg)
        {
            logs.Add(msg);
            while (logs.Count > 4) logs.RemoveAt(0);
            if (logText != null) logText.text = string.Join("\n", logs.ToArray());
            Debug.Log("[GamePlayDemo] " + msg);
        }
    }
}
