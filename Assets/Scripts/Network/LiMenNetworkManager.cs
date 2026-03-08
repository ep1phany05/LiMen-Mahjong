using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LiMen.Network
{
    public struct RoomPlayerState
    {
        public int ConnectionId;
        public string Name;
        public bool IsReady;
        public bool IsHost;
    }

    public struct RoomStateSnapshot
    {
        public int LocalConnectionId;
        public int HostConnectionId;
        public string GameMode;
        public RoomPlayerState[] Players;
    }

    public struct RegisterPlayerNameMessage : NetworkMessage
    {
        public string playerName;
    }

    public struct SetReadyStateMessage : NetworkMessage
    {
        public bool isReady;
    }

    public struct SetGameModeMessage : NetworkMessage
    {
        public string gameMode;
    }

    public struct RequestRoomStateMessage : NetworkMessage
    {
    }

    public struct StartGameRequestMessage : NetworkMessage
    {
    }

    public struct RoomStateMessage : NetworkMessage
    {
        public int localConnectionId;
        public int hostConnectionId;
        public string gameMode;
        public int[] connectionIds;
        public string[] playerNames;
        public bool[] readyStates;
    }

    public class LiMenNetworkManager : NetworkManager
    {
        public static new LiMenNetworkManager singleton => (LiMenNetworkManager)NetworkManager.singleton;

        [Header("LiMen Settings")]
        [Tooltip("最大玩家数（含AI槽位用4）")]
        public int maxPlayers = 4;

        /// <summary>
        /// 已连接玩家信息（按连接顺序）
        /// </summary>
        private readonly List<PlayerConnectionInfo> _players = new();
        private readonly Dictionary<int, bool> _readyStates = new();
        private int _hostConnectionId = -1;
        private string _selectedGameMode = "Riichi";

        public struct PlayerConnectionInfo
        {
            public NetworkConnectionToClient Connection;
            public string Name;
        }

        public IReadOnlyList<PlayerConnectionInfo> ConnectedPlayers => _players;
        public RoomStateSnapshot CurrentRoomState { get; private set; }
        public event Action<RoomStateSnapshot> OnRoomStateChanged;

        public override void Awake()
        {
            base.Awake();
            // 该项目使用场景内管理器，不依赖自动生成的玩家对象。
            autoCreatePlayer = false;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _selectedGameMode = NormalizeGameMode(PlayerPrefs.GetString("GameMode", "Riichi"));
            NetworkServer.RegisterHandler<RegisterPlayerNameMessage>(OnRegisterPlayerNameMessage, false);
            NetworkServer.RegisterHandler<SetReadyStateMessage>(OnSetReadyStateMessage, false);
            NetworkServer.RegisterHandler<SetGameModeMessage>(OnSetGameModeMessage, false);
            NetworkServer.RegisterHandler<RequestRoomStateMessage>(OnRequestRoomStateMessage, false);
            NetworkServer.RegisterHandler<StartGameRequestMessage>(OnStartGameRequestMessage, false);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            NetworkClient.RegisterHandler<RoomStateMessage>(OnRoomStateMessage, false);
        }

        public override void OnStopClient()
        {
            NetworkClient.UnregisterHandler<RoomStateMessage>();
            CurrentRoomState = default;
            base.OnStopClient();
        }

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);
            if (_players.Count == 0)
                _hostConnectionId = conn.connectionId;

            RegisterPlayer(conn, $"玩家{conn.connectionId}", false);
            _readyStates[conn.connectionId] = conn.connectionId == _hostConnectionId;
            BroadcastRoomState();
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            // 不创建默认 playerPrefab，连接信息由 RegisterPlayer 维护。
            Debug.Log($"[LiMen] 收到 AddPlayer 请求: connId={conn.connectionId}");
        }

        /// <summary>
        /// 注册玩家名字（由客户端连接后调用）
        /// </summary>
        public void RegisterPlayer(NetworkConnectionToClient conn, string playerName, bool broadcast = true)
        {
            if (conn == null) return;
            int existingIndex = _players.FindIndex(p => p.Connection == conn);
            if (existingIndex >= 0)
            {
                _players[existingIndex] = new PlayerConnectionInfo
                {
                    Connection = conn,
                    Name = playerName
                };
                if (broadcast && NetworkServer.active)
                    BroadcastRoomState();
                return;
            }

            _players.Add(new PlayerConnectionInfo
            {
                Connection = conn,
                Name = playerName
            });
            if (!_readyStates.ContainsKey(conn.connectionId))
                _readyStates[conn.connectionId] = conn.connectionId == _hostConnectionId;

            Debug.Log($"[LiMen] 注册玩家: {playerName} (connId={conn.connectionId}), 当前人数: {_players.Count}");
            if (broadcast && NetworkServer.active)
                BroadcastRoomState();
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();
            var playerName = PlayerPrefs.GetString("PlayerName", "Player");
            NetworkClient.Send(new RegisterPlayerNameMessage { playerName = playerName });
            NetworkClient.Send(new RequestRoomStateMessage());
            Debug.Log("[LiMen] 客户端连接成功");
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();
            Debug.Log("[LiMen] 客户端断开连接");
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            _players.RemoveAll(p => p.Connection == conn);
            _readyStates.Remove(conn.connectionId);

            if (conn.connectionId == _hostConnectionId)
            {
                _hostConnectionId = _players.Count > 0 ? _players[0].Connection.connectionId : -1;
                if (_hostConnectionId >= 0)
                    _readyStates[_hostConnectionId] = true;
            }

            base.OnServerDisconnect(conn);
            Debug.Log($"[LiMen] 玩家断开: connId={conn.connectionId}, 剩余人数: {_players.Count}");
            BroadcastRoomState();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            _players.Clear();
            _readyStates.Clear();
            _hostConnectionId = -1;
            _selectedGameMode = "Riichi";
        }

        /// <summary>获取所有已注册玩家的连接列表</summary>
        public List<NetworkConnectionToClient> GetPlayerConnections()
        {
            return _players.Select(p => p.Connection).ToList();
        }

        /// <summary>获取所有已注册玩家名</summary>
        public List<string> GetPlayerNames()
        {
            return _players.Select(p => p.Name).ToList();
        }

        public void SetLocalReady(bool isReady)
        {
            if (!NetworkClient.active) return;
            NetworkClient.Send(new SetReadyStateMessage { isReady = isReady });
        }

        public void SetSelectedGameMode(string gameMode)
        {
            var normalized = NormalizeGameMode(gameMode);
            PlayerPrefs.SetString("GameMode", normalized);
            PlayerPrefs.Save();

            if (!NetworkClient.active) return;
            NetworkClient.Send(new SetGameModeMessage { gameMode = normalized });
        }

        public void RequestStartGame()
        {
            if (!NetworkClient.active) return;
            NetworkClient.Send(new StartGameRequestMessage());
        }

        public void RequestRoomState()
        {
            if (!NetworkClient.active) return;
            NetworkClient.Send(new RequestRoomStateMessage());
        }

        private void OnRegisterPlayerNameMessage(NetworkConnectionToClient conn, RegisterPlayerNameMessage msg)
        {
            string fallbackName = $"玩家{conn.connectionId}";
            string playerName = string.IsNullOrWhiteSpace(msg.playerName) ? fallbackName : msg.playerName.Trim();
            RegisterPlayer(conn, playerName);
        }

        private void OnSetReadyStateMessage(NetworkConnectionToClient conn, SetReadyStateMessage msg)
        {
            _readyStates[conn.connectionId] = msg.isReady;
            BroadcastRoomState();
        }

        private void OnSetGameModeMessage(NetworkConnectionToClient conn, SetGameModeMessage msg)
        {
            if (conn.connectionId != _hostConnectionId) return;

            _selectedGameMode = NormalizeGameMode(msg.gameMode);
            PlayerPrefs.SetString("GameMode", _selectedGameMode);
            PlayerPrefs.Save();
            BroadcastRoomState();
        }

        private void OnRequestRoomStateMessage(NetworkConnectionToClient conn, RequestRoomStateMessage msg)
        {
            SendRoomState(conn);
        }

        private void OnStartGameRequestMessage(NetworkConnectionToClient conn, StartGameRequestMessage msg)
        {
            if (conn.connectionId != _hostConnectionId) return;

            if (!string.Equals(SceneManager.GetActiveScene().name, "PUN_Room", StringComparison.Ordinal))
            {
                Debug.LogWarning("[LiMen] StartGame ignored: active scene is not PUN_Room.");
                return;
            }

            if (_players.Count < 2)
            {
                Debug.LogWarning("[LiMen] StartGame rejected: at least 2 players are required.");
                SendRoomState(conn);
                return;
            }

            if (!AllPlayersReady())
            {
                Debug.LogWarning("[LiMen] StartGame rejected: not all players are ready.");
                SendRoomState(conn);
                return;
            }

            ServerChangeScene("PUN_Mahjong");
        }

        private bool AllPlayersReady()
        {
            if (_players.Count == 0) return false;

            foreach (var player in _players)
            {
                int connectionId = player.Connection.connectionId;
                if (!_readyStates.TryGetValue(connectionId, out bool isReady) || !isReady)
                    return false;
            }

            return true;
        }

        private void BroadcastRoomState()
        {
            if (!NetworkServer.active) return;

            foreach (var player in _players)
            {
                if (player.Connection != null)
                    SendRoomState(player.Connection);
            }
        }

        private void SendRoomState(NetworkConnectionToClient conn)
        {
            if (conn == null) return;

            int count = _players.Count;
            var connectionIds = new int[count];
            var names = new string[count];
            var readies = new bool[count];

            for (int i = 0; i < count; i++)
            {
                int connectionId = _players[i].Connection.connectionId;
                connectionIds[i] = connectionId;
                names[i] = _players[i].Name;
                readies[i] = _readyStates.TryGetValue(connectionId, out bool ready) && ready;
            }

            var message = new RoomStateMessage
            {
                localConnectionId = conn.connectionId,
                hostConnectionId = _hostConnectionId,
                gameMode = _selectedGameMode,
                connectionIds = connectionIds,
                playerNames = names,
                readyStates = readies
            };

            conn.Send(message);

            // Host 本地 UI 有时不会立即收到本地连接回包，这里主动应用一次。
            if (NetworkServer.active && NetworkClient.active && conn.connectionId == _hostConnectionId)
            {
                ApplyRoomState(message);
            }
        }

        private void OnRoomStateMessage(RoomStateMessage msg)
        {
            ApplyRoomState(msg);
        }

        private void ApplyRoomState(RoomStateMessage msg)
        {
            int count = Mathf.Min(msg.connectionIds?.Length ?? 0, Mathf.Min(msg.playerNames?.Length ?? 0, msg.readyStates?.Length ?? 0));
            var players = new RoomPlayerState[count];
            for (int i = 0; i < count; i++)
            {
                int connectionId = msg.connectionIds[i];
                players[i] = new RoomPlayerState
                {
                    ConnectionId = connectionId,
                    Name = msg.playerNames[i],
                    IsReady = msg.readyStates[i],
                    IsHost = connectionId == msg.hostConnectionId
                };
            }

            CurrentRoomState = new RoomStateSnapshot
            {
                LocalConnectionId = msg.localConnectionId,
                HostConnectionId = msg.hostConnectionId,
                GameMode = NormalizeGameMode(msg.gameMode),
                Players = players
            };
            OnRoomStateChanged?.Invoke(CurrentRoomState);
        }

        private static string NormalizeGameMode(string gameMode)
        {
            return string.Equals(gameMode, "Sichuan", StringComparison.OrdinalIgnoreCase) ? "Sichuan" : "Riichi";
        }
    }
}
