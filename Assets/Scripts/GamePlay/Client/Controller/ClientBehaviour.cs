using System.Linq;
using Common.StateMachine;
using Common.StateMachine.Interfaces;
using GamePlay.Client.Controller.GameState;
using GamePlay.Client.Model;
using GamePlay.Server.Controller;
using GamePlay.Server.Model;
using GamePlay.Server.Model.Events;
using Mahjong.Model;
using Mirror;
using UnityEngine;

namespace GamePlay.Client.Controller
{
    /// <summary>
    /// Mirror 版本的客户端行为。
    /// [TargetRpc] = 服务器→特定客户端
    /// [ClientRpc] = 服务器→所有客户端
    /// [Command]   = 客户端→服务器
    /// </summary>
    public class ClientBehaviour : NetworkBehaviour
    {
        public static ClientBehaviour Instance { get; private set; }
        private ClientRoundStatus CurrentRoundStatus;
        private ViewController controller;
        public IStateMachine StateMachine { get; private set; }

        private void OnEnable()
        {
            Debug.Log("ClientBehaviour.OnEnable() is called");
            Instance = this;
            StateMachine = new StateMachine();
        }

        private void Start()
        {
            if (isClient)
            {
                var playerName = PlayerPrefs.GetString("PlayerName", "玩家");
                CmdRegisterPlayerName(playerName);
            }

            if (!isServer)
            {
                // 客户端发送加载完成通知（由服务器按连接ID统计）
                CmdLoadComplete();
            }
            controller = ViewController.Instance;
        }

        private void Update()
        {
            StateMachine?.UpdateState();
        }

        // ═══════════════════════════════════════════════════════════════
        // 服务器→特定客户端 的 RPC（替换 [PunRPC] + photonView.RPC(player)）
        // ═══════════════════════════════════════════════════════════════

        [TargetRpc]
        public void TargetRpcGamePrepare(NetworkConnectionToClient target, EventMessages.GamePrepareInfo info)
        {
            CurrentRoundStatus = new ClientRoundStatus(info.PlayerIndex, info.GameSetting);
            var prepareState = new GamePrepareState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                Points = info.Points,
                Names = info.PlayerNames
            };
            StateMachine.ChangeState(prepareState);
        }

