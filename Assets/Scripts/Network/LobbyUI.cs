using System.Collections.Generic;
using LiMen.Network;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LiMen.UI
{
    public class LobbyUI : MonoBehaviour
    {
        [Header("Input")]
        public TMP_InputField playerNameInput;

        [Header("Buttons")]
        public Button hostButton;
        public Button refreshButton;
        public Button joinButton;

        [Header("Room List")]
        public Transform roomListParent;
        public GameObject roomEntryPrefab;

        private LiMenNetworkDiscovery _discovery;
        private readonly Dictionary<System.Net.IPEndPoint, DiscoveryResponse> _discoveredServers = new();
        private DiscoveryResponse _selectedServer;
        private bool _hasSelection;

        void Start()
        {
            _discovery = FindObjectOfType<LiMenNetworkDiscovery>();
            if (_discovery == null)
            {
                Debug.LogError("[LobbyUI] LiMenNetworkDiscovery not found in scene.");
                return;
            }

            _discovery.OnServerFound.AddListener(OnServerFound);

            hostButton?.onClick.AddListener(OnHostClicked);
            refreshButton?.onClick.AddListener(OnRefreshClicked);
            joinButton?.onClick.AddListener(OnJoinClicked);
            if (joinButton != null) joinButton.interactable = false;

            if (playerNameInput != null)
                playerNameInput.text = PlayerPrefs.GetString("PlayerName", "Player");
        }

        void OnDestroy()
        {
            if (_discovery != null)
                _discovery.OnServerFound.RemoveListener(OnServerFound);
        }

        void OnHostClicked()
        {
            if (LiMenNetworkManager.singleton == null || _discovery == null) return;
            SavePlayerName();
            LiMenNetworkManager.singleton.StartHost();
            _discovery.AdvertiseServer();
        }

        void OnRefreshClicked()
        {
            if (_discovery == null) return;
            _hasSelection = false;
            _discoveredServers.Clear();
            ClearRoomList();
            if (joinButton != null) joinButton.interactable = false;
            _discovery.StartDiscovery();
        }

        void OnJoinClicked()
        {
            if (!_hasSelection || LiMenNetworkManager.singleton == null) return;
            SavePlayerName();
            LiMenNetworkManager.singleton.networkAddress = _selectedServer.EndPoint.Address.ToString();
            LiMenNetworkManager.singleton.StartClient();
        }

        void OnServerFound(DiscoveryResponse response)
        {
            _discoveredServers[response.EndPoint] = response;
            RefreshRoomList();
        }

        void RefreshRoomList()
        {
            ClearRoomList();
            foreach (var kvp in _discoveredServers)
            {
                if (roomEntryPrefab == null || roomListParent == null) break;

                var entry = Instantiate(roomEntryPrefab, roomListParent);
                var text = entry.GetComponentInChildren<TMP_Text>();
                var btn = entry.GetComponent<Button>();
                var room = kvp.Value;

                if (text != null)
                    text.text = $"{room.hostName}'s Room [{room.playerCount}/{room.maxPlayers}] {room.gameMode}";

                if (btn != null)
                {
                    btn.onClick.AddListener(() =>
                    {
                        _selectedServer = room;
                        _hasSelection = true;
                        if (joinButton != null) joinButton.interactable = true;
                    });
                }
            }
        }

        void ClearRoomList()
        {
            if (roomListParent == null) return;
            foreach (Transform child in roomListParent)
                Destroy(child.gameObject);
        }

        void SavePlayerName()
        {
            string name = playerNameInput != null ? playerNameInput.text : "Player";
            PlayerPrefs.SetString("PlayerName", name);
            PlayerPrefs.Save();
        }
    }
}
