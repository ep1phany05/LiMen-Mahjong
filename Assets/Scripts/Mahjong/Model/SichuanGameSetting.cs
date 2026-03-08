using UnityEngine;

namespace Mahjong.Model
{
    [System.Serializable]
    public class SichuanGameSetting
    {
        [Tooltip("是否启用血战到底")]
        public bool xuezhanMode = true;

        [Tooltip("自摸是否翻倍")]
        public bool zimoDouble = true;

        [Tooltip("杠上花是否翻倍")]
        public bool gangShangHua = true;

        [Tooltip("是否启用缺一门")]
        public bool queYiMen = true;

        public static int TileCount => 108;
    }
}
