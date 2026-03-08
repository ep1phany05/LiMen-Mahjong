using System.Collections.Generic;
using System.Linq;
using Mahjong.Model;

namespace Mahjong.Logic
{
    public static class SichuanLogic
    {
        public static List<Tile> CreateSichuanTileSet()
        {
            var tiles = new List<Tile>(SichuanGameSetting.TileCount);
            foreach (var suit in new[] { Suit.M, Suit.P, Suit.S })
            {
                for (int rank = 1; rank <= 9; rank++)
                {
                    for (int copy = 0; copy < 4; copy++)
                    {
                        tiles.Add(new Tile(suit, rank));
                    }
                }
            }

            return tiles;
        }

        public static bool CheckQueYiMen(IEnumerable<Tile> hand, IEnumerable<Tile> melds)
        {
            var allTiles = hand.Concat(melds);
            bool hasM = allTiles.Any(t => t.Suit == Suit.M);
            bool hasP = allTiles.Any(t => t.Suit == Suit.P);
            bool hasS = allTiles.Any(t => t.Suit == Suit.S);

            int suitCount = (hasM ? 1 : 0) + (hasP ? 1 : 0) + (hasS ? 1 : 0);
            return suitCount == 2;
        }

        public static bool CanWin(List<Tile> handTiles)
        {
            if (handTiles == null || handTiles.Count != 14) return false;

            var sorted = handTiles.OrderBy(t => t.Suit).ThenBy(t => t.Rank).ToList();
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                if (!sorted[i].EqualsIgnoreColor(sorted[i + 1])) continue;

                var remaining = new List<Tile>(sorted);
                remaining.RemoveAt(i + 1);
                remaining.RemoveAt(i);
                if (CanFormMelds(remaining)) return true;
            }

            return false;
        }

        public static int CalculateFan(List<Tile> hand, bool isTsumo, bool isGangShangHua)
        {
            int fan = 1;
            if (isTsumo) fan *= 2;
            if (isGangShangHua) fan *= 2;

            if (hand != null && hand.Count > 0)
            {
                var suitedTiles = hand.Where(t => t.Suit != Suit.Z).ToList();
                if (suitedTiles.Count == hand.Count && suitedTiles.All(t => t.Suit == suitedTiles[0].Suit))
                {
                    fan += 3;
                }
            }

            return fan;
        }

        private static bool CanFormMelds(List<Tile> tiles)
        {
            if (tiles.Count == 0) return true;

            var sorted = tiles.OrderBy(t => t.Suit).ThenBy(t => t.Rank).ToList();
            var first = sorted[0];

            var triplet = sorted.Where(t => t.EqualsIgnoreColor(first)).Take(3).ToList();
            if (triplet.Count == 3)
            {
                var next = new List<Tile>(sorted);
                RemoveOne(next, triplet[0]);
                RemoveOne(next, triplet[1]);
                RemoveOne(next, triplet[2]);
                if (CanFormMelds(next)) return true;
            }

            if (first.Suit != Suit.Z && first.Rank <= 7)
            {
                if (TryFindAndRemoveSequence(sorted, first, out var remain))
                {
                    if (CanFormMelds(remain)) return true;
                }
            }

            return false;
        }

        private static bool TryFindAndRemoveSequence(List<Tile> tiles, Tile first, out List<Tile> remain)
        {
            remain = null;
            int idx2 = tiles.FindIndex(t => t.Suit == first.Suit && t.Rank == first.Rank + 1);
            if (idx2 < 0) return false;
            int idx3 = tiles.FindIndex(t => t.Suit == first.Suit && t.Rank == first.Rank + 2);
            if (idx3 < 0) return false;

            remain = new List<Tile>(tiles);
            RemoveOne(remain, first);
            RemoveOne(remain, new Tile(first.Suit, first.Rank + 1));
            RemoveOne(remain, new Tile(first.Suit, first.Rank + 2));
            return true;
        }

        private static void RemoveOne(List<Tile> tiles, Tile target)
        {
            int index = tiles.FindIndex(t => t.EqualsIgnoreColor(target));
            if (index >= 0) tiles.RemoveAt(index);
        }
    }
}
