using LiMen.Network;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utils;

namespace PUNLobby.Room
{
    public class RoomLauncher : MonoBehaviour
    {
        public static RoomLauncher Instance { get; private set; }

        public SceneField lobbyScene;
        public SceneField mahjongScene;
        public RoomPanelManager roomPanelManager;

        public string LobbySceneName => string.IsNullOrEmpty(lobbyScene?.SceneName) ? "PUN_Lobby" : lobbyScene.SceneName;
        public string MahjongSceneName => string.IsNullOrEmpty(mahjongScene?.SceneName) ? "PUN_Mahjong" : mahjongScene.SceneName;

        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            var manager = LiMenNetworkManager.singleton;
            if (manager == null || (!NetworkClient.active && !NetworkServer.active))
            {
                Debug.Log("Not in network session, returning back to Lobby.");
                SceneManager.LoadScene(LobbySceneName);
                return;
            }

            if (roomPanelManager != null)
            {
                roomPanelManager.SetTitle("Room");
                roomPanelManager.RefreshRoomState();
            }
        }

        public void GameStart()
        {
            var manager = LiMenNetworkManager.singleton;
            if (manager == null || !NetworkServer.active) return;
            manager.ServerChangeScene(MahjongSceneName);
        }
    }
}
