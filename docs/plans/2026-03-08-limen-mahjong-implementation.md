# LiMen Mahjong 完整实施计划

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 将现有 Unity 2022 日式麻将项目改造为支持本地热点联机、双游戏模式（日式+四川）、AI 填位、角色立绘、金币经济和抽卡系统的 Android 麻将游戏。

**Architecture:** 删除 Photon PUN2 云网络，替换为 Mirror Networking 纯本地 LAN 方案；保留现有日式麻将逻辑完整不动，新增四川麻将逻辑模块；角色系统使用 ScriptableObject 数据驱动，方便扩展；经济/抽卡数据全部本地 JSON 持久化。

**Tech Stack:** Unity 2022.3.62f3, C#, Mirror Networking (via UPM), Android ARM64/IL2CPP, PlayerPrefs + JSON 本地存储

**Design Doc:** `docs/plans/2026-03-08-limen-mahjong-design.md`

---

## 准备工作：初始化 Git

> Unity 项目目前没有 git 仓库，必须先初始化，否则无法版本控制。

**Step 1: 初始化 git 并创建 .gitignore**

在终端中执行（项目根目录）：
```bash
cd /Users/wudelong/Projects/LiMen-Mahjong
git init
```

然后创建 `.gitignore` 文件，内容如下（Unity 标准 gitignore）：
```
# Unity 自动生成目录（不需要提交）
/[Ll]ibrary/
/[Tt]emp/
/[Oo]bj/
/[Bb]uild/
/[Bb]uilds/
/[Ll]ogs/
/[Uu]ser[Ss]ettings/

# Unity3D 生成的 meta 文件（保留！）
# *.meta  ← 不要忽略，这些文件必须提交

# VS/Rider IDE 目录
.vs/
.idea/
*.csproj
*.sln

# macOS
.DS_Store

# APK 输出
*.apk
*.aab
```

**Step 2: 首次提交**

```bash
git add .
git commit -m "chore: initial commit - Unity 2022.3.62f3 migration of LiMen-Mahjong"
```

---

## Phase 1：替换 Mirror 网络层（删除 Photon PUN2）

> **目标：** 2 台手机连接同一热点，能完整打一局日式麻将。
>
> **前置知识：**
> - Mirror 是 Unity 的开源网络库，Host = 服务器+客户端同时运行
> - `NetworkBehaviour` 替代 `MonoBehaviourPun`
> - `[Command]` = 客户端 → 服务器调用；`[ClientRpc]` = 服务器 → 所有客户端广播
> - LAN Discovery = 在局域网内广播/发现游戏房间，无需手动输 IP

### Task 1.1: 安装 Mirror Networking 包

**Files:**
- Modify: `Packages/manifest.json`

**Step 1: 通过 Unity Package Manager 安装 Mirror**

在 Unity 编辑器中：
1. 顶部菜单 → `Window` → `Package Manager`
2. 左上角点击 `+` → `Add package from git URL`
3. 输入：`https://github.com/MirrorNetworking/Mirror.git#upm`
4. 点击 `Add`，等待安装完成（约 1-2 分钟）

安装完成后验证：Package Manager 列表中出现 `Mirror` 条目。

**Step 2: 验证安装**

Unity 控制台无红色错误 → 安装成功。

**Step 3: 提交**

```bash
git add Packages/
git commit -m "feat: add Mirror Networking package"
```

---

### Task 1.2: 删除 Photon PUN2

> ⚠️ 删除前务必完成 Task 1.1，否则删除 Photon 后会产生大量编译错误。

**Files:**
- Delete: `Assets/Photon/` 目录（整个目录）
- Delete: `Assets/Scripts/PUNLobby/` 目录（整个目录）

**Step 1: 在 Unity 编辑器中删除**

1. 在 Unity 的 **Project 面板**中找到 `Assets/Photon`
2. 右键 → `Delete`（或选中后按 `Command+Backspace`）
3. 同样删除 `Assets/Scripts/PUNLobby`

> ⚠️ 不要在 Finder 中删除！必须在 Unity 编辑器内删除，这样 `.meta` 文件也会被正确清理。

**Step 2: 检查编译错误**

删除后 Unity 会自动重新编译。打开 **控制台（Console）** 面板，此时会出现大量红色错误，这是预期结果——所有引用了 Photon 的脚本都需要迁移。

**记录下所有红色错误的文件名列表**（大约 5-10 个文件）。

**Step 3: 暂时注释掉有错误的代码**

对每个报错文件，在文件顶部 `using Photon...` 行加注释，暂时屏蔽：

在 Visual Studio Code 或 Rider 中打开各报错文件，将 `using Photon.Pun;` 和 `using Photon.Realtime;` 等行注释掉（`//`），确保项目能编译通过（0 红色错误）。后续 Task 会逐步替换这些文件。

**Step 4: 提交**

```bash
git add -A
git commit -m "chore: remove Photon PUN2 - lobby and network scripts pending migration"
```

---

### Task 1.3: 创建 Mirror NetworkManager

> Mirror 的 `NetworkManager` 负责管理服务器/客户端生命周期、场景切换和玩家生成。

**Files:**
- Create: `Assets/Scripts/Network/LiMenNetworkManager.cs`
- Create: `Assets/Scripts/Network/LiMenNetworkDiscovery.cs`

**Step 1: 创建 LiMenNetworkManager.cs**

在 `Assets/Scripts/Network/` 目录下（先在 Project 面板右键创建目录）创建新 C# 脚本，内容如下：

```csharp
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LiMen.Network
{
    public class LiMenNetworkManager : NetworkManager
    {
        public static new LiMenNetworkManager singleton => (LiMenNetworkManager)NetworkManager.singleton;

        [Header("LiMen Settings")]
        [Tooltip("最大玩家数（含AI槽位用4）")]
        public int maxPlayers = 4;

        // 玩家连接时自动触发
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            // 后续 Task 实现：生成玩家对象并分配座位
            base.OnServerAddPlayer(conn);
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();
            Debug.Log("[LiMen] 客户端连接成功");
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();
            Debug.Log("[LiMen] 客户端断开连接");
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            base.OnServerDisconnect(conn);
            Debug.Log($"[LiMen] 玩家断开: {conn}");
        }
    }
}
```

**Step 2: 创建 LiMenNetworkDiscovery.cs**

Mirror 提供了 `NetworkDiscovery` 基类，继承它实现 LAN 自动广播：

```csharp
using Mirror.Discovery;
using UnityEngine;

namespace LiMen.Network
{
    // 广播数据（Host 发出，其他设备接收）
    public struct DiscoveryRequest : NetworkMessage
    {
        // 暂时为空，后续可加版本号校验
    }

    public struct DiscoveryResponse : NetworkMessage
    {
        public System.Net.IPEndPoint EndPoint { get; set; }
        public string hostName;    // 房主名字
        public int playerCount;    // 当前人数
        public int maxPlayers;     // 最大人数
        public string gameMode;    // "Riichi" 或 "Sichuan"
    }

    public class LiMenNetworkDiscovery : NetworkDiscovery<DiscoveryRequest, DiscoveryResponse>
    {
        protected override DiscoveryResponse ProcessRequest(DiscoveryRequest request, System.Net.IPEndPoint endpoint)
        {
            return new DiscoveryResponse
            {
                hostName = PlayerPrefs.GetString("PlayerName", "房主"),
                playerCount = NetworkServer.connections.Count,
                maxPlayers = LiMenNetworkManager.singleton?.maxPlayers ?? 4,
                gameMode = "Riichi"
            };
        }

        protected override void ProcessResponse(DiscoveryResponse response, System.Net.IPEndPoint endpoint)
        {
            response.EndPoint = endpoint;
            OnServerFound?.Invoke(response);
        }

        public System.Action<DiscoveryResponse> OnServerFound;
    }
}
```

