# LiMen Mahjong — 完整设计文档

**日期**: 2026-03-08
**Unity 版本**: 2022.3.62f3 LTS
**目标平台**: Android ARM64 (API 24+)
**基础项目**: LiMen-Mahjong (原 Unity 2019.1.8f1, Photon PUN2)

---

## 1. 项目概述

一款面向 Android 手机的日式/四川双模式麻将游戏，支持 2-4 人本地热点/局域网联机（无需互联网），带有雀魂风格 2D 动漫立绘、角色抽卡和金币经济系统。主要使用场景：几位朋友聚在一起，拿着手机打麻将，飞机/地铁/户外均可游玩。

---

## 2. 核心需求

| 需求 | 说明 |
|------|------|
| 离线联机 | 热点模式（一人开热点，其余人连接），无需互联网 |
| 游戏模式 | 日式麻将（立直）+ 四川麻将，开局前选择 |
| AI 填位 | 不足 4 人时 AI 机器人填补空位 |
| 角色立绘 | miHoYo 官方素材（原神/崩坏3/星穹铁道），静态 2D PNG |
| 抽卡系统 | R/SR/SSR 稀有度，保底机制，纯本地无氪金 |
| 经济系统 | 金币（对局奖励/签到），友谊场/金币场 |
| 无服务器 | 全部逻辑本地运行，数据 PlayerPrefs + JSON 持久化 |

---

## 3. 技术架构

### 3.1 总览

```
LiMen Mahjong
│
├─ 网络层      Mirror Networking (替换 Photon PUN2)
│              NetworkDiscovery (LAN 自动发现)
│              Host-Client 架构 (房主 = 服务器 + 客户端)
│
├─ 游戏逻辑   日式麻将 (Riichi)  ← 现有代码保留
│              四川麻将 (Sichuan) ← 新增 SichuanLogic 模块
│              AI 玩家            ← 新增 AiPlayerController
│
├─ 角色系统   CharacterData (ScriptableObject)
│              稀有度 R / SR / SSR
│              miHoYo 静态立绘 PNG
│
├─ 经济系统   CoinManager (本地)
│              签到 / 对局奖励 / 成就 / 防破产
│
└─ 抽卡系统   GachaManager (本地)
               保底计数器 (JSON 持久化)
```

### 3.2 网络方案

**旧架构 (Photon PUN2)**：所有客户端 → Photon 云服务器 → 转发消息（需互联网）

**新架构 (Mirror LAN)**：
```
Host 手机（房主开热点）
  ├── 运行游戏服务器逻辑 (isServer = true)
  └── 同时作为客户端游玩
其他手机 → WiFi 连接热点 → 直连 Host (TCP/UDP)
NetworkDiscovery → 自动广播/发现，无需手动输 IP
```

**Photon → Mirror API 映射**：

| Photon PUN2 | Mirror |
|------------|--------|
| `MonoBehaviourPun` | `NetworkBehaviour` |
| `PhotonNetwork.IsMasterClient` | `isServer` |
| `photonView.RPC(method, ...)` | `[ClientRpc]` / `[Command]` |
| `PhotonNetwork.RaiseEvent` | `NetworkServer.SendToAll` |
| `OnJoinedRoom` 等回调 | `NetworkManager` 覆写方法 |
| Photon Room/Lobby | 自定义 LAN 房间发现 UI |

### 3.3 游戏逻辑

**日式麻将 (保留现有代码)**：
- `MahjongLogic.cs` — 手牌分析、胡牌判定
- `YakuLogic.cs` — 40+ 役种
- `MahjongSet.cs` — 牌山管理
- `GameSetting.cs` — 规则配置

**四川麻将 (新增)**：

| 要素 | 规则 |
|------|------|
| 牌数 | 108 张（万/筒/索各 36 张，无字牌） |
| 胡牌条件 | 一对 + 三个面子，且必须缺一门 |
| 吃牌 | **不支持** |
| 碰/杠 | 支持 |
| 血战到底 | 有人胡牌后其余玩家继续，直到全部结束或牌山摸完 |
| 自摸加番 | 自摸得双倍（其他三家各付一份双倍） |
| 杠后摸牌 | 杠后从尾部摸牌，如自摸胡牌算杠上花 |

