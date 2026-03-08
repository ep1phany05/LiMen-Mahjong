# LiMen-Mahjong

A local multiplayer mahjong game for Android, supporting both Japanese Riichi and Sichuan ruleset. Play with friends over a mobile hotspot — no internet required.

Built with Unity 2022.3.62f3. Anime-style 2D character portraits inspired by [Mahjong Soul](https://www.maj-soul.com/).

---

## Features

### Implemented (Base Project)
- ✅ Full Japanese Riichi Mahjong logic — 40+ yaku, fu/han scoring, tsumo/ron
- ✅ 3D mahjong table with 76 tile textures
- ✅ 2–4 player support
- ✅ Complete game state machine (draw, discard, pon, kan, win, scoring)
- ✅ Unity 2022.3.62f3 (migrated from 2019.1.8f1)

### In Development
- 🔧 **P1** — Local LAN multiplayer via [Mirror Networking](https://mirror-networking.gitbook.io/) (replacing Photon PUN2, no internet needed)
- 🔧 **P2** — Android ARM64 build + touch UI optimization
- 🔧 **P3** — Sichuan Mahjong ruleset (108 tiles, queyi, xuezhan mode)
- 🔧 **P4** — AI players to fill empty seats
- 🔧 **P5** — 2D anime character portraits (miHoYo fan art assets)
- 🔧 **P6** — Coin economy (daily sign-in, match rewards, friendly/ranked rooms)
- 🔧 **P7** — Gacha system (R/SR/SSR, pity at 90 pulls, no pay-to-win)

---

## Gameplay

- **2–4 players** on the same WiFi hotspot — works on planes, outdoors, anywhere
- Choose game mode before starting: **Riichi (日式)** or **Sichuan (四川)**
- AI bots fill empty seats when playing with fewer than 4 people
- Character portraits in each corner — tap to view full artwork

---

## Tech Stack

| | |
|---|---|
| Engine | Unity 2022.3.62f3 LTS |
| Platform | Android (ARM64, API 24+) |
| Networking | Mirror Networking (LAN Discovery) |
| Persistence | PlayerPrefs + local JSON |
| Art | miHoYo official fan kit assets |

---

## Project Structure

```
Assets/
├── Scripts/
│   ├── Mahjong/          # Core game logic (MahjongLogic, YakuLogic, SichuanLogic)
│   ├── GamePlay/         # Server & client state machines
│   ├── Network/          # Mirror NetworkManager, LAN Discovery
│   ├── Character/        # CharacterData ScriptableObjects
│   ├── Managers/         # CoinManager, GachaManager, SignInManager
│   └── UI/               # Portrait UI, Gacha UI, Lobby UI
├── Scenes/
│   ├── Lobby.unity       # LAN room discovery & creation
│   ├── Room.unity        # Room setup, mode selection
│   └── Mahjong.unity     # Main gameplay scene
└── docs/plans/           # Design doc & implementation plan
```

---

## Getting Started (Development)

Requirements: Unity 2022.3.62f3, Android Build Support module (ARM64)

```bash
git clone git@github.com:ep1phany05/LiMen-Mahjong.git
# Open project in Unity Hub → select Unity 2022.3.62f3
```

---

## License

Personal non-commercial project. Character artwork from miHoYo fan kits — all rights belong to miHoYo Co., Ltd.