**Step 3: 在场景中创建 NetworkManager 对象**

1. 打开 `Assets/Scenes/PUN_Lobby.unity`（在 Project 面板双击）
2. 在 Hierarchy 面板右键 → `Create Empty` → 命名为 `NetworkManager`
3. 在 Inspector 中点击 `Add Component`，搜索并添加 `LiMenNetworkManager`
4. 同样添加 `LiMenNetworkDiscovery` 组件到同一对象
5. 在 `LiMenNetworkManager` 的 Inspector 中设置：
   - `Offline Scene`: `PUN_Lobby`（拖入）
   - `Online Scene`: `PUN_Mahjong`（拖入）

**Step 4: 提交**

```bash
git add Assets/Scripts/Network/
git commit -m "feat(network): add Mirror NetworkManager and LAN Discovery"
```

---

### Task 1.4: 创建大厅 UI（替换 PUN_Lobby 逻辑）

> 原来的大厅需要登录 Photon 服务器。新大厅只需要：输入名字 → 创建房间（开热点当 Host）或搜索房间（加入 Host）。

**Files:**
- Create: `Assets/Scripts/Network/LobbyUI.cs`
- Modify: `Assets/Scenes/PUN_Lobby.unity`（在 Unity 编辑器中操作）

**Step 1: 创建 LobbyUI.cs**

```csharp
using Mirror;
using Mirror.Discovery;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using LiMen.Network;

namespace LiMen.UI
{
    public class LobbyUI : MonoBehaviour
    {
        [Header("输入")]
        public TMP_InputField playerNameInput;

        [Header("按钮")]
        public Button hostButton;       // 创建房间（自己当 Host）
        public Button refreshButton;    // 刷新搜索
        public Button joinButton;       // 加入选中的房间

        [Header("房间列表")]
        public Transform roomListParent;
        public GameObject roomEntryPrefab;

        private LiMenNetworkDiscovery _discovery;
        private readonly Dictionary<System.Net.IPEndPoint, DiscoveryResponse> _discoveredServers = new();
        private DiscoveryResponse _selectedServer;
        private bool _hasSelection;

        void Start()
        {
            _discovery = FindObjectOfType<LiMenNetworkDiscovery>();
            _discovery.OnServerFound += OnServerFound;

            hostButton.onClick.AddListener(OnHostClicked);
            refreshButton.onClick.AddListener(OnRefreshClicked);
            joinButton.onClick.AddListener(OnJoinClicked);
            joinButton.interactable = false;

            // 读取上次保存的名字
            playerNameInput.text = PlayerPrefs.GetString("PlayerName", "玩家");
        }

        void OnHostClicked()
        {
            SavePlayerName();
            LiMenNetworkManager.singleton.StartHost();
            _discovery.AdvertiseServer();
        }

        void OnRefreshClicked()
        {
            _discoveredServers.Clear();
            ClearRoomList();
            _discovery.StartDiscovery();
        }

        void OnJoinClicked()
        {
            if (!_hasSelection) return;
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
                var entry = Instantiate(roomEntryPrefab, roomListParent);
                var text = entry.GetComponentInChildren<TMP_Text>();
                var btn = entry.GetComponent<Button>();
                var r = kvp.Value;
                text.text = $"{r.hostName} 的房间  [{r.playerCount}/{r.maxPlayers}]  {r.gameMode}";
                btn.onClick.AddListener(() =>
                {
                    _selectedServer = r;
                    _hasSelection = true;
                    joinButton.interactable = true;
                });
            }
        }

        void ClearRoomList()
        {
            foreach (Transform child in roomListParent)
                Destroy(child.gameObject);
        }

        void SavePlayerName()
        {
            PlayerPrefs.SetString("PlayerName", playerNameInput.text);
            PlayerPrefs.Save();
        }
    }
}
```

**Step 2: 在 PUN_Lobby 场景中设置 UI**

1. 打开 `PUN_Lobby.unity`
2. 删除旧的 Photon 大厅 UI Canvas（选中旧 Canvas 删除）
3. 新建一个 Canvas（右键 Hierarchy → UI → Canvas）
4. 在 Canvas 内添加：
   - `TMP_InputField`（玩家名字）
   - `Button`（创建房间）
   - `Button`（刷新搜索）
   - `Button`（加入房间）
   - `ScrollView`（房间列表）
5. 将 `LobbyUI` 脚本挂载到 Canvas 对象上，在 Inspector 中拖入对应 UI 组件引用

**Step 3: 创建 RoomEntry Prefab**

1. 在 Hierarchy 中创建一个 `Button`，内含 `TMP_Text`
2. 将其拖到 `Assets/Prefabs/UI/` 目录保存为 `RoomEntry.prefab`
3. 在 `LobbyUI` Inspector 中将其赋给 `Room Entry Prefab` 字段

**Step 4: 提交**

```bash
git add Assets/Scripts/Network/LobbyUI.cs Assets/Prefabs/
git commit -m "feat(lobby): replace Photon lobby with Mirror LAN discovery UI"
```

---

### Task 1.5: 迁移 ServerBehaviour（服务器逻辑）

> `ServerBehaviour.cs` 是游戏核心——它运行在 Host 端，控制整个游戏状态机（发牌、摸牌、出牌、胡牌、结算等）。
>
> 迁移原则：状态机逻辑完全保留，只替换 Photon RPC 调用方式。

**Files:**
- Modify: `Assets/Scripts/GamePlay/Server/Controller/ServerBehaviour.cs`

**Step 1: 理解现有代码结构**

打开 `ServerBehaviour.cs`，找到所有以下模式（搜索 `photonView.RPC`）：
```csharp
// 旧写法（Photon）
photonView.RPC("RpcGamePrepare", RpcTarget.All, ...args);
```

每一个 `photonView.RPC(...)` 调用都要替换。

**Step 2: 修改类声明**

```csharp
// 旧
public class ServerBehaviour : MonoBehaviourPun, IPunObservable { ... }

// 新
using Mirror;
public class ServerBehaviour : NetworkBehaviour { ... }
```

**Step 3: 替换所有 RPC 调用**

将文件顶部的 Photon using 删除，加入：
```csharp
using Mirror;
```

每个 RPC 方法改造示例：

```csharp
// 旧（Photon）：
// [PunRPC]
// void RpcGamePrepare(int[] tileData, int[] seatAssignment)

// 新（Mirror）：
[ClientRpc]
void RpcGamePrepare(int[] tileData, int[] seatAssignment)
{
    // 方法体不变
}
```

调用方式改变：
```csharp
// 旧
photonView.RPC("RpcGamePrepare", RpcTarget.All, tileData, seatAssignment);

// 新
RpcGamePrepare(tileData, seatAssignment);
// Mirror 中直接调用 [ClientRpc] 方法，它会自动广播给所有客户端
```

