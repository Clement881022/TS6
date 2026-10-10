#nullable enable
using System;
using System.Collections;
using System.IO;
using System.Linq;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    public sealed class GrowthUiCapture : MonoBehaviour
    {
        public static void Begin(string directory)
        {
            var go = new GameObject("Growth UI review");
            DontDestroyOnLoad(go);
            go.AddComponent<GrowthUiCapture>().StartCoroutine(Run(directory));
            Application.logMessageReceived += (message, trace, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception) Application.Quit(1);
            };
        }

        private static IEnumerator Run(string directory)
        {
            Directory.CreateDirectory(directory);
            yield return new WaitForSecondsRealtime(2);
            if (!(GameSession.Backend is LocalBackend)) throw new InvalidOperationException("Disposable profile required");
            Nav.Go(Page.HeroGrowth);
            yield return new WaitForSecondsRealtime(1);
            var profile = GameSession.View;
            profile.Level = int.TryParse(GameSession.CommandLineValue("-sanguoReviewProfileLevel"), out int level) ? level : 2;
            profile.Raw.Level = profile.Level;
            foreach (var def in GameSession.Roster)
            {
                var state = new HeroState { HeroId = def.Id };
                profile.Heroes[def.Id] = state;
                profile.Raw.Heroes[def.Id] = state;
            }
            var root = PageHost.Current!.GetComponent<UIDocument>().rootVisualElement;
            var page = (HeroesPage)PageHost.Current.ActivePage!;
            page.DebugSelectHero("xiahoudun");
            yield return new WaitForSecondsRealtime(.5f);
            if (root.Q(className: "growth-attributes") == null || root.Q(className: "strategy-hero-art") != null)
                throw new InvalidOperationException("Default attribute view");
            yield return CommercialUiCapture.Capture(directory, "01-attributes-level");
            Validate(root);
            if (profile.Level > 1 && root.Query<Label>(className: "stat-line-val").ToList().Count != 13)
                throw new InvalidOperationException("Four upgrade preview values required");
            page.DebugSetTab(1);
            yield return new WaitForSecondsRealtime(.5f);
            Validate(root);
            yield return CommercialUiCapture.Capture(directory, "02-breakthrough");
            profile.Heroes["xiahoudun"].Stars = 3;
            page.Rebuild();
            yield return new WaitForSecondsRealtime(.5f);
            if (root.Q(className: "growth-break-stars").Query<Label>().ToList().Count(s => s.text == "★") != 3)
                throw new InvalidOperationException("Breakthrough progress");
            yield return CommercialUiCapture.Capture(directory, "03-three-stars");
            page.DebugSetAttributes(false);
            yield return new WaitForSecondsRealtime(.5f);
            if (root.Q(className: "strategy-hero-art") == null || root.Q(className: "growth-attributes") != null)
                throw new InvalidOperationException("Character tab route");
            Validate(root);
            yield return CommercialUiCapture.Capture(directory, "06-character");
            page.DebugToggleModel();
            RenderModels();
            yield return new WaitForSecondsRealtime(1);
            RenderModels();
            if (root.Q(className: "hero-model") == null) throw new InvalidOperationException("Model route");
            Validate(root);
            var model = root.Q(className: "hero-model").worldBound;
            var center = root.Q(className: "hero-center").worldBound;
            if (Mathf.Abs(model.center.x - center.center.x) > 1)
                throw new InvalidOperationException("Model frame must be centered");
            var texture = root.Q(className: "hero-model").resolvedStyle.backgroundImage.renderTexture;
            if (texture == null) throw new InvalidOperationException("Missing model render texture");
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var rendered = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            rendered.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            rendered.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(directory, "model-render.png"), rendered.EncodeToPNG());
            int painted = rendered.GetPixels32().Count(p => p.a > 32);
            Destroy(rendered);
            if (painted < 100) throw new InvalidOperationException("Model render is empty");
            yield return CommercialUiCapture.Capture(directory, "07-model");
            page.DebugSetAttributes(true);
            page.DebugSetTab(0);
            yield return new WaitForSecondsRealtime(.5f);
            Validate(root);
            yield return CommercialUiCapture.Capture(directory, "08-restored-attributes");
            page.DebugScrollRosterEnd();
            yield return new WaitForSecondsRealtime(.5f);
            yield return CommercialUiCapture.Capture(directory, "09-roster-end");
            File.WriteAllText(Path.Combine(directory, "verification.txt"),
                "Default attributes, character/model routes and centered model frame, roster name fit, panel containment, vertically stacked framed costs and compact actions passed. Equipment moved to independent page. Disposable profile only. Visual quality reviewed separately.");
            Application.Quit();
        }

        private static void Validate(VisualElement root)
        {
            foreach (string cls in new[] { "hero-left", "hero-center", "hero-right", "hero-model", "growth-attributes", "growth-action", "growth-equipment-card" })
                foreach (var element in root.Query(className: cls).ToList())
                {
                    var b = element.worldBound;
                    var r = root.worldBound;
                    if (b.width <= 0 || b.height <= 0 || b.xMin < r.xMin - 1 || b.xMax > r.xMax + 1 || b.yMin < r.yMin - 1 || b.yMax > r.yMax + 1)
                        throw new InvalidOperationException("UI outside viewport: " + cls);
                }
            foreach (var label in root.Query<Label>(className: "strategy-roster-name").ToList())
            {
                var measured = label.MeasureTextSize(label.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
                if (measured.x > label.contentRect.width + 1) throw new InvalidOperationException($"Roster name clipped: {label.text}, needs {measured.x}, available {label.contentRect.width}");
            }
            var costs = root.Query(className: "cost-chip").ToList();
            if (costs.Count == 2 && (Mathf.Abs(costs[0].worldBound.xMin - costs[1].worldBound.xMin) > 1 || costs[0].worldBound.yMax > costs[1].worldBound.yMin))
                throw new InvalidOperationException("Costs must be vertically stacked");
            if (root.Query<Label>().ToList().Any(l => l.text.Contains("重複武將") || l.text.Contains("庫存沒有") || l.text.Contains("只能配戴")))
                throw new InvalidOperationException("Redundant growth text");
            foreach (var art in root.Query(className: "growth-equipment-art").ToList())
                if (art.resolvedStyle.backgroundImage.texture == null) throw new InvalidOperationException("Missing equipment image");
        }

        private static void RenderModels()
        {
            foreach (var stage in FindObjectsByType<ModelStage>(FindObjectsSortMode.None))
            {
                var camera = stage.GetComponentInChildren<Camera>();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                    new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = stage.Texture });
            }
        }
    }
}
