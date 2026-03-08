using System.Linq;
using GamePlay.Client.Controller;
using GamePlay.Server.Model.Events;
using Mirror;
using UnityEngine;

namespace GamePlay.Server.Controller.GameState
{
    /// <summary>
    /// When the server is in this state, the server make preparation that the game needs.
    /// The playerIndex is arranged in this state, so is the settings. Messages will be sent to clients to inform the information.
    /// Transfers to RoundStartState. The state transfer will be done regardless whether enough client responds received.
    /// </summary>
    public class GamePrepareState : ServerState
    {
        private bool[] responds;
        private float firstTime;
        private float serverTimeOut = 5f;
        public override void OnServerStateEnter()
        {
            Debug.Log($"This game has total {players.Count} players");
            ServerBehaviour.Instance.OnClientReadyReceived += HandleClientReady;
            responds = new bool[players.Count];
            firstTime = Time.time;
            CurrentRoundStatus.ShufflePlayers();
            AssignInitialPoints();
            ClientRpcCalls();
        }

        private void AssignInitialPoints()
        {
            for (int i = 0; i < players.Count; i++)
            {
                CurrentRoundStatus.SetPoints(i, CurrentRoundStatus.GameSettings.InitialPoints);
            }
        }

        private void ClientRpcCalls()
        {
            for (int i = 0; i < CurrentRoundStatus.TotalPlayers; i++)
            {
                var conn = CurrentRoundStatus.GetConnection(i);
                ClientBehaviour.Instance.TargetRpcGamePrepare(conn, new EventMessages.GamePrepareInfo
                {
                    PlayerIndex = i,
                    Points = CurrentRoundStatus.Points,
                    PlayerNames = CurrentRoundStatus.PlayerNames,
                    GameSetting = gameSettings.ToString()
                });
            }
        }

        public override void OnServerStateExit()
        {
            ServerBehaviour.Instance.OnClientReadyReceived -= HandleClientReady;
        }

        public override void OnStateUpdate()
        {
            if (responds.All(r => r))
            {
                ServerBehaviour.Instance.RoundStart(true, false, false);
                return;
            }
            if (Time.time - firstTime > serverTimeOut)
            {
                Debug.Log("[Server] Prepare state time out");
                ServerBehaviour.Instance.RoundStart(true, false, false);
                return;
            }
        }

        private void HandleClientReady(int index)
        {
            Debug.Log($"{GetType().Name} receives ClientReadyEvent with content {index}");
            responds[index] = true;
        }
    }
}