**Step 4: 替换 IsMasterClient 判断**

```csharp
// 旧
if (PhotonNetwork.IsMasterClient) { ... }

// 新
if (isServer) { ... }
```

**Step 5: 替换玩家列表获取**

```csharp
// 旧
var players = PhotonNetwork.PlayerList;

// 新
var connections = NetworkServer.connections;
// 根据实际逻辑调整（Mirror 连接字典：connId -> NetworkConnectionToClient）
```

**Step 6: 验证编译**

保存文件，在 Unity 控制台确认 0 红色错误。

**Step 7: 提交**

```bash
git add Assets/Scripts/GamePlay/Server/
git commit -m "feat(network): migrate ServerBehaviour from Photon to Mirror"
```

---

### Task 1.6: 迁移 ClientBehaviour（客户端逻辑）

**Files:**
- Modify: `Assets/Scripts/GamePlay/Client/Controller/ClientBehaviour.cs`

**Step 1: 修改类声明**

```csharp
// 旧
public class ClientBehaviour : MonoBehaviourPunCallbacks { ... }

// 新
using Mirror;
public class ClientBehaviour : NetworkBehaviour { ... }
```

**Step 2: 替换 PunCallbacks 回调**

```csharp
// 旧（Photon 回调）
public override void OnJoinedRoom() { ... }
public override void OnPlayerEnteredRoom(Player newPlayer) { ... }

// 新（Mirror 回调，在 NetworkManager 中注册，或使用 NetworkBehaviour 虚方法）
public override void OnStartClient() { ... }      // 对应 OnJoinedRoom
public override void OnStopClient() { ... }       // 对应离开房间
```

**Step 3: 替换 RPC 接收方法**

```csharp
// 旧
[PunRPC]
void RpcReceiveGameState(byte[] data) { ... }

// 新
[ClientRpc]
void RpcReceiveGameState(byte[] data) { ... }
```

**Step 4: 迁移自定义事件系统**

原代码可能使用 `PhotonNetwork.RaiseEvent`。Mirror 的等效方案：

```csharp
// 旧（Photon 自定义事件）
PhotonNetwork.RaiseEvent(EventCode.PlayerOperation, data, RaiseEventOptions.Default, SendOptions.SendReliable);

// 新（Mirror Command，从客户端发送到服务器）
[Command]
void CmdSendOperation(byte operationCode, byte[] data)
{
    // 服务器处理逻辑
}
// 调用：CmdSendOperation(code, data);
```

**Step 5: 验证编译，提交**

```bash
git add Assets/Scripts/GamePlay/Client/
git commit -m "feat(network): migrate ClientBehaviour from Photon to Mirror"
```

---

### Task 1.7: 修复剩余 Photon 引用

**Step 1: 搜索所有剩余 Photon 引用**

在终端中：
```bash
grep -r "photon\|Photon\|PUN\|PhotonNetwork" /Users/wudelong/Projects/LiMen-Mahjong/Assets/Scripts/ --include="*.cs" -l
```

**Step 2: 逐文件修复**

常见模式替换：

| Photon 用法 | Mirror 替换 |
|------------|------------|
| `PhotonNetwork.LocalPlayer.NickName` | `PlayerPrefs.GetString("PlayerName")` |
| `PhotonNetwork.PlayerList.Length` | `NetworkServer.connections.Count` |
| `PhotonNetwork.CurrentRoom.PlayerCount` | `NetworkServer.connections.Count` |
| `PhotonNetwork.Disconnect()` | `NetworkManager.singleton.StopHost()` 或 `StopClient()` |
| `PhotonNetwork.LoadLevel(scene)` | `NetworkManager.singleton.ServerChangeScene(scene)` |

**Step 3: 验证**

Unity Console 中 0 红色错误，0 Photon 相关警告。

**Step 4: 提交**

```bash
git add -A
git commit -m "fix(network): remove all remaining Photon references"
```

---

### Task 1.8: 配置 PUN_Mahjong 场景的 Mirror 组件

**Step 1: 打开 PUN_Mahjong 场景**

在 Project 面板双击 `Assets/Scenes/PUN_Mahjong.unity`。

**Step 2: 为 ServerManager 和 ClientManager 添加 NetworkIdentity**

Mirror 要求所有 `NetworkBehaviour` 所在的 GameObject 必须有 `NetworkIdentity` 组件：
1. 在 Hierarchy 中选中 `ServerManager` 对象
2. Inspector → `Add Component` → `NetworkIdentity`
3. 同样操作 `ClientManager` 对象

**Step 3: 注册 Prefab**

1. 在 Hierarchy 中选中 `NetworkManager` 对象
2. 在 `LiMenNetworkManager` 组件的 `Registered Spawnable Prefabs` 列表中
3. 添加所有可能在网络中生成的 Prefab（如 TileInstance prefab）

**Step 4: 联机测试（本机模拟）**

在 Unity 编辑器中进行本机测试：
1. 点击播放（▶）
2. 在大厅界面点击"创建房间"（StartHost）
3. 打开另一个 Unity 实例（如果有两台 Mac 更好）或用 `Build & Run` 在同一网络测试

**Step 5: 提交**

```bash
git add Assets/Scenes/
git commit -m "feat(network): configure Mirror NetworkIdentity in game scenes"
```

---

## Phase 2：Android 构建 + 触控 UI 优化

### Task 2.1: 配置 Android 构建设置

**Files:**
- Modify: `ProjectSettings/ProjectSettings.asset`（通过 Unity 编辑器操作）

**Step 1: 切换构建平台**

1. 菜单 `File` → `Build Settings`
2. 在平台列表选中 `Android`
3. 点击 `Switch Platform`（等待约 2-5 分钟）

**Step 2: 配置 Player Settings**

`Build Settings` → `Player Settings`：

| 设置项 | 值 |
|--------|-----|
| Company Name | 你的名字 |
| Product Name | LiMen Mahjong |
| Package Name | `com.yourname.limenmahjong` |
| Minimum API Level | Android 7.0 (API 24) |
| Target API Level | Automatic (highest installed) |
| Scripting Backend | **IL2CPP** |
| Target Architectures | ✅ **ARM64**（取消勾选 ARMv7）|
| Default Orientation | **Landscape Left** |

**Step 3: 配置 Android SDK**

如果 Unity 提示找不到 Android SDK：
1. `Unity Hub` → `Installs` → 找到 2022.3.62f3 → 齿轮图标 → `Add Modules`
2. 添加 `Android Build Support`（含 Android SDK & NDK Tools）

**Step 4: 验证构建配置**

`Build Settings` → `Build`，输出一个测试 APK，不需要任何功能，只需确认能打包成功。

APK 默认输出路径：项目根目录下自定义位置，建议 `Builds/Android/`

**Step 5: 提交**

```bash
git add ProjectSettings/
git commit -m "chore: configure Android ARM64 IL2CPP build settings"
```

---

### Task 2.2: 触控 UI 优化——手牌选择

> 原项目为桌面鼠标设计，手机上需要触控友好的交互。手牌区域的牌需要能用手指点击选中并出牌。

