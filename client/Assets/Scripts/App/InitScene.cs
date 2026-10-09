#nullable enable
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SanGuo.Client
{
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
            await GameSession.Refresh();
            SceneManager.LoadScene(MainSceneName);
        }
    }
}
