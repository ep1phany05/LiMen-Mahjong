using System.Collections.Generic;
using GamePlay.Server.Model;
using GamePlay.Server.Model.Events;
using Mirror;
using UnityEngine;


namespace GamePlay.Server.Controller.GameState
{
    /// <summary>
    /// When server is in this state, the server waits for ReadinessMessage from every player.
    /// When the server gets enough ReadinessMessages, the server transfers to GamePrepareState.
    /// Otherwise the server will resend the messages to not-responding clients until get enough responds or time out.
    /// When time out, the server transfers to GameAbortState.
    /// </summary>
    public class WaitForLoadingState : ServerState
    {
        public int TotalPlayers;
        public int ExpectedResponses;
        private ISet<int> responds;
        private float firstTime;
        public float serverTimeOut;

        public override void OnServerStateEnter()
        {
            ServerBehaviour.Instance.OnLoadCompleteReceived += HandleLoadComplete;
            responds = new HashSet<int>();
            firstTime = Time.time;
            serverTimeOut = ServerConstants.ServerWaitForLoadingTimeOut;
        }

        public override void OnServerStateExit()
        {
            ServerBehaviour.Instance.OnLoadCompleteReceived -= HandleLoadComplete;
        }

        public override void OnStateUpdate()
        {
            int targetResponses = Mathf.Max(ExpectedResponses, 0);
            if (responds.Count >= targetResponses)
            {
                Debug.Log("All set, game start");
                ServerBehaviour.Instance.GamePrepare();
                return;
            }
            if (Time.time - firstTime > serverTimeOut)
            {
                Debug.Log("Time out");
                ServerBehaviour.Instance.GameAbort();
                return;
            }
        }

        private void HandleLoadComplete(int connectionId)
        {
            Debug.Log($"Received LoadComplete event from connection {connectionId}");
            responds.Add(connectionId);
        }
    }
}