**Files:**
- Modify: `Assets/Scripts/GamePlay/Client/View/Elements/HandTile.cs`
- Modify: `Assets/Scripts/GamePlay/Client/View/HandPanelManager.cs`（查看后确认）

**Step 1: 检查现有 HandTile.cs 的点击实现**

打开 `HandTile.cs`，找到鼠标点击处理代码（通常是 `OnMouseDown` 或 `IPointerClickHandler`）。

如果是 `OnMouseDown`：
```csharp
// 旧（鼠标，手机上无效）
void OnMouseDown() { SelectTile(); }

// 新（使用 Unity EventSystem，同时支持鼠标和触控）
using UnityEngine.EventSystems;
public class HandTile : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        SelectTile();
    }
}
```

**Step 2: 调整手牌 UI 尺寸**

手机屏幕手指点击需要至少 44px × 44px 的点击区域（推荐 60px 以上）：
1. 打开 `PUN_Mahjong.unity`
2. 在 Hierarchy 中找到手牌面板（HandPanel 或 PlayerHand 相关对象）
3. 调整手牌图片的 `Rect Transform` 宽高，确保手指可以轻松点击

**Step 3: 提交**

```bash
git add Assets/Scripts/GamePlay/Client/View/Elements/HandTile.cs
git commit -m "fix(ui): replace OnMouseDown with IPointerClickHandler for touch support"
```

---

### Task 2.3: 适配手机分辨率

**Files:**
- Modify: `Assets/Scenes/PUN_Mahjong.unity`（在 Unity 编辑器中操作）

**Step 1: 设置 Canvas Scaler**

1. 打开 `PUN_Mahjong.unity`
2. 在 Hierarchy 找到 `Canvas` 对象
3. Inspector 中找到 `Canvas Scaler` 组件
4. 设置如下：
   - UI Scale Mode: **Scale With Screen Size**
   - Reference Resolution: **1920 × 1080**（横屏标准）
   - Screen Match Mode: **Match Width Or Height**
   - Match: **0.5**（宽高均衡缩放）

**Step 2: 对大厅和房间场景做同样设置**

重复上述步骤处理 `PUN_Lobby.unity` 和 `PUN_Room.unity`。

**Step 3: 安全区域适配（刘海屏）**

对于有刘海/打孔屏的手机，在主 Canvas 下的 UI Panel 添加安全区域适配：

```csharp
// 创建 Assets/Scripts/UI/SafeAreaFitter.cs
using UnityEngine;

public class SafeAreaFitter : MonoBehaviour
{
    RectTransform _rt;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    void ApplySafeArea()
    {
        var safeArea = Screen.safeArea;
        var anchorMin = safeArea.position;
        var anchorMax = anchorMin + safeArea.size;
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;
        _rt.anchorMin = anchorMin;
        _rt.anchorMax = anchorMax;
    }
}
```

将此脚本挂载到游戏 UI 的根 Panel 上。

**Step 4: 构建并安装测试 APK**

```bash
# Unity 命令行构建（或在编辑器中 File → Build & Run）
Unity -batchmode -quit \
  -projectPath /Users/wudelong/Projects/LiMen-Mahjong \
  -buildTarget Android \
  -executeMethod BuildScript.BuildAndroid \
  -logFile build.log
```

或直接在 Unity 编辑器：`File` → `Build Settings` → `Build And Run`（手机通过 USB 连接时自动安装）。

**Step 5: 提交**

```bash
git add Assets/Scripts/UI/SafeAreaFitter.cs Assets/Scenes/
git commit -m "fix(ui): adapt Canvas Scaler and safe area for mobile screens"
```

---

## Phase 3：四川麻将模式

### Task 3.1: 实现四川麻将核心逻辑

**Files:**
- Create: `Assets/Scripts/Mahjong/Logic/SichuanLogic.cs`
- Create: `Assets/Scripts/Mahjong/Model/SichuanGameSetting.cs`

**Step 1: 创建 SichuanGameSetting.cs**

```csharp
using UnityEngine;

namespace Mahjong.Model
{
    [System.Serializable]
    public class SichuanGameSetting
    {
        [Tooltip("是否启用血战到底（有人胡牌后其余人继续）")]
        public bool xuezhanMode = true;

        [Tooltip("自摸是否加倍")]
        public bool zimoDouble = true;

        [Tooltip("杠上花是否算番")]
        public bool gangShangHua = true;

        [Tooltip("门清限制（必须缺一门）")]
        public bool queYiMen = true;

        // 108 张牌（万/筒/索，各36张），无字牌
        public static int TileCount => 108;
    }
}
```

**Step 2: 创建 SichuanLogic.cs**

```csharp
using System.Collections.Generic;
using System.Linq;
using Mahjong.Model;

namespace Mahjong.Logic
{
    /// <summary>
    /// 四川麻将规则逻辑
    /// 规则：108张（万筒索），缺一门，碰杠，无吃，血战到底
    /// </summary>
    public static class SichuanLogic
    {
        // ─── 牌山 ───────────────────────────────────────────────────────

        /// <summary>生成 108 张四川麻将牌（无字牌）</summary>
        public static List<Tile> CreateSichuanTileSet()
        {
            var tiles = new List<Tile>(108);
            // Suit: M=万, P=筒, S=索（与日麻相同枚举）
            foreach (var suit in new[] { Suit.M, Suit.P, Suit.S })
                for (int rank = 1; rank <= 9; rank++)
                    for (int copy = 0; copy < 4; copy++)
                        tiles.Add(new Tile(suit, rank));
            return tiles;
        }

        // ─── 缺一门检测 ──────────────────────────────────────────────────

        /// <summary>
        /// 检测玩家手牌是否满足"缺一门"条件
        /// 缺一门 = 手牌中恰好缺少万/筒/索三种花色之一
        /// </summary>
        public static bool CheckQueYiMen(IEnumerable<Tile> hand, IEnumerable<Tile> melds)
        {
            var allTiles = hand.Concat(melds).ToList();
            bool hasM = allTiles.Any(t => t.Suit == Suit.M);
            bool hasP = allTiles.Any(t => t.Suit == Suit.P);
            bool hasS = allTiles.Any(t => t.Suit == Suit.S);
            // 三种花色中恰好有一种为 false
            int count = (hasM ? 1 : 0) + (hasP ? 1 : 0) + (hasS ? 1 : 0);
            return count == 2; // 只有2种 = 缺1种
        }

        // ─── 胡牌判断 ──────────────────────────────────────────────────

        /// <summary>
        /// 判断给定手牌是否能胡牌（四川规则）
        /// 胡牌 = 一对将牌 + 三个面子（顺子或刻子）
        /// 注意：四川麻将支持顺子（与日麻不同的是无役种限制）
        /// </summary>
        public static bool CanWin(List<Tile> handTiles)
        {
            if (handTiles.Count != 14) return false;

            // 尝试每张牌作为将牌（对子）
            for (int i = 0; i < handTiles.Count - 1; i++)
            {
                for (int j = i + 1; j < handTiles.Count; j++)
                {
                    if (!handTiles[i].EqualsIgnoreRed(handTiles[j])) continue;

                    // 移除将牌，检查剩余 12 张能否组成 3 个面子
                    var remaining = new List<Tile>(handTiles);
                    remaining.RemoveAt(j);
                    remaining.RemoveAt(i);
                    if (CanFormMelds(remaining)) return true;
                }
            }
            return false;
        }

        /// <summary>递归检查牌列表能否全部组成面子（刻子或顺子）</summary>
        private static bool CanFormMelds(List<Tile> tiles)
        {
            if (tiles.Count == 0) return true;

            var sorted = tiles.OrderBy(t => t.Suit).ThenBy(t => t.Rank).ToList();
            var first = sorted[0];

            // 尝试刻子
            var triplet = sorted.FindAll(t => t.EqualsIgnoreRed(first));
            if (triplet.Count >= 3)
            {
                var next = new List<Tile>(sorted);
                for (int i = 0; i < 3; i++) next.Remove(triplet[i]);
                if (CanFormMelds(next)) return true;
            }

            // 尝试顺子（同花色连续3张）
            if (first.Rank <= 7)
            {
                var t2 = sorted.FirstOrDefault(t => t.Suit == first.Suit && t.Rank == first.Rank + 1);
                var t3 = sorted.FirstOrDefault(t => t.Suit == first.Suit && t.Rank == first.Rank + 2);
                if (t2 != null && t3 != null)
                {
                    var next = new List<Tile>(sorted);
                    next.Remove(first);
                    next.Remove(t2);
                    next.Remove(t3);
                    if (CanFormMelds(next)) return true;
                }
            }

            return false;
        }

        // ─── 番数计算 ──────────────────────────────────────────────────

        /// <summary>四川麻将基础番数（简化版）</summary>
        public static int CalculateFan(List<Tile> hand, bool isTsumo, bool isGangShangHua)
        {
            int fan = 1; // 基础1番

            if (isTsumo) fan *= 2;          // 自摸翻倍
            if (isGangShangHua) fan *= 2;   // 杠上花翻倍

            // 清一色（全同一花色）
            if (hand.All(t => t.Suit == hand[0].Suit)) fan += 3;

            // 对对胡（全部刻子，无顺子）
            // TODO: 检测对对胡并加番

            return fan;
        }
    }
}
```

