#nullable enable
using System.Collections;
using System.IO;
using SanGuo.Core.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>
    /// 自動截圖（-sanguoShot）：跨場景存活，依序造訪每個頁面並截圖，打完一場戰鬥後離開。
    /// 供自動驗證畫面用，不影響正常遊戲。
    /// </summary>
    public sealed class ShotRunner : MonoBehaviour
    {
        public static ShotRunner? Instance { get; private set; }

        public static void Begin(string dir)
        {
            var go = new GameObject("ShotRunner");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<ShotRunner>();
            var args = System.Environment.GetCommandLineArgs();
            int review = System.Array.IndexOf(args, "-sanguoModelReview");
            bool campaign = System.Array.IndexOf(args, "-sanguoCampaignShot") >= 0;
            bool account = System.Array.IndexOf(args, "-sanguoAccountShot") >= 0;
            Instance.StartCoroutine(review >= 0 ? Instance.ReviewModels(dir, review+1 < args.Length ? args[review+1] : "guanyu")
                : account ? Instance.RunAccount(dir) : campaign ? Instance.RunCampaign(dir) : Instance.Run(dir));
        }

        private int _n = 1;

        private IEnumerator ReviewModels(string dir, string names)
        {
            Directory.CreateDirectory(dir);
            yield return Wait(.5f);
            foreach (string id in names.Split(','))
            {
                var prefab = Resources.Load<GameObject>("ProductionCharacters/"+id);
                if(prefab == null || prefab.GetComponent<CharacterClipSet>() == null)
                    throw new System.InvalidOperationException("Refined skeletal review model is missing: "+id);
                var stage = ModelStage.Create(id,1536,1536);
                if(stage == null) throw new System.InvalidOperationException("Could not render model: "+id);
                foreach(var pose in new[]{("front",0f),("three-quarter",-28f),("back",180f)})
                {
                    stage.ReviewAngle(pose.Item2);yield return Wait(.35f);yield return new WaitForEndOfFrame();
                    SaveTexture(stage.Texture,Path.Combine(dir,id+"-"+pose.Item1+".png"));
                }
                stage.ReviewAngle(-20);stage.ReviewAttack();yield return Wait(.32f);yield return new WaitForEndOfFrame();
                SaveTexture(stage.Texture,Path.Combine(dir,id+"-attack.png"));
                stage.Dispose();yield return Wait(.2f);
            }
            Application.Quit();
        }

        private static void SaveTexture(RenderTexture target,string path)
        {
            var previous=RenderTexture.active;RenderTexture.active=target;
            var pixels=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();
            File.WriteAllBytes(path,pixels.EncodeToPNG());Destroy(pixels);RenderTexture.active=previous;
            Debug.Log("[model-review] "+path);
        }

        /// <summary>
        /// 主線章節驗證（-sanguoCampaignShot，搭配 -sanguoClearTo 與 -sanguoLevel 指定章-關）：
        /// 首頁 → 地圖（該章）→ 關卡面板 → 戰前劇情 → 開戰 → 出牌 → 自動打完 → 首通劇情。
        /// </summary>
        private IEnumerator RunCampaign(string dir)
        {
            Directory.CreateDirectory(dir);
            int chapter = GameSession.SelectedChapter, level = GameSession.SelectedLevel;
            yield return Wait(2f);
            Shot(dir, "home");
            yield return Wait(0.4f);
            GameSession.Select(chapter, level);
            GameSession.OpenSelectedStageOnMap = true;
            yield return Go(Page.Map);
            yield return Wait(0.6f);
            Shot(dir, "map-stage");
            yield return Wait(0.4f);
            var root = PageHost.Current!.GetComponent<UIDocument>().rootVisualElement;
            StoryPlayer.Show(root, StoryPlayer.StageTitle(chapter, level), SanGuo.Core.Data.CampaignStory.Before(chapter, level));
            yield return Wait(2.5f);
            Shot(dir, "story-before");
            yield return Wait(0.4f);
            var task = GameSession.BeginStage(GameSession.StageIdOf(chapter, level));
            while (!task.IsCompleted) yield return null;
            if (task.Result != null) { Debug.LogError("[shot] BeginStage failed: " + task.Result); Application.Quit(); yield break; }
            yield return Go(Page.Battle);
            var battle = (PageHost.Current?.ActivePage as BattlePage)?.Screen;
            if (battle == null) { Application.Quit(); yield break; }
            yield return Wait(1.2f);
            Shot(dir, "battle-start");
            yield return Wait(0.4f);
            battle.DebugPlayFirstPlayable();
            yield return Wait(1.0f);
            Shot(dir, "battle-played");
            yield return Wait(0.4f);
            battle.DebugAutoFinish();
            yield return Wait(4f);
            Shot(dir, "battle-result");
            yield return Wait(0.6f);

            // 世界 Boss：面板 → 開戰 → 自動打完 → 結算（-sanguoClearTo 需在第 2 章之後才會開放）。
            yield return Go(Page.WorldBoss);
            yield return Wait(1.5f);
            Shot(dir, "worldboss");
            yield return Wait(0.4f);
            var wb = GameSession.BeginStage(SanGuo.Core.Meta.WorldBoss.StageId);
            while (!wb.IsCompleted) yield return null;
            if (wb.Result != null) { Debug.LogError("[shot] WorldBoss BeginStage failed: " + wb.Result); Application.Quit(); yield break; }
            yield return Go(Page.Battle);
            battle = (PageHost.Current?.ActivePage as BattlePage)?.Screen;
            if (battle == null) { Application.Quit(); yield break; }
            yield return Wait(1.2f);
            Shot(dir, "worldboss-battle");
            yield return Wait(0.4f);
            battle.DebugAutoFinish();
            yield return Wait(4f);
            Shot(dir, "worldboss-result");
            yield return Wait(0.4f);
            yield return Go(Page.WorldBoss);
            yield return Wait(1.5f);
            Shot(dir, "worldboss-after");
            yield return Wait(0.4f);

            // 商店：首儲禮包與月卡、測試付款買豪華通行證後的通行證分頁。
            yield return Go(Page.Shop);
            yield return Wait(0.8f);
            Shot(dir, "shop-pay");
            yield return Wait(0.3f);
            var buy = GameSession.Backend.BuyWithTestPayment(SanGuo.Core.Meta.Shop.PassLuxury);
            while (!buy.IsCompleted) yield return null;
            var refresh = GameSession.Refresh();
            while (!refresh.IsCompleted) yield return null;
            if (PageHost.Current?.ActivePage is ShopPage shopPage) shopPage.DebugShowPass();
            yield return Wait(0.8f);
            Shot(dir, "shop-pass-luxury");
            yield return Wait(0.4f);
            Application.Quit();
        }

        private IEnumerator Run(string dir)
        {
            Directory.CreateDirectory(dir);
            foreach (var hero in GameSession.Roster)
                if (HeroArt.Face(hero.Id) == null || HeroArt.Full(hero.Id) == null || HeroArt.Bust(hero.Id) == null || HeroArt.Model(hero.Id) == null)
                    throw new System.InvalidOperationException("Q-style hero art is incomplete: " + hero.Id);
            Debug.Log($"[shot] Q-style roster art verified: {GameSession.Roster.Count} faces and full illustrations.");
            yield return Wait(1.0f);
            Shot(dir, "home");
            yield return Wait(0.4f);

            // Send actual UI navigation-submit events to verify the new home controls.
            var homeRoot = PageHost.Current!.GetComponent<UIDocument>().rootVisualElement;
            Submit(homeRoot.Q<Button>(className: "home-primary"));
            yield return Wait(1.0f);
            if (!(PageHost.Current.ActivePage is MapPage) || GameSession.SelectedChapter != 0 || GameSession.SelectedLevel != 1
                || homeRoot.Q(className: "stage-panel") == null)
                throw new System.InvalidOperationException("Home expedition did not open the next stage detail.");
            yield return Go(Page.Home);
            var homeNav = homeRoot.Query<Button>(className: "home-fn").ToList();
            Submit(homeNav[1]);
            yield return Wait(1.0f);
            if (!(PageHost.Current.ActivePage is GachaPage))
                throw new System.InvalidOperationException("Home recruitment navigation failed.");
            Debug.Log("[shot] Home UI submit events verified: expedition detail and recruitment navigation.");
            Shot(dir, "gacha-main");
            yield return Wait(0.4f);
            if (PageHost.Current?.ActivePage is GachaPage gacha) _ = gacha.DebugTenPull();
            yield return Wait(2.2f);
            Shot(dir, "gacha");
            yield return Wait(0.4f);

            foreach (var page in new[] { Page.Map, Page.Heroes, Page.Dungeons, Page.Quests, Page.Shop })
            {
                yield return Go(page);
                Shot(dir, page.ToString().ToLowerInvariant());
                yield return Wait(0.4f);

                // 各頁的次要畫面：關卡面板、武將的突破 / 裝備分頁、七日目標。
                var active = PageHost.Current?.ActivePage;
                if (active is MapPage map) { map.DebugOpenStage(1); yield return Wait(0.4f); Shot(dir, "map-stage"); }
                else if (active is HeroesPage heroes)
                {
                    heroes.DebugSetTab(1); yield return Wait(0.5f); Shot(dir, "heroes-break"); yield return Wait(0.3f); // 截圖在幀尾才擷取，下一步要等一下
                    heroes.DebugSetTab(2); yield return Wait(0.5f); Shot(dir, "heroes-equip");
                    yield return Wait(0.3f);
                    heroes.DebugScrollRosterEnd(); yield return Wait(0.3f); Shot(dir, "heroes-roster-end");
                    yield return Wait(0.3f);
                    heroes.DebugSelectLastOwned(); yield return Wait(0.5f); Shot(dir, "heroes-featured");
                    yield return Wait(.3f); heroes.DebugToggleModel();
                    yield return Wait(.5f); Shot(dir, "heroes-model"); yield return Wait(.3f);
                    heroes.DebugToggleModel();
                }
                else if (active is DungeonsPage dungeons) { dungeons.DebugScrollEnd(); yield return Wait(0.4f); Shot(dir, "dungeons-end"); }
                else if (active is QuestsPage quests) { quests.DebugShowSevenDay(); yield return Wait(0.5f); Shot(dir, "quests-seven"); }
                else if (active is ShopPage shop)
                {
                    shop.DebugSetTab(1); yield return Wait(0.5f); Shot(dir, "shop-souls"); yield return Wait(0.3f);
                    shop.DebugShowPass(); yield return Wait(0.5f); Shot(dir, "shop-pass");
                }
                yield return Wait(0.2f);
            }

            GameSession.Select(0, Mathf.Max(2, GameSession.SelectedLevel));
            GameSession.FormationStageId = GameSession.StageIdOf(0, DemoMeta.FirstOpenFormationLevel); // 編隊頁只用於教學關之後的關卡
            yield return Go(Page.Formation);
            Shot(dir, "formation");
            yield return Wait(0.4f);

            // 實際開一場戰鬥：經過後端開始關卡 → 戰鬥場景 → 預覽 / 出牌 / 打完結算。
            var task = GameSession.BeginStage(GameSession.StageIdOf(GameSession.SelectedChapter, GameSession.SelectedLevel));
            while (!task.IsCompleted) yield return null;
            yield return Go(Page.Battle);
            var battle = (PageHost.Current?.ActivePage as BattlePage)?.Screen;
            if (battle == null) { Application.Quit(); yield break; }
            Shot(dir, "battle-start");
            yield return Wait(0.4f);
            yield return Wait(0.5f);

            battle.DebugPreviewFirstCard();
            yield return Wait(0.3f);
            Shot(dir, "battle-preview");
            yield return Wait(0.4f);
            yield return Wait(0.5f);

            battle.DebugSelectFirstPlayable();
            yield return Wait(0.3f);
            Shot(dir, "battle-selected");
            yield return Wait(0.5f);
            battle.DebugPlayFirstPlayable();
            yield return Wait(0.3f);
            Shot(dir, "battle-played");
            yield return Wait(0.4f);
            yield return Wait(0.5f);

            battle.DebugAutoFinish();
            yield return Wait(1.2f);
            Shot(dir, "battle-result");
            yield return Wait(0.4f);
            yield return Wait(0.5f);
            yield return Go(Page.Home);
            Shot(dir, "home-progress");
            yield return Wait(0.5f);
            GameSession.Select(0, 3);
            yield return Go(Page.Battle);
            battle = (PageHost.Current?.ActivePage as BattlePage)?.Screen;
            battle!.DebugReviewScenario(3);
            yield return Wait(.8f);
            Shot(dir, "battle-status-card"); yield return Wait(.4f);
            battle.DebugReviewActions();
            yield return Wait(.22f); Shot(dir, "battle-distinct-actions"); yield return Wait(.7f);
            battle.DebugPreviewFirstCard();
            yield return Wait(.4f); Shot(dir, "battle-status-detail"); yield return Wait(.4f);
            battle.DebugReviewZoom(true);
            yield return Wait(.5f); Shot(dir, "battle-max-zoom"); yield return Wait(.4f);
            battle.DebugReviewZoom(false); battle.DebugReviewLongHand();
            yield return Wait(.5f); Shot(dir, "battle-ten-cards"); yield return Wait(.4f);
            battle.DebugReviewScenario(8);
            yield return Wait(.5f); Shot(dir, "battle-crowded"); yield return Wait(.4f);
            battle.DebugReviewZoom(true);
            yield return Wait(.5f); Shot(dir, "battle-crowded-zoom"); yield return Wait(.4f);
            battle.DebugReviewZoom(false);yield return Wait(.5f);
            battle.DebugReviewUnitDetails(true);yield return Wait(.3f);
            Shot(dir,"battle-enemy-details");yield return Wait(.4f);
            battle.DebugReviewUnitDetails(false);yield return Wait(.3f);
            Shot(dir,"battle-hero-details");yield return Wait(.4f);
            Application.Quit();
        }

        /// <summary>
        /// 帳號流程驗證（-sanguoAccountShot，需搭配 -sanguoServer 且裝置上沒有登入 token）：
        /// 登入頁 → 錯誤密碼 → 遊客進入 → 帳號頁 → 綁定帳號密碼 → 登出 → 以帳號密碼重新登入。每一步都檢查結果，不符就拋例外。
        /// </summary>
        private IEnumerator RunAccount(string dir)
        {
            Directory.CreateDirectory(dir);
            string username = "shot_" + System.DateTime.UtcNow.ToString("MMddHHmmss");
            const string password = "shotpass123";
            // 每次都從全新的遊客開始：清掉這台機器上次留下的遊客金鑰與 token。
            PlayerPrefs.DeleteKey("sanguo.guestKey");
            yield return Wait(1.5f);
            if (!(PageHost.Current?.ActivePage is LoginPage))
            {
                var logout = GameSession.Accounts!.Logout();
                while (!logout.IsCompleted) yield return null;
                Nav.Go(Page.Login);
                yield return Wait(1f);
            }
            Expect<LoginPage>();
            Shot(dir, "login");
            yield return Wait(0.4f);

            Fill(0, username); Fill(1, "wrongpass99");
            Submit(ButtonText("登入"));
            yield return Wait(1.5f);
            Expect<LoginPage>();
            Shot(dir, "login-wrong-password");
            yield return Wait(0.4f);

            Submit(ButtonText("遊客進入"));
            yield return Wait(2.5f);
            Expect<HomePage>();
            if (GameSession.Account == null || GameSession.Account.Bound) throw new System.InvalidOperationException("遊客登入後沒有取得遊客帳號");
            Shot(dir, "home-guest");
            yield return Wait(0.4f);

            var playerCard = PageHost.Current!.GetComponent<UIDocument>().rootVisualElement.Q(className: "home-player");
            using (var click = ClickEvent.GetPooled())
            {
                click.target = playerCard;
                playerCard.SendEvent(click);
            }
            yield return Wait(2f);
            Expect<AccountPage>();
            Shot(dir, "account-guest");
            yield return Wait(0.4f);

            Fill(0, "測試主公");
            Submit(ButtonText("修改暱稱"));
            yield return Wait(1.5f);
            if (GameSession.Account?.Nickname != "測試主公") throw new System.InvalidOperationException("暱稱沒有更新");
            Fill(1, username); Fill(2, password); Fill(3, password);
            Submit(ButtonText("綁定帳號"));
            yield return Wait(2f);
            if (GameSession.Account?.Username != username) throw new System.InvalidOperationException("綁定後帳號不符");
            Shot(dir, "account-bound");
            yield return Wait(0.4f);

            Submit(ButtonText("登出"));
            yield return Wait(1.5f);
            Expect<LoginPage>();
            Fill(0, username); Fill(1, password);
            Submit(ButtonText("登入"));
            yield return Wait(2.5f);
            Expect<HomePage>();
            if (GameSession.Account?.Nickname != "測試主公") throw new System.InvalidOperationException("重新登入後不是同一個帳號");
            Shot(dir, "home-relogin");
            yield return Wait(0.6f);
            Debug.Log("[shot] account flow verified");
            Application.Quit();
        }

        private static void Expect<T>() where T : PageBase
        {
            if (!(PageHost.Current?.ActivePage is T))
                throw new System.InvalidOperationException($"預期在 {typeof(T).Name}，實際是 {PageHost.Current?.ActivePage?.GetType().Name}");
        }

        private static Button ButtonText(string text) =>
            PageHost.Current!.GetComponent<UIDocument>().rootVisualElement.Query<Button>().Where(b => b.text == text).First()
            ?? throw new System.InvalidOperationException("找不到按鈕：" + text);

        private static void Fill(int index, string value)
        {
            var fields = PageHost.Current!.GetComponent<UIDocument>().rootVisualElement.Query<TextField>().ToList();
            if (index >= fields.Count) throw new System.InvalidOperationException($"找不到第 {index + 1} 個輸入框");
            fields[index].value = value;
        }

        private IEnumerator Go(Page page)
        {
            Nav.Go(page);
            yield return Wait(1.0f);
        }

        private static WaitForSecondsRealtime Wait(float seconds) => new WaitForSecondsRealtime(seconds);

        private static void Submit(Button button)
        {
            if (button == null || !button.enabledInHierarchy)
                throw new System.InvalidOperationException("Home control is missing or unavailable.");
            using (var evt = NavigationSubmitEvent.GetPooled())
            {
                evt.target = button;
                button.SendEvent(evt);
            }
        }

        private static void CheckHomeLayout()
        {
            var root = PageHost.Current!.GetComponent<UIDocument>().rootVisualElement;
            var bounds = root.worldBound;
            var navigation = root.Q(className: "home-fn-bar");
            var campaign = root.Q(className: "home-campaign");
            foreach (var cls in new[] { "home-fn-bar", "home-campaign", "home-res", "strategy-brand", "home-primary" })
            {
                var element = root.Q(className: cls);
                if (element == null || element.worldBound.xMin < bounds.xMin - 1 || element.worldBound.xMax > bounds.xMax + 1
                    || element.worldBound.yMin < bounds.yMin - 1 || element.worldBound.yMax > bounds.yMax + 1)
                    throw new System.InvalidOperationException("Home UI is clipped: " + cls);
            }
            if (navigation.worldBound.Overlaps(campaign.worldBound)
                || root.Q(className: "home-res").worldBound.Overlaps(root.Q(className: "strategy-brand").worldBound))
                throw new System.InvalidOperationException("Home navigation or header overlaps.");
            Debug.Log("[shot] Home layout bounds and group separation verified.");
        }

        private void Shot(string dir, string name)
        {
            if (PageHost.Current?.ActivePage is HomePage) CheckHomeLayout();
            if (PageHost.Current?.ActivePage is BattlePage battlePage) battlePage.Screen?.DebugCheckLayout();
            string path = Path.Combine(dir, $"{_n++:00}-{name}.png");
            Debug.Log("[shot] capture " + path);
            ScreenCapture.CaptureScreenshot(path);
        }
    }
}
