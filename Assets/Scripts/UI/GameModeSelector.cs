using TMPro;
using LiMen.Network;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace LiMen.UI
{
    public enum GameMode
    {
        Riichi,
        Sichuan
    }

    public class GameModeSelector : MonoBehaviour
    {
        public Button riichiButton;
        public Button sichuanButton;
        public TMP_Text selectedModeText;
        public Text selectedModeLegacyText;

        public static GameMode SelectedMode { get; private set; } = GameMode.Riichi;
        public static bool HasModeSelectionConfirmed { get; private set; }
        public static event Action<bool> OnModeSelectionStateChanged;

        private const string ModeKey = "GameMode";
        private LiMenNetworkManager manager;
        private bool canChangeMode = true;

        void Awake()
        {
            var modeRaw = PlayerPrefs.GetString(ModeKey, GameMode.Riichi.ToString());
            if (System.Enum.TryParse(modeRaw, out GameMode parsedMode))
                SelectedMode = parsedMode;
            else
                SelectedMode = GameMode.Riichi;

            // 每次进入房间都要求显式选择一次模式，再开放 Check Rules。
            HasModeSelectionConfirmed = false;
            OnModeSelectionStateChanged?.Invoke(HasModeSelectionConfirmed);
        }

        private void OnEnable()
        {
            manager = LiMenNetworkManager.singleton;
            if (manager != null)
                manager.OnRoomStateChanged += OnRoomStateChanged;
        }

        private void OnDisable()
        {
            if (manager != null)
                manager.OnRoomStateChanged -= OnRoomStateChanged;
        }

        void Start()
        {
            EnsureUiReferences();
            BindButtonEvents();
            manager?.RequestRoomState();
            UpdateUI();
        }

        private void BindButtonEvents()
        {
            if (riichiButton != null)
            {
                // 清理场景里可能遗留的持久化事件（例如从 CheckRuleButton 复制出来的按钮）
                riichiButton.onClick = new Button.ButtonClickedEvent();
                riichiButton.onClick.AddListener(OnRiichiButtonClicked);
            }

            if (sichuanButton != null)
            {
                sichuanButton.onClick = new Button.ButtonClickedEvent();
                sichuanButton.onClick.AddListener(OnSichuanButtonClicked);
            }
        }

        private void OnRiichiButtonClicked()
        {
            SelectMode(GameMode.Riichi);
        }

        private void OnSichuanButtonClicked()
        {
            SelectMode(GameMode.Sichuan);
        }

        private void SelectMode(GameMode mode)
        {
            if (!canChangeMode) return;
            SelectedMode = mode;
            PlayerPrefs.SetString(ModeKey, mode.ToString());
            PlayerPrefs.Save();
            manager?.SetSelectedGameMode(mode.ToString());
            SetModeSelectionConfirmed(true);
            UpdateUI();
        }

        private void OnRoomStateChanged(RoomStateSnapshot state)
        {
            if (System.Enum.TryParse(state.GameMode, out GameMode parsedMode))
            {
                SelectedMode = parsedMode;
            }
            canChangeMode = state.LocalConnectionId == state.HostConnectionId;

            // 非房主不能切模式，收到房主状态后直接开放规则查看。
            if (!canChangeMode)
                SetModeSelectionConfirmed(true);

            UpdateUI();
        }

        private void EnsureUiReferences()
        {
            if (IsReferenceSetAndVisible())
                return;

            TryFindReferencesInChildren();
        }

        private bool IsReferenceSetAndVisible()
        {
            if (riichiButton == null || sichuanButton == null)
                return false;
            return !IsTransformInvisible(riichiButton.transform) && !IsTransformInvisible(sichuanButton.transform);
        }

        private void TryFindReferencesInChildren()
        {
            if (riichiButton == null)
                riichiButton = FindButtonByName("riichi");

            if (sichuanButton == null)
                sichuanButton = FindButtonByName("sichuan");

            if (selectedModeText == null)
            {
                foreach (var text in GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text != null && text.name.ToLowerInvariant().Contains("mode"))
                    {
                        selectedModeText = text;
                        break;
                    }
                }
            }

            if (selectedModeLegacyText == null)
            {
                foreach (var text in GetComponentsInChildren<Text>(true))
                {
                    if (text != null && text.name.ToLowerInvariant().Contains("mode"))
                    {
                        selectedModeLegacyText = text;
                        break;
                    }
                }
            }
        }

        private Button FindButtonByName(string keyword)
        {
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                if (button == null) continue;
                if (button.name.ToLowerInvariant().Contains(keyword))
                    return button;
            }

            return null;
        }

        private static bool IsTransformInvisible(Transform target)
        {
            if (target == null) return true;

            Vector3 lossy = target.lossyScale;
            return Mathf.Abs(lossy.x) < 0.0001f || Mathf.Abs(lossy.y) < 0.0001f;
        }

        private void UpdateUI()
        {
            if (selectedModeText != null)
            {
                selectedModeText.text = SelectedMode == GameMode.Riichi
                    ? "Current Mode: Riichi"
                    : "Current Mode: Sichuan";
            }
            if (selectedModeLegacyText != null)
            {
                selectedModeLegacyText.text = SelectedMode == GameMode.Riichi
                    ? "Current Mode: Riichi"
                    : "Current Mode: Sichuan";
            }

            if (riichiButton != null) riichiButton.interactable = canChangeMode;
            if (sichuanButton != null) sichuanButton.interactable = canChangeMode;
        }

        private static void SetModeSelectionConfirmed(bool value)
        {
            if (HasModeSelectionConfirmed == value)
                return;

            HasModeSelectionConfirmed = value;
            OnModeSelectionStateChanged?.Invoke(value);
        }
    }
}
