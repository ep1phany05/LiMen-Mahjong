using System.Collections.Generic;
using System.Linq;
using Common.StateMachine;
using Common.StateMachine.Interfaces;
using GamePlay.Server.Controller.GameState;
using GamePlay.Server.Model;
using GamePlay.Server.Model.Events;
using LiMen.Network;
using Managers;
using Mahjong.Logic;
using Mahjong.Model;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GamePlay.Server.Controller
{
    /// <summary>
    /// This class only takes effect on server (Host).
    /// Mirror 版本：替换了 Photon PUN2 的所有网络调用。
    /// </summary>
    public class ServerBehaviour : NetworkBehaviour
    {
        public string lobbySceneName = "PUN_Lobby";
        [HideInInspector] public GameSetting GameSettings;
        public IStateMachine StateMachine { get; private set; }
        private MahjongSet mahjongSet;
        private bool useSichuanMode;
        public ServerRoundStatus CurrentRoundStatus = null;
        public static ServerBehaviour Instance { get; private set; }

        // ─── 服务器端事件（GameState 订阅这些事件来接收客户端消息） ───
        public event System.Action<int> OnLoadCompleteReceived;
        public event System.Action<int> OnClientReadyReceived;
        public event System.Action<EventMessages.DiscardTileInfo> OnDiscardTileReceived;
        public event System.Action<EventMessages.InTurnOperationInfo> OnInTurnOperationReceived;
        public event System.Action<EventMessages.OutTurnOperationInfo> OnOutTurnOperationReceived;
        public event System.Action<int> OnNextRoundReceived;

        private void OnEnable()
        {
            Debug.Log("[Server] ServerBehaviour.OnEnable() is called");
            if (!NetworkServer.active)
            {
                SceneManager.LoadScene(lobbySceneName);
                return;
            }
            if (!isServer) return;
            Instance = this;
            StateMachine = new StateMachine();
            ReadSetting();
            WaitForOthersLoading();
        }

        private void Start()
        {
            if (!isServer)
                enabled = false;
        }

        private void Update()
        {
            if (!isServer) return;
            StateMachine?.UpdateState();
        }

        private void ReadSetting()
        {
            if (GameSettings == null)
            {
                if (ResourceManager.Instance != null)
                    ResourceManager.Instance.LoadSettings(out GameSettings);
                else
                    GameSettings = new GameSetting();
            }

            var manager = LiMenNetworkManager.singleton;
            int connectedPlayers = manager != null ? manager.ConnectedPlayers.Count : 0;
            if (connectedPlayers >= 2)
            {
                GameSettings.GamePlayers = connectedPlayers switch
                {
                    2 => GamePlayers.Two,
                    3 => GamePlayers.Three,
                    _ => GamePlayers.Four
                };
            }

            var mode = PlayerPrefs.GetString("GameMode", "Riichi");
            if (mode == "Sichuan")
            {
                InitializeSichuanGame();
            }
            else
            {
                InitializeRiichiGame();
            }
        }

        private void InitializeRiichiGame()
        {
            useSichuanMode = false;
            Debug.Log("[Server] Game mode: Riichi");
        }

        private void InitializeSichuanGame()
        {
            useSichuanMode = true;
            Debug.Log("[Server] Game mode: Sichuan");
        }

        private void WaitForOthersLoading()
        {
            var manager = LiMenNetworkManager.singleton;
            int connectedPlayers = manager != null ? manager.ConnectedPlayers.Count : GameSettings.MaxPlayer;
            connectedPlayers = Mathf.Max(connectedPlayers, 1);

            var waitingState = new WaitForLoadingState
            {
                TotalPlayers = connectedPlayers,
                ExpectedResponses = Mathf.Max(connectedPlayers - 1, 0)
            };
            StateMachine.ChangeState(waitingState);
        }

        public void GamePrepare()
        {
            var manager = LiMenNetworkManager.singleton;
            var connections = manager.GetPlayerConnections();
            var names = manager.GetPlayerNames();
            CurrentRoundStatus = new ServerRoundStatus(GameSettings, connections, names);
            var tiles = useSichuanMode
                ? SichuanLogic.CreateSichuanTileSet()
                : new List<Tile>(GameSettings.GetAllTiles());
            mahjongSet = new MahjongSet(GameSettings, tiles);
            var prepareState = new GamePrepareState
            {
                CurrentRoundStatus = CurrentRoundStatus,
            };
            StateMachine.ChangeState(prepareState);
        }

        public void GameAbort()
        {
            Debug.LogError("The game aborted, this part is still under construction");
        }

        public void RoundStart(bool next, bool extra, bool keepSticks)
        {
            var startState = new RoundStartState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                MahjongSet = mahjongSet,
                NextRound = next,
                ExtraRound = extra,
                KeepSticks = keepSticks
            };
            StateMachine.ChangeState(startState);
        }

        public void DrawTile(int playerIndex, bool isLingShang = false, bool turnDoraAfterDiscard = false)
        {
            var drawState = new PlayerDrawTileState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                CurrentPlayerIndex = playerIndex,
                MahjongSet = mahjongSet,
                IsLingShang = isLingShang,
                TurnDoraAfterDiscard = turnDoraAfterDiscard
            };
            StateMachine.ChangeState(drawState);
        }

        public void DiscardTile(int playerIndex, Tile tile, bool isRichiing, bool discardLastDraw, int bonusTurnTime, bool turnDoraAfterDiscard)
        {
            var discardState = new PlayerDiscardTileState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                CurrentPlayerIndex = playerIndex,
                DiscardTile = tile,
                IsRichiing = isRichiing,
                DiscardLastDraw = discardLastDraw,
                BonusTurnTime = bonusTurnTime,
                MahjongSet = mahjongSet,
                TurnDoraAfterDiscard = turnDoraAfterDiscard
            };
            StateMachine.ChangeState(discardState);
        }

        public void TurnEnd(int playerIndex, Tile discardingTile, bool isRichiing, OutTurnOperation[] operations,
            bool isRobKong, bool turnDoraAfterDiscard)
        {
            var turnEndState = new TurnEndState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                CurrentPlayerIndex = playerIndex,
                DiscardingTile = discardingTile,
                IsRichiing = isRichiing,
                Operations = operations,
                MahjongSet = mahjongSet,
                IsRobKong = isRobKong,
                TurnDoraAfterDiscard = turnDoraAfterDiscard
            };
            StateMachine.ChangeState(turnEndState);
        }

        public void PerformOutTurnOperation(int newPlayerIndex, OutTurnOperation operation)
        {
            var operationPerformState = new OperationPerformState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                CurrentPlayerIndex = newPlayerIndex,
                DiscardPlayerIndex = CurrentRoundStatus.CurrentPlayerIndex,
                Operation = operation,
                MahjongSet = mahjongSet
            };
            StateMachine.ChangeState(operationPerformState);
        }

        public void Tsumo(int currentPlayerIndex, Tile winningTile, PointInfo pointInfo)
        {
            var tsumoState = new PlayerTsumoState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                TsumoPlayerIndex = currentPlayerIndex,
                WinningTile = winningTile,
                MahjongSet = mahjongSet,
                TsumoPointInfo = pointInfo
            };
            StateMachine.ChangeState(tsumoState);
        }

        public void Rong(int currentPlayerIndex, Tile winningTile, int[] rongPlayerIndices, PointInfo[] rongPointInfos)
        {
            var rongState = new PlayerRongState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                CurrentPlayerIndex = currentPlayerIndex,
                RongPlayerIndices = rongPlayerIndices,
                WinningTile = winningTile,
                MahjongSet = mahjongSet,
                RongPointInfos = rongPointInfos
            };
            StateMachine.ChangeState(rongState);
        }

        public void Kong(int playerIndex, OpenMeld kong)
        {
            var kongState = new PlayerKongState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                CurrentPlayerIndex = playerIndex,
                MahjongSet = mahjongSet,
                Kong = kong
            };
            StateMachine.ChangeState(kongState);
        }

        public void RoundDraw(RoundDrawType type)
        {
            var drawState = new RoundDrawState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                RoundDrawType = type
            };
            StateMachine.ChangeState(drawState);
        }

        public void BeiDora(int playerIndex)
        {
            var beiState = new PlayerBeiDoraState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                CurrentPlayerIndex = playerIndex,
                MahjongSet = mahjongSet
            };
            StateMachine.ChangeState(beiState);
        }

        public void PointTransfer(IList<PointTransfer> transfers, bool next, bool extra, bool keepSticks)
        {
            var transferState = new PointTransferState
            {
                CurrentRoundStatus = CurrentRoundStatus,
                NextRound = next,
                ExtraRound = extra,
                KeepSticks = keepSticks,
                PointTransferList = transfers
            };
            StateMachine.ChangeState(transferState);
        }

        public void GameEnd()
        {
            var gameEndState = new GameEndState
            {
                CurrentRoundStatus = CurrentRoundStatus
            };
            StateMachine.ChangeState(gameEndState);
        }

        // ─── 接收客户端消息的处理方法（由 ClientBehaviour 的 [Command] 转发） ───

        public void HandleLoadComplete(int connectionId)
        {
            OnLoadCompleteReceived?.Invoke(connectionId);
        }

        public void HandleClientReady(int playerIndex)
        {
            OnClientReadyReceived?.Invoke(playerIndex);
        }

        public void HandleDiscardTile(EventMessages.DiscardTileInfo info)
        {
            OnDiscardTileReceived?.Invoke(info);
        }

        public void HandleInTurnOperation(EventMessages.InTurnOperationInfo info)
        {
            OnInTurnOperationReceived?.Invoke(info);
        }

        public void HandleOutTurnOperation(EventMessages.OutTurnOperationInfo info)
        {
            OnOutTurnOperationReceived?.Invoke(info);
        }

        public void HandleNextRound(int playerIndex)
        {
            OnNextRoundReceived?.Invoke(playerIndex);
        }
    }
}
