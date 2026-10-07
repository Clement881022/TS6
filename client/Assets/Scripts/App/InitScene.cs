#nullable enable
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SanGuo.Client
{
    /// <summary>
    /// 啟動場景（Init）：做一次性的初始化（螢幕、幀率、後端、預載玩家資料），完成後載入 Main 場景。
    /// Main 是常駐的 UI 外殼，之後所有頁面都由 PageHost 以 prefab 載入。
    /// </summary>
    public sealed class InitScene : MonoBehaviour
    {
        public const string MainSceneName = "Main";

        private async void Awake()
        {
            Application.runInBackground = true;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Application.targetFrameRate = 60;

            GameSession.EnsureInit();
            // 預載玩家資料；失敗不擋啟動，進主畫面後各頁會重試並提示。
            await GameSession.Refresh();
            SceneManager.LoadScene(MainSceneName);
        }
    }
}
