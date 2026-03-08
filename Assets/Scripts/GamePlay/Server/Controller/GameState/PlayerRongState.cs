using System.Collections.Generic;
using System.Linq;
using GamePlay.Client.Controller;
using GamePlay.Server.Model;
using GamePlay.Server.Model.Events;
using Mahjong.Model;
using Mirror;
using UnityEngine;

namespace GamePlay.Server.Controller.GameState
{
    public class PlayerRongState : ServerState
    {
        public int CurrentPlayerIndex;
        public int[] RongPlayerIndices;
        public Tile WinningTile;
        public MahjongSet MahjongSet;
        public PointInfo[] RongPointInfos;
        private IList<PointTransfer> transfers;
        private bool[] responds;
        private float serverTimeOut;
        private float firstTime;
        private bool next;
        private const float ServerMaxTimeOut = 10;

        public override void OnServerStateEnter()
        {
            ServerBehaviour.Instance.OnClientReadyReceived += HandleClientReady;
            responds = new bool[players.Count];
            var playerNames = RongPlayerIndices.Select(
                playerIndex => CurrentRoundStatus.GetPlayerName(playerIndex)
            ).ToArray();
            var handData = RongPlayerIndices.Select(
                playerIndex => CurrentRoundStatus.HandData(playerIndex)
            ).ToArray();
            var richiStatus = RongPlayerIndices.Select(
                playerIndex => CurrentRoundStatus.RichiStatus(playerIndex)
            ).ToArray();
            var multipliers = RongPlayerIndices.Select(
                playerIndex => gameSettings.GetMultiplier(CurrentRoundStatus.IsDealer(playerIndex), players.Count)
            ).ToArray();
            var totalPoints = RongPointInfos.Select((info, i) => info.BasePoint * multipliers[i]).ToArray();
            var netInfos = RongPointInfos.Select(info => new NetworkPointInfo
            {
                Fu = info.Fu,
                YakuValues = info.YakuList.ToArray(),
                Dora = info.Dora,
                UraDora = info.UraDora,
                RedDora = info.RedDora,
                BeiDora = info.BeiDora,
                IsQTJ = info.IsQTJ
            }).ToArray();
            Debug.Log($"The following players are claiming rong: {string.Join(",", RongPlayerIndices)}, "
                + $"PlayerNames: {string.Join(",", playerNames)}");
            var rongInfo = new EventMessages.RongInfo
            {
                RongPlayerIndices = RongPlayerIndices,
                RongPlayerNames = playerNames,
                HandData = handData,
                WinningTile = WinningTile,
                DoraIndicators = MahjongSet.DoraIndicators,
                UraDoraIndicators = MahjongSet.UraDoraIndicators,
                RongPlayerRichiStatus = richiStatus,
                RongPointInfos = netInfos,
                TotalPoints = totalPoints
            };
            // send rpc calls
            ClientBehaviour.Instance.RpcRong(rongInfo);
            // get point transfers
            transfers = new List<PointTransfer>();
            for (int i = 0; i < RongPlayerIndices.Length; i++)
            {
                var rongPlayerIndex = RongPlayerIndices[i];
                var point = RongPointInfos[i];
                var multiplier = multipliers[i];
                int pointValue = point.BasePoint * multiplier;
                int extraPoints = i == 0 ? CurrentRoundStatus.ExtraPoints * (players.Count - 1) : 0;
                transfers.Add(new PointTransfer
                {
                    From = CurrentPlayerIndex,
                    To = rongPlayerIndex,
                    Amount = pointValue + extraPoints
                });
            }
            // richi-sticks-points
            transfers.Add(new PointTransfer
            {
                From = -1,
                To = RongPlayerIndices[0],
                Amount = CurrentRoundStatus.RichiSticksPoints
            });
            next = !RongPlayerIndices.Contains(CurrentRoundStatus.OyaPlayerIndex);
            // determine server time out
            serverTimeOut = ServerMaxTimeOut * RongPointInfos.Length + ServerConstants.ServerTimeBuffer;
            firstTime = Time.time;
        }

        public override void OnServerStateExit()
        {
            ServerBehaviour.Instance.OnClientReadyReceived -= HandleClientReady;
        }

        public override void OnStateUpdate()
        {
            if (Time.time - firstTime > serverTimeOut || responds.All(r => r))
            {
                PointTransfer();
                return;
            }
        }

        private void PointTransfer()
        {
            ServerBehaviour.Instance.PointTransfer(transfers, next, !next, false);
        }

        private void HandleClientReady(int index)
        {
            Debug.Log($"{GetType().Name} receives ClientReadyEvent with content {index}");
            responds[index] = true;
        }
    }
}