```
新增文件：
Assets/Scripts/Mahjong/Logic/SichuanLogic.cs
Assets/Scripts/Mahjong/Model/SichuanGameSetting.cs
Assets/Scripts/GamePlay/Server/State/SichuanXuezhanState.cs
```

### 3.4 AI 玩家

难度：**小白级**（规则型，无搜索）

决策逻辑：
1. **摸牌后**：检查是否可自摸 → 否则执行出牌决策
2. **出牌决策**：优先保留听牌结构，丢弃最孤立的牌（简单向听数计算）
3. **碰/杠判断**：有碰/杠机会时，75% 概率执行
4. **荣和判断**：有胡牌机会时 100% 执行

```
新增文件：
Assets/Scripts/GamePlay/AI/AiPlayerController.cs
Assets/Scripts/GamePlay/AI/AiDecisionMaker.cs
```

---

## 4. 角色立绘系统

### 4.1 UI 布局

```
┌────────────────────────────────────┐
│[P3头像] 名字          名字 [P4头像]│
│ 分数                       分数   │
│                                    │
│          3D 麻将桌面                │
│         (现有，保持不变)            │
│                                    │
│[P2头像] 名字          名字 [P1头像]│
│ 分数                       分数   │
└────────────────────────────────────┘
点击任意头像圈 → 弹出面板：完整立绘 + 玩家信息
```

### 4.2 数据结构

```csharp
// Assets/Scripts/Character/CharacterData.cs
[CreateAssetMenu]
public class CharacterData : ScriptableObject
{
    public string characterName;
    public Rarity rarity;          // R / SR / SSR
    public string sourceGame;      // "原神" / "崩坏3" / "星穹铁道"
    public Sprite avatarSprite;    // 头像圆图 (128x128)
    public Sprite portraitSprite;  // 完整立绘 (竖版)
    public string description;
    public bool isDefault;         // true = 初始可选角色
}

public enum Rarity { R, SR, SSR }
```

**扩展方式**：新增角色只需：
1. `Assets/Resources/Characters/` 下放两张 PNG（头像 + 立绘）
2. 在 `Assets/GameData/Characters/` 下新建一个 `CharacterData.asset`
3. 零代码修改

### 4.3 miHoYo 素材来源

- 米哈游官方 Fan Kit（原神、崩坏3、星穹铁道官网下载）
- 使用范围：非商业个人项目允许使用，需注明来源
- 存储路径：`Assets/Resources/Characters/Portraits/` 和 `Assets/Resources/Characters/Avatars/`

---

## 5. 金币经济系统

### 5.1 获取途径

| 途径 | 金币 | 说明 |
|------|------|------|
| 每日签到 | 50-200 | 7 天循环：50/60/80/100/120/150/200 |
| 对局胜利 | 80-150 | 按番数/名次给奖励 |
| 对局参与 | 20-40 | 输了也有（不能中途退出）|
| 每日首胜 | 100 | 每天第一场胜利额外奖励 |
| 成就系统 | 50-500 | 里程碑一次性奖励 |

### 5.2 消耗

- 金币场入场费：50 金币
- 单抽：160 金币
- 十连：1600 金币

### 5.3 对局模式

| 模式 | 入场费 | 奖励倍率 |
|------|--------|---------|
| 金币场 | 50 金币 | 100% |
| 友谊场 | 免费 | 50% |

### 5.4 防破产

- 友谊场永远免费
- 金币 < 50 时每日登录自动补到 50（每日低保）
- 金币 < 100 时 UI 引导去友谊场

### 5.5 数据持久化

```json
// playerdata.json
{
  "coins": 1200,
  "ownedCharacters": ["keqing", "bronya", "stelle"],
  "equippedCharacter": "keqing",
  "lastSignInDate": "2026-03-08",
  "consecutiveSignIn": 3,
  "gachaCounter": { "total": 45, "sinceLastSsr": 45, "sinceLastSr": 3 },
  "achievements": { "firstWin": true, "chiitoi": false }
}
```

---

## 6. 抽卡系统

### 6.1 概率

