#nullable enable
using System.Collections;
using System.IO;
using UnityEngine;

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
            Instance.StartCoroutine(Instance.Run(dir));
        }

        private int _n = 1;

        private IEnumerator Run(string dir)
        {
            Directory.CreateDirectory(dir);
            yield return Wait(1.0f);
            Shot(dir, "home");

            yield return Go(Page.Gacha);
            if (PageHost.Current?.ActivePage is GachaPage gacha) _ = gacha.DebugTenPull();
            yield return Wait(0.8f);
            Shot(dir, "gacha");

            foreach (var page in new[] { Page.Map, Page.Heroes, Page.Dungeons, Page.Quests, Page.Shop })
            {
                yield return Go(page);
                Shot(dir, page.ToString().ToLowerInvariant());
            }

            GameSession.SelectedLevel = Mathf.Max(2, GameSession.SelectedLevel);
            yield return Go(Page.Formation);
            Shot(dir, "formation");

            // 實際開一場戰鬥：經過後端開始關卡 → 戰鬥場景 → 預覽 / 出牌 / 打完結算。
            var task = GameSession.BeginStage(GameSession.StageIdOf(GameSession.SelectedLevel));
            while (!task.IsCompleted) yield return null;
            yield return Go(Page.Battle);
            var battle = (PageHost.Current?.ActivePage as BattlePage)?.Screen;
            if (battle == null) { Application.Quit(); yield break; }
            Shot(dir, "battle-start");
            yield return Wait(0.5f);

            battle.DebugPreviewFirstCard();
            yield return Wait(0.3f);
            Shot(dir, "battle-preview");
            yield return Wait(0.5f);

            battle.DebugPlayFirstPlayable();
            battle.DebugPlayFirstPlayable();
            yield return Wait(0.3f);
            Shot(dir, "battle-played");
            yield return Wait(0.5f);

            battle.DebugAutoFinish();
            yield return Wait(1.2f);
            Shot(dir, "battle-result");
            yield return Wait(0.5f);
            Application.Quit();
        }

        private IEnumerator Go(Page page)
        {
            Nav.Go(page);
            yield return Wait(1.0f);
        }

        private static WaitForSecondsRealtime Wait(float seconds) => new WaitForSecondsRealtime(seconds);

        private void Shot(string dir, string name)
        {
            string path = Path.Combine(dir, $"{_n++:00}-{name}.png");
            Debug.Log("[shot] capture " + path);
            ScreenCapture.CaptureScreenshot(path);
        }
    }
}
