#nullable enable
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
namespace SanGuo.Client
{
    public sealed class CommercialUiCapture : MonoBehaviour
    {
        public static void Begin(string directory)
        {
            var go = new GameObject("Commercial UI capture");
            DontDestroyOnLoad(go);
            go.AddComponent<CommercialUiCapture>().StartCoroutine(Run(directory));
            Application.logMessageReceived += (m,t,k) => { if (k == LogType.Error || k == LogType.Exception) Application.Quit(1); };
        }
        private static IEnumerator Run(string directory)
        {
            Directory.CreateDirectory(directory);
            yield return new WaitForSecondsRealtime(2);
            Nav.Go(Page.Home);
            yield return new WaitForSecondsRealtime(1);
            var root = PageHost.Current!.GetComponent<UIDocument>().rootVisualElement;
            if (root.Query<Button>(className:"home-fn").ToList().Count != 6 || root.Q(className:"ui-help-button") != null) throw new InvalidOperationException("Home navigation or help");
            yield return Capture(directory,"01-home");
            foreach (var item in new[] { ("home-heroes",Page.Heroes),("home-herogrowth",Page.HeroGrowth),("home-gacha",Page.Gacha),("home-dungeons",Page.Dungeons),("home-quests",Page.Quests),("home-shop",Page.Shop) })
            {
                Submit(root.Q<Button>(item.Item1));
                yield return new WaitForSecondsRealtime(.8f);
                if (PageHost.Current.ActivePage!.GetType().Name != item.Item2+"Page") throw new InvalidOperationException("Home route: "+item.Item1);
                Nav.Go(Page.Home);yield return new WaitForSecondsRealtime(.6f);
            }
            Submit(root.Q<Button>("home-dungeons"));yield return new WaitForSecondsRealtime(.7f);
            yield return Capture(directory,"02-challenge");
            Submit(root.Q<Button>("challenge-WorldBoss"));yield return new WaitForSecondsRealtime(1);
            if (!(PageHost.Current.ActivePage is WorldBossPage)) throw new InvalidOperationException("Challenge boss route");
            Nav.Go(Page.Home);yield return new WaitForSecondsRealtime(.5f);
            Submit(root.Q<Button>(className:"home-primary"));yield return new WaitForSecondsRealtime(.7f);
            if (!(PageHost.Current.ActivePage is MapPage)) throw new InvalidOperationException("Expedition route");
            GameSession.Select(0,6);GameSession.Ticket = null;Nav.Go(Page.Battle);
            yield return new WaitForSecondsRealtime(1.5f);
            var battle = ((BattlePage)PageHost.Current.ActivePage!).Screen!;
            battle.DebugReviewScenario(6);yield return new WaitForSecondsRealtime(1);
            battle.DebugValidateCommercialArt();
            yield return Capture(directory,"03-battle");
            battle.DebugReviewLongHand();yield return new WaitForSecondsRealtime(.5f);
            battle.DebugValidateCommercialArt();
            yield return Capture(directory,"04-ten-cards");
            battle.DebugArtSelectCard(0);yield return new WaitForSecondsRealtime(.5f);
            yield return Capture(directory,"05-card-detail");
            battle.DebugArtSelectCard(0);yield return new WaitForSecondsRealtime(.3f);
            battle.DebugArtSelectCard(5);yield return new WaitForSecondsRealtime(.5f);
            yield return Capture(directory,"07-move-hint");
            battle.DebugArtSelectCard(5);yield return new WaitForSecondsRealtime(.3f);
            battle.DebugReviewUnitDetails(true);yield return new WaitForSecondsRealtime(.5f);
            yield return Capture(directory,"08-unit-window");
            battle.DebugShowObjective();yield return new WaitForSecondsRealtime(.5f);
            yield return Capture(directory,"09-objective");
            battle.DebugReviewScenario(8);yield return new WaitForSecondsRealtime(.8f);
            battle.DebugValidateCommercialArt();
            yield return Capture(directory,"06-crowded");
            File.WriteAllText(Path.Combine(directory,"verification.txt"),"Home six routes, challenge tabs, expedition route, grid picking, head HUD, intent order and compact card fields passed. No commercial art quality claim.");
            Application.Quit();
        }
        internal static IEnumerator Capture(string directory,string name)
        {
            int width = Screen.width, height = Screen.height;
            var settings = PageHost.Current!.GetComponent<UIDocument>().panelSettings;
            var scene = new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
            var ui = new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
            scene.Create();ui.Create();
            var camera = Camera.main;
            if (camera != null) camera.targetTexture = scene;
            settings.targetTexture = ui;
            settings.clearColor = true;
            settings.colorClearValue = Color.clear;
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();
            if (camera != null) UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = scene });
            var pixels = Read(scene);
            var overlay = Read(ui);
            var a = pixels.GetPixels32();var b = overlay.GetPixels32();
            for (int i=0;i<a.Length;i++)
            {
                float t = 1-b[i].a/255f;
                a[i] = new Color32((byte)Mathf.Min(255,b[i].r+a[i].r*t),(byte)Mathf.Min(255,b[i].g+a[i].g*t),(byte)Mathf.Min(255,b[i].b+a[i].b*t),255);
            }
            pixels.SetPixels32(a);pixels.Apply();
            File.WriteAllBytes(Path.Combine(directory,name+".png"),pixels.EncodeToPNG());
            settings.targetTexture = null;
            if (camera != null) camera.targetTexture = null;
            scene.Release();ui.Release();
            Destroy(scene);Destroy(ui);Destroy(pixels);Destroy(overlay);
            yield return new WaitForSecondsRealtime(.4f);
        }
        private static Texture2D Read(RenderTexture target)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var pixels = new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();
            RenderTexture.active = previous;
            return pixels;
        }
        private static void Submit(Button button)
        {
            if (button == null) throw new InvalidOperationException("Missing route button");
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = button;button.SendEvent(e); }
        }
    }
}
