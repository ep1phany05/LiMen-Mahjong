using System.Linq;
using LiMen.Network;
using LiMen.UI;
using Mahjong.Model;
using Managers;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PUNLobby.Room
{
    public class RoomPanelManager : MonoBehaviour
    {
        public Text roomTitleText;
        public RoomSlotPanel[] slots;
        public Button checkRuleButton;
        public Button readyButton;
        public Button cancelButton;
        public Button startButton;
        public RulePanel rulePanel;
        public WarningPanel warningPanel;

        private LiMenNetworkManager manager;
        private RoomStateSnapshot latestState;
        private bool hasState;

        private void OnEnable()
        {
            AttachManager();
            GameModeSelector.OnModeSelectionStateChanged += HandleModeSelectionChanged;
        }

        private void Start()
        {
            AttachManager();
            if (checkRuleButton == null)
                checkRuleButton = FindButtonByName("CheckRuleButton");
            CheckButtonForMaster();
            RefreshRoomState();
            UpdateButtons();
        }

        private void OnDisable()
        {
            DetachManager();
            GameModeSelector.OnModeSelectionStateChanged -= HandleModeSelectionChanged;
        }

        private void AttachManager()
        {
            if (manager != null) return;
            manager = LiMenNetworkManager.singleton;
            if (manager != null)
                manager.OnRoomStateChanged += OnRoomStateChanged;
        }

        private void DetachManager()
        {
            if (manager != null)
                manager.OnRoomStateChanged -= OnRoomStateChanged;
            manager = null;
        }

        public void RefreshRoomState()
        {
            AttachManager();
            manager?.RequestRoomState();
            if (manager != null)
            {
                latestState = manager.CurrentRoomState;
                if (latestState.Players != null && latestState.Players.Length > 0)
                {
                    hasState = true;
                    UpdateView();
                }
            }
        }

        public void SetTitle(string title)
        {
            if (roomTitleText != null)
                roomTitleText.text = title;
        }

        public void CheckButtonForMaster()
        {
            UpdateButtons();
        }

        public void LeaveRoom()
        {
            AttachManager();
            if (manager != null)
            {
                if (NetworkServer.active && NetworkClient.active)
                    manager.StopHost();
                else if (NetworkClient.active)
                    manager.StopClient();
                else if (NetworkServer.active)
                    manager.StopServer();
                else
                    LoadLobbyScene();
                return;
            }

            LoadLobbyScene();
        }

        public void CheckRule()
        {
            if (!GameModeSelector.HasModeSelectionConfirmed)
            {
                warningPanel?.Show(460, 220, "Please select Riichi or Sichuan first.");
                return;
            }

            var gameSetting = new GameSetting();
            if (ResourceManager.Instance != null)
                ResourceManager.Instance.LoadSettings(out gameSetting);
            rulePanel?.Show(gameSetting);
        }

        public void OnStartButtonClicked()
        {
            if (!hasState)
            {
                warningPanel?.Show(460, 220, "Room state not ready.");
                return;
            }

            if (!HasEnoughPlayers())
            {
                warningPanel?.Show(460, 220, "At least 2 players are required.");
                return;
            }

            if (!IsLocalHost())
            {
                warningPanel?.Show(460, 220, "Only host can start the game.");
                return;
            }

            if (!AllPlayersReady())
            {
                warningPanel?.Show(500, 220, "Game cannot start, some players are not ready.");
                return;
            }

            AttachManager();
            manager?.RequestStartGame();
        }

        public void OnReadyButtonClicked()
        {
            AttachManager();
            manager?.SetLocalReady(true);
        }

        public void OnCancelButtonClicked()
        {
            AttachManager();
            manager?.SetLocalReady(false);
        }

        private void OnRoomStateChanged(RoomStateSnapshot state)
        {
            latestState = state;
            hasState = state.Players != null;
            UpdateView();
        }

        private void UpdateView()
        {
            if (!hasState) return;
            ShowSlots();
            UpdateTitle();
            UpdateButtons();
        }

        private void ShowSlots()
        {
            int length = latestState.Players?.Length ?? 0;
            for (int i = 0; i < slots.Length; i++)
            {
                bool show = i < length;
                if (slots[i] == null) continue;

                slots[i].gameObject.SetActive(show);
                if (!show) continue;

                var player = latestState.Players[i];
                slots[i].Set(player.IsHost, player.Name, player.IsReady);
            }
        }

        private void UpdateTitle()
        {
            if (roomTitleText == null) return;
            int playerCount = latestState.Players?.Length ?? 0;
            int maxCount = manager != null ? manager.maxPlayers : 4;
            roomTitleText.text = $"Room [{playerCount}/{maxCount}]";
        }

        private void UpdateButtons()
        {
            bool isHost = IsLocalHost();
            bool enoughPlayers = HasEnoughPlayers();
            bool allReady = AllPlayersReady();
            bool localReady = IsLocalReady();
            bool hasModeSelection = GameModeSelector.HasModeSelectionConfirmed;

            if (startButton != null)
                startButton.interactable = isHost && enoughPlayers && allReady;

            if (checkRuleButton != null)
                checkRuleButton.interactable = hasModeSelection;

            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(true);
                readyButton.interactable = !isHost && !localReady;
            }

            if (cancelButton != null)
            {
                cancelButton.gameObject.SetActive(false);
                cancelButton.interactable = false;
            }
        }

        private bool IsLocalHost()
        {
            if (!hasState) return false;
            return latestState.LocalConnectionId == latestState.HostConnectionId;
        }

        private bool AllPlayersReady()
        {
            if (!hasState || latestState.Players == null || latestState.Players.Length == 0)
                return false;
            return latestState.Players.All(p => p.IsReady);
        }

        private bool HasEnoughPlayers()
        {
            return hasState && latestState.Players != null && latestState.Players.Length >= 2;
        }

        private bool IsLocalReady()
        {
            if (!hasState || latestState.Players == null) return false;
            int localId = latestState.LocalConnectionId;
            foreach (var player in latestState.Players)
            {
                if (player.ConnectionId == localId)
                    return player.IsReady;
            }
            return false;
        }

        private static void LoadLobbyScene()
        {
            string lobbyScene = RoomLauncher.Instance != null
                ? RoomLauncher.Instance.LobbySceneName
                : "PUN_Lobby";
            SceneManager.LoadScene(lobbyScene);
        }

        private void HandleModeSelectionChanged(bool _)
        {
            UpdateButtons();
        }

        private Button FindButtonByName(string objectName)
        {
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                if (button != null && button.name == objectName)
                    return button;
            }

            return null;
        }
    }
}
