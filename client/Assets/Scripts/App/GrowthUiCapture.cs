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
            page.DebugSetTab(2);
            yield return new WaitForSecondsRealtime(.5f);
            Validate(root);
            if (root.Query(className: "growth-equipment-empty").ToList().Count != 3)
                throw new InvalidOperationException("Three empty equipment slots");
            yield return CommercialUiCapture.Capture(directory, "04-empty-equipment");
            for (int tier = 1; tier <= Equipment.MaxTier; tier++)
            {
                foreach (var slot in Equipment.Slots) profile.Heroes["xiahoudun"].Equipment[slot.ToString()] = tier;
                page.Rebuild();
                yield return new WaitForSecondsRealtime(.4f);
                Validate(root);
                var effects = root.Query<Label>(className: "growth-equipment-effect").ToList();
                if (!effects.Any(e => e.text == $"生命 +{Equipment.PercentOf(tier)}%")
                    || !effects.Any(e => e.text == $"防禦 +{Equipment.PercentOf(tier)}%"))
                    throw new InvalidOperationException("Current equipment bonuses");
                yield return CommercialUiCapture.Capture(directory, "05-equipment-tier-" + tier);
            }
            page.DebugSetAttributes(false);
            yield return new WaitForSecondsRealtime(.5f);
            if (root.Q(className: "strategy-hero-art") == null || root.Q(className: "growth-attributes") != null)
                throw new InvalidOperationException("Character tab route");
            Validate(root);
            yield return CommercialUiCapture.Capture(directory, "06-character");
            page.DebugToggleModel();
            yield return new WaitForSecondsRealtime(1);
            if (root.Q(className: "hero-model") == null) throw new InvalidOperationException("Model route");
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
                "Default attributes, character/model routes, roster name fit, panel containment, horizontal framed costs, compact actions, empty equipment, current armor bonuses and five rarity tiers passed. Disposable profile only. Visual quality reviewed separately.");
            Application.Quit();
        }

        private static void Validate(VisualElement root)
        {
            foreach (string cls in new[] { "hero-left", "hero-center", "hero-right", "growth-attributes", "growth-action", "growth-equipment-card" })
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
            if (costs.Count == 2 && (Mathf.Abs(costs[0].worldBound.yMin - costs[1].worldBound.yMin) > 1 || costs[0].worldBound.xMax > costs[1].worldBound.xMin))
                throw new InvalidOperationException("Costs must be side by side");
            if (root.Query<Label>().ToList().Any(l => l.text.Contains("重複武將") || l.text.Contains("庫存沒有") || l.text.Contains("只能配戴")))
                throw new InvalidOperationException("Redundant growth text");
            foreach (var art in root.Query(className: "growth-equipment-art").ToList())
                if (art.resolvedStyle.backgroundImage.texture == null) throw new InvalidOperationException("Missing equipment image");
        }
    }
}
