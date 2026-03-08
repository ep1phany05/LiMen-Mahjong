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
    public class PlayerTsumoState : ServerState
    {
        public int TsumoPlayerIndex;
        public Tile WinningTile;
        public MahjongSet MahjongSet;
        public PointInfo TsumoPointInfo;
        private IList<PointTransfer> transfers;
        private bool[] responds;
        private float serverTimeOut;
        private float firstTime;
        private const float ServerMaxTimeOut = 10;

        public override void OnServerStateEnter()
        {
            ServerBehaviour.Instance.OnClientReadyReceived += HandleClientReady;
            int multiplier = gameSettings.GetMultiplier(CurrentRoundStatus.IsDealer(TsumoPlayerIndex), players.Count);
            var netInfo = new NetworkPointInfo
            {
                Fu = TsumoPointInfo.Fu,
                YakuValues = TsumoPointInfo.YakuList.ToArray(),
                Dora = TsumoPointInfo.Dora,
                UraDora = TsumoPointInfo.UraDora,
                RedDora = TsumoPointInfo.RedDora,
                IsQTJ = TsumoPointInfo.IsQTJ
            };
            var info = new EventMessages.TsumoInfo
            {
                TsumoPlayerIndex = TsumoPlayerIndex,
                TsumoPlayerName = CurrentRoundStatus.GetPlayerName(TsumoPlayerIndex),
                TsumoHandData = CurrentRoundStatus.HandData(TsumoPlayerIndex),
                WinningTile = WinningTile,
                DoraIndicators = MahjongSet.DoraIndicators,
                UraDoraIndicators = MahjongSet.UraDoraIndicators,
                IsRichi = CurrentRoundStatus.RichiStatus(TsumoPlayerIndex),
                TsumoPointInfo = netInfo,
                TotalPoints = TsumoPointInfo.BasePoint * multiplier
            };
            // send rpc calls
            ClientBehaviour.Instance.RpcTsumo(info);
            // get point transfers
            // todo -- tsumo loss related, now there is tsumo loss by default
            transfers = new List<PointTransfer>();
            for (int playerIndex = 0; playerIndex < players.Count; playerIndex++)
            {
                if (playerIndex == TsumoPlayerIndex) continue;
                int amount = TsumoPointInfo.BasePoint;
                if (CurrentRoundStatus.IsDealer(playerIndex)) amount *= 2;
                int extraPoints = CurrentRoundStatus.ExtraPoints;
                transfers.Add(new PointTransfer
                {
                    From = playerIndex,
                    To = TsumoPlayerIndex,
                    Amount = amount + extraPoints
                });
            }
            // richi-sticks-points
            transfers.Add(new PointTransfer
            {
                From = -1,
                To = TsumoPlayerIndex,
                Amount = CurrentRoundStatus.RichiSticksPoints
            });
            responds = new bool[players.Count];
            // determine server time out
            serverTimeOut = ServerMaxTimeOut + ServerConstants.ServerTimeBuffer;
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
            var next = CurrentRoundStatus.OyaPlayerIndex != TsumoPlayerIndex;
            ServerBehaviour.Instance.PointTransfer(transfers, next, !next, false);
        }

        private void HandleClientReady(int index)
        {
            Debug.Log($"{GetType().Name} receives ClientReadyEvent with content {index}");
            responds[index] = true;
        }
    }
}