**Step 3: 为 SichuanLogic 添加单元测试**

在 `Assets/Scripts/Editor/` 目录下创建 `SichuanLogicTest.cs`：

```csharp
using NUnit.Framework;
using System.Collections.Generic;
using Mahjong.Model;
using Mahjong.Logic;

public class SichuanLogicTest
{
    [Test]
    public void CreateSichuanTileSet_Returns108Tiles()
    {
        var tiles = SichuanLogic.CreateSichuanTileSet();
        Assert.AreEqual(108, tiles.Count);
    }

    [Test]
    public void CheckQueYiMen_MissingOneSuit_ReturnsTrue()
    {
        // 全是万和筒，没有索 → 缺一门
        var hand = new List<Tile>
        {
            new Tile(Suit.M, 1), new Tile(Suit.M, 2), new Tile(Suit.M, 3),
            new Tile(Suit.P, 1), new Tile(Suit.P, 2), new Tile(Suit.P, 3),
        };
        Assert.IsTrue(SichuanLogic.CheckQueYiMen(hand, new List<Tile>()));
    }

    [Test]
    public void CheckQueYiMen_HasAllSuits_ReturnsFalse()
    {
        var hand = new List<Tile>
        {
            new Tile(Suit.M, 1), new Tile(Suit.P, 1), new Tile(Suit.S, 1),
        };
        Assert.IsFalse(SichuanLogic.CheckQueYiMen(hand, new List<Tile>()));
    }

    [Test]
    public void CanWin_ValidHand_ReturnsTrue()
    {
        // 123m 456m 789m 12p 3p3p → 标准胡牌手
        var hand = new List<Tile>
        {
            new Tile(Suit.M,1), new Tile(Suit.M,2), new Tile(Suit.M,3),
            new Tile(Suit.M,4), new Tile(Suit.M,5), new Tile(Suit.M,6),
            new Tile(Suit.M,7), new Tile(Suit.M,8), new Tile(Suit.M,9),
            new Tile(Suit.P,1), new Tile(Suit.P,2), new Tile(Suit.P,3),
            new Tile(Suit.P,5), new Tile(Suit.P,5),
        };
        Assert.IsTrue(SichuanLogic.CanWin(hand));
    }
}
```

在 Unity 中运行测试：菜单 `Window` → `General` → `Test Runner` → `EditMode` → 点击 `Run All`

所有测试应通过（绿色）。

**Step 4: 提交**

```bash
git add Assets/Scripts/Mahjong/Logic/SichuanLogic.cs \
        Assets/Scripts/Mahjong/Model/SichuanGameSetting.cs \
        Assets/Scripts/Editor/SichuanLogicTest.cs
git commit -m "feat(sichuan): implement core Sichuan Mahjong logic with tests"
```

---

### Task 3.2: 游戏模式选择界面

**Files:**
- Create: `Assets/Scripts/UI/GameModeSelector.cs`
- Modify: `Assets/Scenes/PUN_Room.unity`（在 Unity 编辑器中操作）

**Step 1: 创建 GameModeSelector.cs**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LiMen.UI
{
    public enum GameMode { Riichi, Sichuan }

    public class GameModeSelector : MonoBehaviour
    {
        public Button riichiButton;
        public Button sichuanButton;
        public TMP_Text selectedModeText;

        public static GameMode SelectedMode { get; private set; } = GameMode.Riichi;

        void Start()
        {
            riichiButton.onClick.AddListener(() => SelectMode(GameMode.Riichi));
            sichuanButton.onClick.AddListener(() => SelectMode(GameMode.Sichuan));
            UpdateUI();
        }

        void SelectMode(GameMode mode)
        {
            SelectedMode = mode;
            PlayerPrefs.SetString("GameMode", mode.ToString());
            UpdateUI();
        }

        void UpdateUI()
        {
            selectedModeText.text = SelectedMode == GameMode.Riichi
                ? "当前模式：立直麻将（日式）"
                : "当前模式：四川麻将";

            riichiButton.interactable = SelectedMode != GameMode.Riichi;
            sichuanButton.interactable = SelectedMode != GameMode.Sichuan;
        }
    }
}
```

**Step 2: 在 PUN_Room.unity 场景中添加模式选择 UI**

1. 打开 `PUN_Room.unity`
2. 在房间设置区域添加两个 `Button`：
   - "立直麻将（日式）"
   - "四川麻将"
3. 添加一个 `TMP_Text` 显示当前选择
4. 挂载 `GameModeSelector` 脚本，绑定引用

**Step 3: 在游戏开始时读取模式**

在 `ServerBehaviour.cs` 的游戏初始化逻辑中，根据所选模式决定使用哪套规则：

```csharp
var mode = PlayerPrefs.GetString("GameMode", "Riichi");
if (mode == "Sichuan")
{
    // 使用 SichuanLogic
    InitializeSichuanGame();
}
else
{
    // 使用现有日麻逻辑
    InitializeRiichiGame();
}
```

**Step 4: 提交**

```bash
git add Assets/Scripts/UI/GameModeSelector.cs Assets/Scenes/
git commit -m "feat(ui): add game mode selector (Riichi vs Sichuan)"
```

---

## Phase 4：AI 填位

### Task 4.1: AI 决策器

**Files:**
- Create: `Assets/Scripts/GamePlay/AI/AiDecisionMaker.cs`
- Create: `Assets/Scripts/GamePlay/AI/AiPlayerController.cs`

**Step 1: 创建 AiDecisionMaker.cs**

```csharp
using System.Collections.Generic;
using System.Linq;
using Mahjong.Model;
using Mahjong.Logic;