| 稀有度 | 单抽概率 | 保底 |
|--------|---------|------|
| SSR | 0.6% | 90 抽硬保底 |
| SR | 5.1% | 10 抽至少 1 SR |
| R | 94.3% | — |

### 6.2 规则

- 十连保底 1 SR（即使全 R 也会替换最后一张为 SR）
- 90 抽未出 SSR → 第 90 抽必出 SSR
- 保底计数器写入 JSON，重装 App 后保留
- 重复角色：未来可扩展为"碎片兑换"，现阶段直接跳过（已拥有时再次抽到不消耗保底，重新抽）

### 6.3 初始角色

新玩家从 3-5 个基础 R 级角色中自选 1 个（免费），进入游戏前强制选择。

---

## 7. Android 构建规格

| 配置项 | 值 |
|--------|-----|
| Target Architecture | ARM64（64 位，现代手机标准）|
| Minimum API Level | Android 7.0 (API 24) |
| Target API Level | Android 13 (API 33) |
| Scripting Backend | IL2CPP |
| Build System | Gradle |
| Screen Orientation | Landscape（横屏固定）|
| 触控输入 | Input System 或 EventSystem Touch，替换鼠标点击 |

---

## 8. 实施阶段

| 阶段 | 核心工作 | 验收标准 |
|------|---------|---------|
| **P1** 网络迁移 | 删除 Photon PUN2，接入 Mirror，实现 LAN Discovery，迁移 ServerBehaviour/ClientBehaviour RPC 调用 | 2 台手机热点下能完整打一局 |
| **P2** Android 构建 | 配置 ARM64/IL2CPP，修复触控 UI（手牌点击/出牌），适配手机分辨率 | APK 安装到手机正常游玩 |
| **P3** 四川麻将 | 实现 SichuanLogic，108 张牌集，缺一门判定，血战到底规则，开局模式选择界面 | 四川模式完整可玩 |
| **P4** AI 填位 | AiPlayerController，决策器，房间内 AI 槽位选择 | 1 人开局对 3 个 AI 可正常进行 |
| **P5** 角色立绘 | CharacterData ScriptableObject，四角头像 UI，立绘弹出面板，接入 miHoYo 素材 | 四角有头像，点击有立绘 |
| **P6** 金币经济 | CoinManager，签到系统，对局结算奖励，成就，友谊场/金币场，防破产 | 完整经济循环运作 |
| **P7** 抽卡系统 | GachaManager，抽卡 UI，保底逻辑，角色解锁，初始角色选择 | 抽卡保底正确，数据持久化 |

---

## 9. 关键文件变更清单

### 删除
- `Assets/Photon/` 整个目录
- `Assets/Scripts/PUNLobby/` → 替换为 Mirror Lobby

### 新增
- `Assets/Mirror/` — Mirror Networking 包
- `Assets/Scripts/Network/` — MirrorNetworkManager, LanDiscovery, MirrorLobby
- `Assets/Scripts/Mahjong/Logic/SichuanLogic.cs`
- `Assets/Scripts/GamePlay/AI/AiPlayerController.cs`
- `Assets/Scripts/Character/CharacterData.cs`
- `Assets/Scripts/Managers/CoinManager.cs`
- `Assets/Scripts/Managers/GachaManager.cs`
- `Assets/Scripts/Managers/AchievementManager.cs`
- `Assets/Scripts/Managers/SignInManager.cs`
- `Assets/Scripts/UI/CharacterPortraitUI.cs`
- `Assets/Scripts/UI/GachaUI.cs`
- `Assets/Resources/Characters/` — miHoYo 立绘 PNG

### 修改
- `Assets/Scripts/GamePlay/Server/ServerBehaviour.cs` → 继承 `NetworkBehaviour`
- `Assets/Scripts/GamePlay/Client/ClientBehaviour.cs` → 继承 `NetworkBehaviour`
- 所有 `photonView.RPC(...)` → Mirror `[ClientRpc]` / `[Command]`
- `Assets/Scripts/PUNLobby/Launcher.cs` → 替换为 `MirrorNetworkManager.cs`
- `ProjectSettings/ProjectSettings.asset` → Android ARM64 配置

---

*设计确认日期: 2026-03-08*
*下一步: 制定分阶段实施计划 (writing-plans)*
