using System.Collections.Generic;
using Mahjong.Logic;
using Mahjong.Model;
using NUnit.Framework;

public class SichuanLogicTest
{
    [Test]
    public void CreateSichuanTileSet_Returns108Tiles()
    {
        var tiles = SichuanLogic.CreateSichuanTileSet();
        Assert.AreEqual(108, tiles.Count);
    }

    [Test]
    public void CheckQueYiMen_MissingOneSuit_ReturnsTrue()
    {
        var hand = new List<Tile>
        {
            new Tile(Suit.M, 1), new Tile(Suit.M, 2), new Tile(Suit.M, 3),
            new Tile(Suit.P, 1), new Tile(Suit.P, 2), new Tile(Suit.P, 3),
        };

        Assert.IsTrue(SichuanLogic.CheckQueYiMen(hand, new List<Tile>()));
    }

    [Test]
    public void CheckQueYiMen_HasAllSuits_ReturnsFalse()
    {
        var hand = new List<Tile>
        {
            new Tile(Suit.M, 1), new Tile(Suit.P, 1), new Tile(Suit.S, 1),
        };

        Assert.IsFalse(SichuanLogic.CheckQueYiMen(hand, new List<Tile>()));
    }

    [Test]
    public void CanWin_ValidHand_ReturnsTrue()
    {
        var hand = new List<Tile>
        {
            new Tile(Suit.M, 1), new Tile(Suit.M, 2), new Tile(Suit.M, 3),
            new Tile(Suit.M, 4), new Tile(Suit.M, 5), new Tile(Suit.M, 6),
            new Tile(Suit.M, 7), new Tile(Suit.M, 8), new Tile(Suit.M, 9),
            new Tile(Suit.P, 1), new Tile(Suit.P, 2), new Tile(Suit.P, 3),
            new Tile(Suit.P, 5), new Tile(Suit.P, 5),
        };

        Assert.IsTrue(SichuanLogic.CanWin(hand));
    }
}