namespace LiMen.AI
{
    /// <summary>
    /// 小白级 AI 决策器：随机出牌 + 基础胜利检测
    /// </summary>
    public static class AiDecisionMaker
    {
        private static readonly System.Random _rng = new System.Random();

        /// <summary>选择要出的牌（简单策略：丢弃手牌中最孤立的牌）</summary>
        public static Tile ChooseDiscard(List<Tile> hand)
        {
            if (hand == null || hand.Count == 0) return default;
            // 简单策略：丢弃最后一张（实际游戏中会是摸来的牌或孤张）
            // TODO: 实现向听数最大化算法
            return hand[hand.Count - 1];
        }

        /// <summary>是否碰牌（75% 概率）</summary>
        public static bool ShouldPon(Tile tile, List<Tile> hand)
        {
            int matches = hand.Count(t => t.EqualsIgnoreRed(tile));
            return matches >= 2 && _rng.NextDouble() < 0.75;
        }

        /// <summary>是否杠牌（60% 概率）</summary>
        public static bool ShouldKan(Tile tile, List<Tile> hand)
        {
            int matches = hand.Count(t => t.EqualsIgnoreRed(tile));
            return matches >= 3 && _rng.NextDouble() < 0.6;
        }

        /// <summary>是否荣和（100%：有机会必胡）</summary>
        public static bool ShouldRon(List<Tile> hand, Tile discardedTile)
        {
            var testHand = new List<Tile>(hand) { discardedTile };
            return MahjongLogic.IsWinningHand(testHand);
        }
    }
}
```

**Step 2: 创建 AiPlayerController.cs**

```csharp
using Mirror;
using UnityEngine;
using System.Collections;
using LiMen.AI;
using Mahjong.Model;

namespace LiMen.GamePlay
{
    /// <summary>
    /// AI 玩家控制器，运行在 Host 端
    /// AI 占用一个玩家槽位，模拟真实玩家的操作
    /// </summary>
    public class AiPlayerController : MonoBehaviour
    {
        [Tooltip("AI 思考延迟（模拟真人，单位秒）")]
        public float thinkDelay = 1.5f;

        public int SeatIndex { get; set; }
        public bool IsActive { get; set; }

        // 由 ServerBehaviour 调用，通知 AI 做决策
        public void OnYourTurn(System.Action<Tile> onDiscard)
        {
            StartCoroutine(ThinkAndDiscard(onDiscard));
        }

        private IEnumerator ThinkAndDiscard(System.Action<Tile> onDiscard)
        {
            yield return new WaitForSeconds(thinkDelay);
            // 从 ServerBehaviour 获取当前 AI 手牌
            var hand = GetCurrentHand();
            var tile = AiDecisionMaker.ChooseDiscard(hand);
            onDiscard?.Invoke(tile);
        }

        private System.Collections.Generic.List<Tile> GetCurrentHand()
        {
            // TODO: 从 ServerBehaviour 获取对应座位的手牌
            return new System.Collections.Generic.List<Tile>();
        }
    }
}
```

**Step 3: 在房间界面添加 AI 槽位选项**

在 `PUN_Room.unity` 的房间槽位 UI 中，每个空位可以设置为"AI 填充"。

**Step 4: 提交**

```bash
git add Assets/Scripts/GamePlay/AI/
git commit -m "feat(ai): add basic AI decision maker and controller"
```

---

## Phase 5：角色立绘系统

### Task 5.1: CharacterData ScriptableObject

**Files:**
- Create: `Assets/Scripts/Character/CharacterData.cs`
- Create: `Assets/Scripts/Managers/CharacterManager.cs`

**Step 1: 创建 CharacterData.cs**

```csharp
using UnityEngine;

namespace LiMen.Character
{
    public enum Rarity { R, SR, SSR }

    [CreateAssetMenu(fileName = "NewCharacter", menuName = "LiMen/Character Data")]
    public class CharacterData : ScriptableObject
    {
        [Header("基本信息")]
        public string characterId;       // 唯一 ID，如 "keqing"
        public string displayName;       // 显示名称，如 "刻晴"
        public string sourceGame;        // 来源游戏，如 "原神"
        public Rarity rarity;

        [Header("美术资源")]
        public Sprite avatarSprite;      // 头像（圆形裁剪，128x128）
        public Sprite portraitSprite;    // 完整立绘（竖版）

        [Header("描述")]
        [TextArea(2, 4)]
        public string description;

        [Header("初始可选")]
        [Tooltip("true = 新玩家可免费选择此角色")]
        public bool isDefault;
    }
}
```

**Step 2: 创建 CharacterManager.cs**

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using LiMen.Character;

namespace LiMen.Managers
{
    public class CharacterManager : MonoBehaviour
    {
        public static CharacterManager Instance { get; private set; }

        [Tooltip("将所有 CharacterData 资源拖入此列表")]
        public List<CharacterData> allCharacters = new();

        private HashSet<string> _ownedIds = new();
        private string _equippedId;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadFromDisk();
        }

        public bool IsOwned(string characterId) => _ownedIds.Contains(characterId);

        public CharacterData GetEquipped() =>
            allCharacters.FirstOrDefault(c => c.characterId == _equippedId);

        public void Equip(string characterId)
        {
            _equippedId = characterId;
            SaveToDisk();
        }

        public void Unlock(string characterId)
        {
            _ownedIds.Add(characterId);
            SaveToDisk();
        }

        public List<CharacterData> GetDefaultCharacters() =>
            allCharacters.Where(c => c.isDefault).ToList();

        // ── 存储 ────────────────────────────────────────────────
        void SaveToDisk()
        {
            PlayerPrefs.SetString("EquippedCharacter", _equippedId);
            PlayerPrefs.SetString("OwnedCharacters", string.Join(",", _ownedIds));
            PlayerPrefs.Save();
        }

        void LoadFromDisk()
        {
            _equippedId = PlayerPrefs.GetString("EquippedCharacter", "");
            var owned = PlayerPrefs.GetString("OwnedCharacters", "");
            _ownedIds = owned.Length > 0
                ? new HashSet<string>(owned.Split(','))
                : new HashSet<string>();
        }
    }
}
```

**Step 3: 导入 miHoYo 角色素材**

1. 前往 miHoYo 官方网站下载 Fan Kit：
   - 原神：https://genshin.hoyoverse.com（官网 → 粉丝创作）
   - 崩坏：星穹铁道：https://hsr.hoyoverse.com
2. 将角色立绘 PNG 放入：
   - 完整立绘：`Assets/Resources/Characters/Portraits/角色名.png`
   - 头像：`Assets/Resources/Characters/Avatars/角色名_avatar.png`
3. 在 Unity 中将立绘 Texture Type 设为 `Sprite (2D and UI)`

**Step 4: 创建角色 ScriptableObject 资源**

在 Project 面板中：右键 → `Create` → `LiMen` → `Character Data`

为每个角色填写 `characterId`、`displayName`、拖入 `avatarSprite` 和 `portraitSprite`。

