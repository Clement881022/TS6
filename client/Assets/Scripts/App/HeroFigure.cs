#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    public sealed class HeroFigure : VisualElement
    {
        [Serializable]
        private sealed class Frame
        {
            public string id = "";
            public float center;
            public float top;
            public float feet;
        }

        [Serializable]
        private sealed class Frames
        {
            public Frame[] frames = Array.Empty<Frame>();
        }

        private static readonly Dictionary<string, Frame> Framing = LoadFrames();
        private readonly VisualElement _image;
        private Texture2D? _texture;
        private Frame? _frame;
        public string HeroId { get; private set; } = "";
        public float BodyHeight => resolvedStyle.height * .6f;
        public float FootLine => worldBound.yMin + resolvedStyle.height * .9f;

        public HeroFigure(string heroId, string className)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList(className);
            AddToClassList("hero-figure");
            _image = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("hero-figure-image");
            Add(_image);
            RegisterCallback<GeometryChangedEvent>(_ => Fit());
            SetHero(heroId);
        }

        public void SetHero(string heroId)
        {
            HeroId = heroId;
            _texture = HeroArt.Full(heroId);
            Framing.TryGetValue(heroId, out _frame);
            _image.style.backgroundImage = _texture != null ? new StyleBackground(_texture) : new StyleBackground();
            Fit();
        }

        private void Fit()
        {
            if (_texture == null || _frame == null) return;
            float height = resolvedStyle.height, width = resolvedStyle.width;
            if (float.IsNaN(height) || float.IsNaN(width) || height <= 0 || width <= 0) return;
            float scale = height * .6f / ((_frame.feet - _frame.top) * _texture.height);
            _image.style.width = _texture.width * scale;
            _image.style.height = _texture.height * scale;
            _image.style.left = width * .5f - _frame.center * _texture.width * scale;
            _image.style.top = height * .9f - _frame.feet * _texture.height * scale;
        }

        private static Dictionary<string, Frame> LoadFrames()
        {
            var result = new Dictionary<string, Frame>();
            var source = Resources.Load<TextAsset>("ChibiSkin/body-framing");
            if (source != null)
                foreach (var frame in JsonUtility.FromJson<Frames>(source.text).frames) result[frame.id] = frame;
            return result;
        }
    }
}
