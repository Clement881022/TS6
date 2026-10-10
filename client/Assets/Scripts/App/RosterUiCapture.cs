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
    public sealed class RosterUiCapture : MonoBehaviour
    {
        public static void Begin(string directory)
        {
            var go = new GameObject("Roster UI review");
            DontDestroyOnLoad(go);
            go.AddComponent<RosterUiCapture>().StartCoroutine(Run(directory));
            Application.logMessageReceived += (message, trace, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception) Application.Quit(1);
            };
        }

        private static IEnumerator Run(string directory)
        {
            Directory.CreateDirectory(directory);
            yield return new WaitForSecondsRealtime(2);
            if (!(GameSession.Backend is LocalBackend)) throw new InvalidOperationException("Review requires disposable local profile");
            var first = GameSession.Roster.OrderByDescending(d => d.Rarity).ThenBy(d => d.Id, StringComparer.Ordinal).First();
            var root = PageHost.Current!.GetComponent<UIDocument>().rootVisualElement;
            foreach (var page in new[] { Page.Heroes, Page.HeroGrowth })
            {
                Nav.Go(page);
                yield return new WaitForSecondsRealtime(1);
                var profile = GameSession.View;
                foreach (var def in GameSession.Roster)
                {
                    var state = new HeroState { HeroId = def.Id };
                    profile.Heroes[def.Id] = state;
                    profile.Raw.Heroes[def.Id] = state;
                }
                var heroes = (HeroesPage)PageHost.Current.ActivePage!;
                heroes.Rebuild();
                yield return new WaitForSecondsRealtime(.5f);
                yield return CommercialUiCapture.Capture(directory, page + "-zero");
                Validate(root, first.Id, 0);
                foreach (int stars in new[] { 3, HeroGrowth.MaxStars })
                {
                    profile.Heroes[first.Id].Stars = stars;
                    heroes.Rebuild();
                    yield return new WaitForSecondsRealtime(.5f);
                    Validate(root, first.Id, stars);
                    yield return CommercialUiCapture.Capture(directory, page + "-stars-" + stars);
                }
                var scroll = root.Q<ScrollView>(className: "roster-scroll");
                var thumb = scroll.verticalScroller.Q(className: "unity-base-slider__dragger");
                float top = thumb.worldBound.yMin;
                scroll.verticalScroller.value = scroll.verticalScroller.highValue / 2;
                yield return new WaitForSecondsRealtime(.5f);
                yield return CommercialUiCapture.Capture(directory, page + "-scroll-middle");
                heroes.DebugScrollRosterEnd();
                yield return new WaitForSecondsRealtime(.5f);
                var last = scroll.contentContainer.Children().Last();
                var viewport = scroll.Q(className: "unity-scroll-view__content-viewport");
                if (thumb.worldBound.yMin <= top + 10 || last.worldBound.yMax > viewport.worldBound.yMax + 1
                    || last.worldBound.yMin < viewport.worldBound.yMin)
                    throw new InvalidOperationException("Roster scroll cannot reach last hero");
                yield return CommercialUiCapture.Capture(directory, page + "-scroll-end");
                Submit(root.Q<Button>(className: "strategy-hero-view"));
                yield return new WaitForSecondsRealtime(.6f);
                if (root.Q(className: "hero-model") == null) throw new InvalidOperationException("Model icon route failed");
                yield return CommercialUiCapture.Capture(directory, page + "-model");
                Submit(root.Q<Button>(className: "strategy-hero-view"));
                yield return new WaitForSecondsRealtime(.5f);
                if (root.Q(className: "strategy-hero-art") == null || root.Q(className: "hero-model") != null)
                    throw new InvalidOperationException("Illustration icon route failed");
                Validate(root, first.Id, HeroGrowth.MaxStars);
                yield return CommercialUiCapture.Capture(directory, page + "-restored");
            }
            File.WriteAllText(Path.Combine(directory, "verification.txt"),
                "Roster/growth: removed headings, rarity order/frames, level-only labels, zero/three/five breakthrough stars, scroll track/thumb/end containment and icon submit routes passed. Uses disposable local profile. Model rendering quality is not asserted.");
            Application.Quit();
        }

        private static void Validate(VisualElement root, string selected, int stars)
        {
            if (root.Q(className: "strategy-roster-title") != null || root.Q(className: "strategy-roster-count") != null)
                throw new InvalidOperationException("Redundant roster heading");
            var rows = root.Query<Button>(className: "strategy-roster-entry").ToList();
            var expected = GameSession.Roster.OrderByDescending(d => d.Rarity).ThenBy(d => d.Id, StringComparer.Ordinal).ToList();
            if (!rows.Select(r => r.name).SequenceEqual(expected.Select(d => "roster-" + d.Id)))
                throw new InvalidOperationException("Roster rarity order");
            for (int i = 0; i < rows.Count; i++)
            {
                var face = rows[i].Q(className: "strategy-roster-face");
                var rarity = expected[i].Rarity;
                float width = face.resolvedStyle.borderTopWidth;
                if (rarity == Rarity.R ? width > .1f : width <= 0)
                    throw new InvalidOperationException($"Rarity border: {expected[i].Id} {rarity}, width {width}");
                var color = face.resolvedStyle.borderTopColor;
                var target = rarity == Rarity.UR ? new Color(214/255f,58/255f,48/255f) : new Color(234/255f,187/255f,72/255f);
                if (rarity != Rarity.R && (Mathf.Abs(color.r-target.r) > .01f || Mathf.Abs(color.g-target.g) > .01f || Mathf.Abs(color.b-target.b) > .01f))
                    throw new InvalidOperationException("Rarity border color");
                if (rows[i].Q<Label>(className: "strategy-roster-level").text != "Lv.1")
                    throw new InvalidOperationException("Redundant rarity text");
                CheckStars(rows[i], expected[i].Id == selected ? stars : 0);
                if (rows[i].Q(className: "strategy-roster-stars").worldBound.yMax > rows[i].Q(className: "strategy-roster-name").worldBound.yMin + 1)
                    throw new InvalidOperationException("Breakthrough stars must be above hero name");
            }
            CheckStars(root.Q(className: "hero-grade"), stars);
            if (!root.Q<Button>("roster-" + selected).ClassListContains("strategy-roster-selected"))
                throw new InvalidOperationException("Default hero selection");
            var toggle = root.Q<Button>(className: "strategy-hero-view");
            if (toggle.text.Length != 0 || toggle.Q(className: "hero-view-icon") == null || toggle.tooltip.Length == 0)
                throw new InvalidOperationException("Model switch icon");
            var scroll = root.Q<ScrollView>(className: "roster-scroll");
            var track = scroll.verticalScroller.Q(className: "unity-base-slider__drag-container");
            var thumb = scroll.verticalScroller.Q(className: "unity-base-slider__dragger");
            if (track.worldBound.height < scroll.worldBound.height * .8f || thumb.worldBound.height >= track.worldBound.height
                || thumb.worldBound.yMin < track.worldBound.yMin - 1 || thumb.worldBound.yMax > track.worldBound.yMax + 1)
                throw new InvalidOperationException($"Broken roster scroll: viewport {scroll.worldBound}, track {track.worldBound}, thumb {thumb.worldBound}");
        }

        private static void CheckStars(VisualElement element, int count)
        {
            var stars = element.Query<Label>(className: "hero-breakthrough-star").ToList();
            if (stars.Count != HeroGrowth.MaxStars || stars.Count(s => s.text == "★") != count
                || stars.Count(s => s.text == "☆") != HeroGrowth.MaxStars-count)
                throw new InvalidOperationException("Breakthrough star state");
            foreach (var star in stars)
            {
                var color = star.resolvedStyle.color;
                float target = star.text == "★" ? 241 / 255f : 8 / 255f;
                if (Mathf.Abs(color.r - target) > .01f) throw new InvalidOperationException("Breakthrough star color");
            }
        }

        private static void Submit(Button button)
        {
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = button; button.SendEvent(e); }
        }
    }
}