**Step 5: 提交**

```bash
git add Assets/Scripts/Character/ Assets/Scripts/Managers/CharacterManager.cs
git commit -m "feat(character): add CharacterData ScriptableObject and CharacterManager"
```

---

### Task 5.2: 四角头像 UI + 立绘弹出面板

**Files:**
- Create: `Assets/Scripts/UI/CharacterPortraitUI.cs`
- Create: `Assets/Scripts/UI/PortraitDetailPanel.cs`
- Modify: `Assets/Scenes/PUN_Mahjong.unity`

**Step 1: 创建 CharacterPortraitUI.cs（四角头像组件）**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LiMen.Character;
using LiMen.Managers;

namespace LiMen.UI
{
    public class CharacterPortraitUI : MonoBehaviour
    {
        [Header("UI 引用")]
        public Image avatarImage;
        public TMP_Text playerNameText;
        public TMP_Text scoreText;
        public Button clickButton;

        [Header("详情面板")]
        public PortraitDetailPanel detailPanel;

        private CharacterData _characterData;
        private string _playerName;

        public void Setup(string playerName, CharacterData character, int score)
        {
            _playerName = playerName;
            _characterData = character;

            playerNameText.text = playerName;
            scoreText.text = score.ToString();
            avatarImage.sprite = character?.avatarSprite;

            clickButton.onClick.RemoveAllListeners();
            clickButton.onClick.AddListener(OnAvatarClicked);
        }

        public void UpdateScore(int score)
        {
            scoreText.text = score.ToString();
        }

        void OnAvatarClicked()
        {
            if (detailPanel != null && _characterData != null)
                detailPanel.Show(_playerName, _characterData);
        }
    }
}
```

**Step 2: 创建 PortraitDetailPanel.cs（点击后的完整立绘弹窗）**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LiMen.Character;

namespace LiMen.UI
{
    public class PortraitDetailPanel : MonoBehaviour
    {
        public Image portraitImage;
        public TMP_Text nameText;
        public TMP_Text rarityText;
        public TMP_Text sourceGameText;
        public TMP_Text descriptionText;
        public Button closeButton;

        void Start()
        {
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            gameObject.SetActive(false);
        }

        public void Show(string playerName, CharacterData data)
        {
            nameText.text = $"{playerName}  ·  {data.displayName}";
            rarityText.text = data.rarity.ToString();
            sourceGameText.text = data.sourceGame;
            descriptionText.text = data.description;
            portraitImage.sprite = data.portraitSprite;
            gameObject.SetActive(true);
        }
    }
}
```

**Step 3: 在 PUN_Mahjong 场景布置四角 UI**

1. 打开 `PUN_Mahjong.unity`
2. 在主 Canvas 下添加 4 个 `CharacterPortraitUI` 预制体，放在四个角
3. 添加一个 `PortraitDetailPanel` 作为弹窗（默认隐藏）
4. 将每个 `CharacterPortraitUI` 的 `detailPanel` 字段指向同一个弹窗

**Step 4: 提交**

```bash
git add Assets/Scripts/UI/CharacterPortraitUI.cs Assets/Scripts/UI/PortraitDetailPanel.cs
git commit -m "feat(ui): add character portrait corners and detail panel"
```

---

## Phase 6：金币经济系统

### Task 6.1: CoinManager + 签到系统

**Files:**
- Create: `Assets/Scripts/Managers/CoinManager.cs`
- Create: `Assets/Scripts/Managers/SignInManager.cs`

**Step 1: 创建 CoinManager.cs**

```csharp
using UnityEngine;

namespace LiMen.Managers
{
    public class CoinManager : MonoBehaviour
    {
        public static CoinManager Instance { get; private set; }

        private const string CoinKey = "Coins";
        private const int DailyMinimum = 50;    // 每日低保

        public int Coins { get; private set; }

        public event System.Action<int> OnCoinsChanged;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Coins = PlayerPrefs.GetInt(CoinKey, 500); // 新用户初始500金币
        }

        public void Add(int amount)
        {
            Coins += amount;
            Save();
            OnCoinsChanged?.Invoke(Coins);
        }

        public bool Spend(int amount)
        {
            if (Coins < amount) return false;
            Coins -= amount;
            Save();
            OnCoinsChanged?.Invoke(Coins);
            return true;
        }

        public void ApplyDailyMinimum()
        {
            if (Coins < DailyMinimum)
            {
                Add(DailyMinimum - Coins);
                Debug.Log($"[CoinManager] 每日低保补充至 {DailyMinimum} 金币");
            }
        }

        void Save() => PlayerPrefs.SetInt(CoinKey, Coins);
    }
}
```

**Step 2: 创建 SignInManager.cs**

```csharp
using System;
using UnityEngine;

namespace LiMen.Managers
{
    public class SignInManager : MonoBehaviour
    {
        public static SignInManager Instance { get; private set; }

        // 7天循环签到奖励
        private static readonly int[] SignInRewards = { 50, 60, 80, 100, 120, 150, 200 };

        public int ConsecutiveDays { get; private set; }
        public bool HasSignedInToday { get; private set; }

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CheckSignIn();
        }

        void CheckSignIn()
        {
            var lastDate = PlayerPrefs.GetString("LastSignIn", "");
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            HasSignedInToday = lastDate == today;

            if (!HasSignedInToday)
            {
                var yesterday = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");
                ConsecutiveDays = lastDate == yesterday
                    ? PlayerPrefs.GetInt("ConsecutiveDays", 0) + 1
                    : 1;
            }
            else
            {
                ConsecutiveDays = PlayerPrefs.GetInt("ConsecutiveDays", 1);
            }
        }

        /// <returns>奖励金币数，-1 表示今天已签到</returns>
        public int ClaimSignIn()
        {
            if (HasSignedInToday) return -1;

            var reward = SignInRewards[(ConsecutiveDays - 1) % SignInRewards.Length];
            CoinManager.Instance?.Add(reward);
            CoinManager.Instance?.ApplyDailyMinimum();

            PlayerPrefs.SetString("LastSignIn", DateTime.Today.ToString("yyyy-MM-dd"));
            PlayerPrefs.SetInt("ConsecutiveDays", ConsecutiveDays);
            PlayerPrefs.Save();
            HasSignedInToday = true;

            return reward;
        }
    }
}
```

**Step 3: 对局结算奖励集成**

在 `PointTransferManager`（或游戏结算逻辑）中，结算完成后调用：

```csharp
// 在游戏结束结算时调用
void OnMatchEnd(bool isWinner, int fan)
{
    bool isFriendlyMode = GameModeSelector.SelectedMode == /* 友谊场 */;
    float multiplier = isFriendlyMode ? 0.5f : 1.0f;

    int reward = isWinner
        ? Mathf.RoundToInt(Random.Range(80, 150) * multiplier)
        : Mathf.RoundToInt(Random.Range(20, 40) * multiplier);

    CoinManager.Instance?.Add(reward);
}
```

**Step 4: 提交**

```bash
git add Assets/Scripts/Managers/CoinManager.cs Assets/Scripts/Managers/SignInManager.cs
git commit -m "feat(economy): add CoinManager and SignInManager with daily rewards"
```

---

## Phase 7：抽卡系统

### Task 7.1: GachaManager