        [TargetRpc]
        public void TargetRpcRoundStart(NetworkConnectionToClient target, EventMessages.RoundStartInfo info)
        {
            var startState = new RoundStartState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                LocalPlayerHandTiles = info.InitialHandTiles,
                OyaPlayerIndex = info.OyaPlayerIndex,
                Dice = info.Dice,
                Field = info.Field,
                Extra = info.Extra,
                RichiSticks = info.RichiSticks,
                MahjongSetData = info.MahjongSetData,
                Points = info.Points
            };
            StateMachine.ChangeState(startState);
        }

        [TargetRpc]
        public void TargetRpcDrawTile(NetworkConnectionToClient target, EventMessages.DrawTileInfo info)
        {
            var drawState = new PlayerDrawState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                PlayerIndex = info.DrawPlayerIndex,
                Tile = info.Tile,
                BonusTurnTime = info.BonusTurnTime,
                Zhenting = info.Zhenting,
                MahjongSetData = info.MahjongSetData,
                Operations = info.Operations
            };
            StateMachine.ChangeState(drawState);
        }

        [TargetRpc]
        public void TargetRpcKong(NetworkConnectionToClient target, EventMessages.KongInfo message)
        {
            var kongState = new PlayerKongState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                KongPlayerIndex = message.KongPlayerIndex,
                HandData = message.HandData,
                BonusTurnTime = message.BonusTurnTime,
                Operations = message.Operations,
                MahjongSetData = message.MahjongSetData
            };
            StateMachine.ChangeState(kongState);
        }

        [TargetRpc]
        public void TargetRpcBeiDora(NetworkConnectionToClient target, EventMessages.BeiDoraInfo message)
        {
            var beiDoraState = new PlayerBeiDoraState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                BeiDoraPlayerIndex = message.BeiDoraPlayerIndex,
                BeiDoras = message.BeiDoras,
                HandData = message.HandData,
                BonusTurnTime = message.BonusTurnTime,
                Operations = message.Operations,
                MahjongSetData = message.MahjongSetData
            };
            StateMachine.ChangeState(beiDoraState);
        }

        [TargetRpc]
        public void TargetRpcDiscardOperation(NetworkConnectionToClient target, EventMessages.DiscardOperationInfo info)
        {
            var discardOperationState = new PlayerDiscardOperationState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                CurrentPlayerIndex = info.CurrentTurnPlayerIndex,
                IsRichiing = info.IsRichiing,
                DiscardingLastDraw = info.DiscardingLastDraw,
                Tile = info.Tile,
                BonusTurnTime = info.BonusTurnTime,
                Zhenting = info.Zhenting,
                Operations = info.Operations,
                HandTiles = info.HandTiles,
                Rivers = info.Rivers
            };
            StateMachine.ChangeState(discardOperationState);
        }

        [TargetRpc]
        public void TargetRpcTurnEnd(NetworkConnectionToClient target, EventMessages.TurnEndInfo info)
        {
            var turnEndState = new PlayerTurnEndState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                PlayerIndex = info.PlayerIndex,
                ChosenOperationType = info.ChosenOperationType,
                Operations = info.Operations,
                Points = info.Points,
                RichiStatus = info.RichiStatus,
                RichiSticks = info.RichiSticks,
                Zhenting = info.Zhenting,
                MahjongSetData = info.MahjongSetData
            };
            StateMachine.ChangeState(turnEndState);
        }

        [TargetRpc]
        public void TargetRpcOperationPerform(NetworkConnectionToClient target, EventMessages.OperationPerformInfo info)
        {
            var operationState = new PlayerOperationPerformState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                PlayerIndex = info.PlayerIndex,
                OperationPlayerIndex = info.OperationPlayerIndex,
                Operation = info.Operation,
                HandData = info.HandData,
                BonusTurnTime = info.BonusTurnTime,
                Rivers = info.Rivers,
                MahjongSetData = info.MahjongSetData
            };
            StateMachine.ChangeState(operationState);
        }

        [TargetRpc]
        public void TargetRpcRoundDraw(NetworkConnectionToClient target, EventMessages.RoundDrawInfo info)
        {
            var roundDrawState = new RoundDrawState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                RoundDrawType = info.RoundDrawType,
                WaitingData = info.WaitingData
            };
            StateMachine.ChangeState(roundDrawState);
        }

        // ═══════════════════════════════════════════════════════════════
        // 服务器→所有客户端 的 RPC（替换 RpcTarget.AllBufferedViaServer）
        // ═══════════════════════════════════════════════════════════════

        [ClientRpc]
        public void RpcTsumo(EventMessages.TsumoInfo info)
        {
            var tsumoState = new PlayerTsumoState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                TsumoPlayerIndex = info.TsumoPlayerIndex,
                TsumoPlayerName = info.TsumoPlayerName,
                TsumoHandData = info.TsumoHandData,
                WinningTile = info.WinningTile,
                DoraIndicators = info.DoraIndicators,
                UraDoraIndicators = info.UraDoraIndicators,
                IsRichi = info.IsRichi,
                TsumoPointInfo = info.TsumoPointInfo,
                TotalPoints = info.TotalPoints
            };
            StateMachine.ChangeState(tsumoState);
        }

        [ClientRpc]
        public void RpcRong(EventMessages.RongInfo message)
        {
            var rongState = new PlayerRongState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                RongPlayerIndices = message.RongPlayerIndices,
                RongPlayerNames = message.RongPlayerNames,
                HandData = message.HandData,
                WinningTile = message.WinningTile,
                DoraIndicators = message.DoraIndicators,
                UraDoraIndicators = message.UraDoraIndicators,
                RongPlayerRichiStatus = message.RongPlayerRichiStatus,
                RongPointInfos = message.RongPointInfos,
                TotalPoints = message.TotalPoints
            };
            StateMachine.ChangeState(rongState);
        }

        [ClientRpc]
        public void RpcPointTransfer(EventMessages.PointTransferInfo message)
        {
            var transferState = new PointTransferState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                PlayerNames = message.PlayerNames,
                Points = message.Points,
                PointTransfers = message.PointTransfers
            };
            StateMachine.ChangeState(transferState);
        }

        [ClientRpc]
        public void RpcGameEnd(EventMessages.GameEndInfo message)
        {
            var endState = new GameEndState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                PlayerNames = message.PlayerNames,
                Points = message.Points,
                Places = message.Places
            };
            StateMachine.ChangeState(endState);
        }

        // ═══════════════════════════════════════════════════════════════
        // 客户端→服务器 的 Command（替换 PhotonNetwork.RaiseEvent）
        // ═══════════════════════════════════════════════════════════════

        [Command(requiresAuthority = false)]
        public void CmdLoadComplete(NetworkConnectionToClient sender = null)
        {
            if (sender == null) return;
            ServerBehaviour.Instance?.HandleLoadComplete(sender.connectionId);
        }

        [Command(requiresAuthority = false)]
        public void CmdRegisterPlayerName(string playerName, NetworkConnectionToClient sender = null)
        {
            if (sender == null) return;
            var manager = LiMen.Network.LiMenNetworkManager.singleton;
            if (manager == null) return;
            manager.RegisterPlayer(sender, playerName);
        }

        [Command(requiresAuthority = false)]
        public void CmdClientReady(int playerIndex)
        {
            ServerBehaviour.Instance?.HandleClientReady(playerIndex);
        }

        [Command(requiresAuthority = false)]
        public void CmdDiscardTile(EventMessages.DiscardTileInfo info)
        {
            ServerBehaviour.Instance?.HandleDiscardTile(info);
        }

        [Command(requiresAuthority = false)]
        public void CmdInTurnOperation(EventMessages.InTurnOperationInfo info)
        {
            ServerBehaviour.Instance?.HandleInTurnOperation(info);
        }

        [Command(requiresAuthority = false)]
        public void CmdOutTurnOperation(EventMessages.OutTurnOperationInfo info)
        {
            ServerBehaviour.Instance?.HandleOutTurnOperation(info);
        }

        [Command(requiresAuthority = false)]
        public void CmdNextRound(int playerIndex)
        {
            ServerBehaviour.Instance?.HandleNextRound(playerIndex);
        }

        // ═══════════════════════════════════════════════════════════════
        // 客户端方法（替换 PhotonNetwork.RaiseEvent 调用）
        // ═══════════════════════════════════════════════════════════════

        public void ClientReady()
        {
            CmdClientReady(CurrentRoundStatus.LocalPlayerIndex);
        }

        public void NextRound()
        {
            CmdNextRound(CurrentRoundStatus.LocalPlayerIndex);
        }

        public void OnDiscardTile(Tile tile, bool isLastDraw)
        {
            int bonusTimeLeft = controller.TurnTimeController.StopCountDown();
            OnDiscardTile(tile, isLastDraw, bonusTimeLeft);
        }

        public void OnDiscardTile(Tile tile, bool isLastDraw, int bonusTimeLeft)
        {
            Debug.Log($"Sending request of discarding tile {tile}");
            var info = new EventMessages.DiscardTileInfo
            {
                PlayerIndex = CurrentRoundStatus.LocalPlayerIndex,
                IsRichiing = CurrentRoundStatus.IsRichiing,
                DiscardingLastDraw = isLastDraw,
                Tile = tile,
                BonusTurnTime = bonusTimeLeft
            };
            CmdDiscardTile(info);
            var localDiscardState = new LocalDiscardState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                CurrentPlayerIndex = CurrentRoundStatus.LocalPlayerIndex,
                IsRichiing = CurrentRoundStatus.IsRichiing,
                DiscardingLastDraw = isLastDraw,
                Tile = tile
            };
            StateMachine.ChangeState(localDiscardState);
        }

        private void OnInTurnOperationTaken(InTurnOperation operation, int bonusTurnTime)
        {
            var info = new EventMessages.InTurnOperationInfo
            {
                PlayerIndex = CurrentRoundStatus.LocalPlayerIndex,
                Operation = operation,
                BonusTurnTime = bonusTurnTime
            };
            CmdInTurnOperation(info);
        }

        public void OnSkipOutTurnOperation(int bonusTurnTime)
        {
            OnOutTurnOperationTaken(new OutTurnOperation { Type = OutTurnOperationType.Skip }, bonusTurnTime);
        }

        public void OnOutTurnOperationTaken(OutTurnOperation operation, int bonusTurnTime)
        {
            var info = new EventMessages.OutTurnOperationInfo
            {
                PlayerIndex = CurrentRoundStatus.LocalPlayerIndex,
                Operation = operation,
                BonusTurnTime = bonusTurnTime
            };
            CmdOutTurnOperation(info);
        }

        public void OnInTurnSkipButtonClicked()
        {
            Debug.Log("In turn skip button clicked, hide buttons");
            controller.InTurnPanelManager.Close();
        }

        public void OnTsumoButtonClicked(InTurnOperation operation)
        {
            if (operation.Type != InTurnOperationType.Tsumo)
            {
                Debug.LogError($"Cannot send a operation with type {operation.Type} within OnTsumoButtonClicked method");
                return;
            }
            int bonusTimeLeft = controller.TurnTimeController.StopCountDown();
            Debug.Log($"Sending request of tsumo operation with bonus turn time {bonusTimeLeft}");
            OnInTurnOperationTaken(operation, bonusTimeLeft);
            controller.InTurnPanelManager.Close();
        }

        public void OnRichiButtonClicked(InTurnOperation operation)
        {
            if (operation.Type != InTurnOperationType.Richi)
            {
                Debug.LogError($"Cannot send a operation with type {operation.Type} within OnRichiButtonClicked method");
                return;
            }
            Debug.Log($"Showing richi selection panel, candidates: {string.Join(",", operation.RichiAvailableTiles)}");
            CurrentRoundStatus.SetRichiing(true);
            controller.HandPanelManager.SetCandidates(operation.RichiAvailableTiles);
        }

        public void OnInTurnKongButtonClicked(InTurnOperation[] operationOptions)
        {
            if (operationOptions == null || operationOptions.Length == 0)
            {
                Debug.LogError("The operations are null or empty in OnInTurnKongButtonClicked method, this should not happen.");
                return;
            }
            if (!operationOptions.All(op => op.Type == InTurnOperationType.Kong))
            {
                Debug.LogError("There are incompatible type within OnInTurnKongButtonClicked method");
                return;
            }
            if (operationOptions.Length == 1)
            {
                int bonusTimeLeft = controller.TurnTimeController.StopCountDown();
                Debug.Log($"Sending request of in turn kong operation with bonus turn time {bonusTimeLeft}");
                OnInTurnOperationTaken(operationOptions[0], bonusTimeLeft);
                controller.InTurnPanelManager.Close();
                return;
            }
            controller.InTurnPanelManager.ShowBackButton();
            var meldOptions = operationOptions.Select(op => op.Meld);
            controller.MeldSelectionManager.SetMeldOptions(meldOptions.ToArray(), meld =>
            {
                int bonusTimeLeft = controller.TurnTimeController.StopCountDown();
                Debug.Log($"Sending request of in turn kong operation with bonus turn time {bonusTimeLeft}");
                OnInTurnOperationTaken(new InTurnOperation
                {
                    Type = InTurnOperationType.Kong,
                    Meld = meld
                }, bonusTimeLeft);
                controller.InTurnPanelManager.Close();
                controller.MeldSelectionManager.Close();
            });
        }

        public void OnInTurnButtonClicked(InTurnOperation operation)
        {
            Debug.Log($"Requesting to proceed operation: {operation}");
            int bonusTimeLeft = controller.TurnTimeController.StopCountDown();
            OnInTurnOperationTaken(operation, bonusTimeLeft);
            controller.InTurnPanelManager.Close();
        }

        public void OnInTurnBackButtonClicked(InTurnOperation[] operations)
        {
            controller.InTurnPanelManager.SetOperations(operations);
            controller.MeldSelectionManager.Close();
            controller.HandPanelManager.RemoveCandidates();
            CurrentRoundStatus.SetRichiing(false);
        }

        public void OnOutTurnBackButtonClicked(OutTurnOperation[] operations)
        {
            controller.OutTurnPanelManager.SetOperations(operations);
            controller.MeldSelectionManager.Close();
        }

        public void OnOutTurnButtonClicked(OutTurnOperation operation)
        {
            int bonusTimeLeft = controller.TurnTimeController.StopCountDown();
            Debug.Log($"Sending request of operation {operation} with bonus turn time {bonusTimeLeft}");
            OnOutTurnOperationTaken(operation, bonusTimeLeft);
            controller.OutTurnPanelManager.Close();
        }

        public void OnChowButtonClicked(OutTurnOperation[] operationOptions, OutTurnOperation[] originalOperations)
        {
            if (operationOptions == null || operationOptions.Length == 0)
            {
                Debug.LogError("The operations are null or empty in OnChowButtonClicked method, this should not happen.");
                return;
            }
            if (!operationOptions.All(op => op.Type == OutTurnOperationType.Chow))
            {
                Debug.LogError("There are incompatible type within OnChowButtonClicked method");
                return;
            }
            if (operationOptions.Length == 1)
            {
                int bonusTimeLeft = controller.TurnTimeController.StopCountDown();
                Debug.Log($"Sending request of chow operation with bonus turn time {bonusTimeLeft}");
                OnOutTurnOperationTaken(operationOptions[0], bonusTimeLeft);
                controller.OutTurnPanelManager.Close();
                return;
            }
            controller.OutTurnPanelManager.ShowBackButton();
            var meldOptions = operationOptions.Select(op => op.Meld);
            controller.MeldSelectionManager.SetMeldOptions(meldOptions.ToArray(), meld =>
            {
                int bonusTimeLeft = controller.TurnTimeController.StopCountDown();
                Debug.Log($"Sending request of chow operation with bonus turn time {bonusTimeLeft}");
                OnOutTurnOperationTaken(new OutTurnOperation
                {
                    Type = OutTurnOperationType.Chow,
                    Meld = meld
                }, bonusTimeLeft);
                controller.OutTurnPanelManager.Close();
                controller.MeldSelectionManager.Close();
            });
        }

        public void OnPongButtonClicked(OutTurnOperation[] operationOptions, OutTurnOperation[] originalOperations)
        {
            if (operationOptions == null || operationOptions.Length == 0)
            {
                Debug.LogError("The operations are null or empty in OnPongButtonClicked method, this should not happen.");
                return;
            }
            if (!operationOptions.All(op => op.Type == OutTurnOperationType.Pong))
            {
                Debug.LogError("There are incompatible type within OnPongButtonClicked method");
                return;
            }
            if (operationOptions.Length == 1)
            {
                int bonusTimeLeft = controller.TurnTimeController.StopCountDown();
                Debug.Log($"Sending request of pong operation with bonus turn time {bonusTimeLeft}");
                OnOutTurnOperationTaken(operationOptions[0], bonusTimeLeft);
                controller.OutTurnPanelManager.Close();
                return;
            }
            controller.OutTurnPanelManager.ShowBackButton();
            var meldOptions = operationOptions.Select(op => op.Meld);
            controller.MeldSelectionManager.SetMeldOptions(meldOptions.ToArray(), meld =>
            {
                int bonusTimeLeft = controller.TurnTimeController.StopCountDown();
                Debug.Log($"Sending request of pong operation with bonus turn time {bonusTimeLeft}");
                OnOutTurnOperationTaken(new OutTurnOperation
                {
                    Type = OutTurnOperationType.Pong,
                    Meld = meld
                }, bonusTimeLeft);
                controller.OutTurnPanelManager.Close();
                controller.MeldSelectionManager.Close();
            });
        }
    }
}
