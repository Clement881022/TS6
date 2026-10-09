using System;
using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core
{
    public sealed partial class Battle
    {
        private void RemoveHeroCards(Unit unit)
        {
            DrawPile.RemoveAll(c => c.Owner == unit);
            Hand.RemoveAll(c => c.Owner == unit);
            DiscardPile.RemoveAll(c => c.Owner == unit);
            foreach (var pile in new[] { DiscardPile, DrawPile, Hand })
            {
                int i = pile.FindIndex(c => c.Owner == null);
                if (i < 0) continue;
                pile.RemoveAt(i);
                break;
            }
        }

        private bool DrawOne()
        {
            if (DrawPile.Count == 0)
            {
                if (DiscardPile.Count == 0) return false;
                DrawPile.AddRange(DiscardPile);
                DiscardPile.Clear();
                Shuffle(DrawPile);
            }
            var card = DrawPile[0];
            DrawPile.RemoveAt(0);
            if (Hand.Count >= MaxHandSize) DiscardPile.Add(card);
            else Hand.Add(card);
            return true;
        }

        private void OrderByScript(List<CardInstance> list)
        {
            var rest = new List<CardInstance>(list);
            var ordered = new List<CardInstance>();
            foreach (var id in Setup.ScriptedDraw)
            {
                var card = rest.FirstOrDefault(c => c.Def.Id == id);
                if (card == null) continue;
                ordered.Add(card);
                rest.Remove(card);
            }
            ordered.AddRange(rest);
            list.Clear();
            list.AddRange(ordered);
        }

        private void Shuffle(List<CardInstance> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Rng.Next(i + 1);
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