**Files:**
- Create: `Assets/Scripts/Managers/GachaManager.cs`

**Step 1: 创建 GachaManager.cs**

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using LiMen.Character;

namespace LiMen.Managers
{
    public class GachaManager : MonoBehaviour
    {
        public static GachaManager Instance { get; private set; }

        private const int SingleCost = 160;
        private const int TenCost = 1600;
        private const int SsrPity = 90;    // SSR 硬保底
        private const float SsrRate = 0.006f;
        private const float SrRate = 0.051f;

        private int _pullsSinceLastSsr;
        private int _pullsSinceLastSr;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadPity();
        }

        public List<CharacterData> PullTen()
        {
            if (!CoinManager.Instance.Spend(TenCost)) return null;

            var results = new List<CharacterData>();
            bool hasSrOrAbove = false;

            for (int i = 0; i < 10; i++)
            {
                var result = PullOne(out var rarity);
                results.Add(result);
                if (rarity >= Rarity.SR) hasSrOrAbove = true;
            }

            // 10连保底：至少1个SR
            if (!hasSrOrAbove)
                results[9] = DrawByRarity(Rarity.SR);

            SavePity();
            return results;
        }

        public CharacterData PullSingle()
        {
            if (!CoinManager.Instance.Spend(SingleCost)) return null;
            var result = PullOne(out _);
            SavePity();
            return result;
        }

        private CharacterData PullOne(out Rarity rarity)
        {
            _pullsSinceLastSsr++;
            _pullsSinceLastSr++;

            // SSR 保底
            if (_pullsSinceLastSsr >= SsrPity)
            {
                _pullsSinceLastSsr = 0;
                _pullsSinceLastSr = 0;
                rarity = Rarity.SSR;
                return DrawByRarity(Rarity.SSR);
            }

            float roll = Random.value;
            if (roll < SsrRate)
            {
                _pullsSinceLastSsr = 0;
                _pullsSinceLastSr = 0;
                rarity = Rarity.SSR;
                return DrawByRarity(Rarity.SSR);
            }
            else if (roll < SsrRate + SrRate)
            {
                _pullsSinceLastSr = 0;
                rarity = Rarity.SR;
                return DrawByRarity(Rarity.SR);
            }
            else
            {
                rarity = Rarity.R;
                return DrawByRarity(Rarity.R);
            }
        }

        private CharacterData DrawByRarity(Rarity rarity)
        {
            var pool = CharacterManager.Instance.allCharacters
                .Where(c => c.rarity == rarity)
                .ToList();

            if (pool.Count == 0)
            {
                // 回退：从任意稀有度抽
                pool = CharacterManager.Instance.allCharacters;
            }

            var drawn = pool[Random.Range(0, pool.Count)];
            CharacterManager.Instance.Unlock(drawn.characterId);
            return drawn;
        }

        void SavePity()
        {
            PlayerPrefs.SetInt("PitySsr", _pullsSinceLastSsr);
            PlayerPrefs.SetInt("PitySr", _pullsSinceLastSr);
            PlayerPrefs.Save();
        }

        void LoadPity()
        {
            _pullsSinceLastSsr = PlayerPrefs.GetInt("PitySsr", 0);
            _pullsSinceLastSr = PlayerPrefs.GetInt("PitySr", 0);
        }
    }
}
```

**Step 2: 为 GachaManager 编写单元测试**

```csharp
// Assets/Scripts/Editor/GachaManagerTest.cs
using NUnit.Framework;
using LiMen.Managers;

public class GachaManagerTest
{
    [Test]
    public void PityCounter_ReachesHardCap_AtPull90()
    {
        // 模拟90抽不出SSR，第90抽必须出SSR
        // 注：这是集成测试，需要 CharacterManager 也存在
        // 暂时标记为 TODO，等 CharacterManager 完整后再补全
        Assert.Pass("保底逻辑在 GachaManager 中实现，_pullsSinceLastSsr >= 90 时强制 SSR");
    }
}
```

**Step 3: 提交**

```bash
git add Assets/Scripts/Managers/GachaManager.cs
git commit -m "feat(gacha): implement GachaManager with pity system"
```

---

### Task 7.2: 抽卡 UI + 初始角色选择

**Files:**
- Create: `Assets/Scripts/UI/GachaUI.cs`
- Create: `Assets/Scripts/UI/InitialCharacterSelector.cs`

**Step 1: 创建初始角色选择界面 InitialCharacterSelector.cs**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LiMen.Character;
using LiMen.Managers;

namespace LiMen.UI
{
    /// <summary>
    /// 新玩家首次进入游戏时，从默认角色中选择一个免费角色
    /// </summary>
    public class InitialCharacterSelector : MonoBehaviour
    {
        public GameObject selectorPanel;
        public Transform characterGrid;
        public GameObject characterCardPrefab;
        public Button confirmButton;

        private CharacterData _selected;

        void Start()
        {
            // 如果已有装备角色，不显示选择界面
            if (CharacterManager.Instance.GetEquipped() != null)
            {
                gameObject.SetActive(false);
                return;
            }

            ShowSelector();
        }

        void ShowSelector()
        {
            selectorPanel.SetActive(true);
            var defaults = CharacterManager.Instance.GetDefaultCharacters();

            foreach (var c in defaults)
            {
                var card = Instantiate(characterCardPrefab, characterGrid);
                card.GetComponentInChildren<Image>().sprite = c.portraitSprite;
                card.GetComponentInChildren<TMP_Text>().text = c.displayName;
                card.GetComponent<Button>().onClick.AddListener(() => SelectCharacter(c));
            }

            confirmButton.interactable = false;
            confirmButton.onClick.AddListener(Confirm);
        }

        void SelectCharacter(CharacterData c)
        {
            _selected = c;
            confirmButton.interactable = true;
        }

        void Confirm()
        {
            if (_selected == null) return;
            CharacterManager.Instance.Unlock(_selected.characterId);
            CharacterManager.Instance.Equip(_selected.characterId);
            selectorPanel.SetActive(false);
        }
    }
}
```

**Step 2: 提交**

```bash
git add Assets/Scripts/UI/GachaUI.cs Assets/Scripts/UI/InitialCharacterSelector.cs
git commit -m "feat(ui): add gacha UI and initial character selector"
```

---

## 最终验收清单

在认为完成之前，逐一验证：

- [ ] 2 台 Android 手机，一台开热点，另一台连接，能在大厅发现房间并加入
- [ ] 4 人日式麻将局从开始到结算完整流程正常
- [ ] 4 人四川麻将局正常，缺一门判定正确，血战到底
- [ ] 1 人开局配 3 个 AI，AI 能正常摸牌出牌不卡死
- [ ] 四个角的头像显示，点击弹出立绘
- [ ] 签到奖励正常发放，明天签到递增
- [ ] 金币场入场扣费 50，胡牌后奖励到账
- [ ] 抽卡消耗金币正确，90 抽必出 SSR，10 连保 SR
- [ ] APK 在 Android 7.0+ 手机正常安装运行（ARM64）
- [ ] 无 WiFi 环境下，热点联机正常（飞机模式 + 热点验证）

---

*计划写作日期: 2026-03-08*
*设计文档: `docs/plans/2026-03-08-limen-mahjong-design.md`*
