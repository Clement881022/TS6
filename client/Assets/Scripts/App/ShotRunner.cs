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
            Instance.StartCoroutine(review >= 0 ? Instance.ReviewModels(dir, review+1 < args.Length ? args[review+1] : "guanyu") : Instance.Run(dir));
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
            if (!(PageHost.Current.ActivePage is MapPage) || GameSession.SelectedLevel != 1
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
                else if (active is ShopPage shop) { shop.DebugSetTab(1); yield return Wait(0.5f); Shot(dir, "shop-souls"); }
                yield return Wait(0.2f);
            }

            GameSession.SelectedLevel = Mathf.Max(2, GameSession.SelectedLevel);
            GameSession.FormationStageId = GameSession.StageIdOf(DemoMeta.FirstOpenFormationLevel); // 編隊頁只用於教學關之後的關卡
            yield return Go(Page.Formation);
            Shot(dir, "formation");
            yield return Wait(0.4f);

            // 實際開一場戰鬥：經過後端開始關卡 → 戰鬥場景 → 預覽 / 出牌 / 打完結算。
            var task = GameSession.BeginStage(GameSession.StageIdOf(GameSession.SelectedLevel));
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
            GameSession.SelectedLevel = 3;
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
            Application.Quit();
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
