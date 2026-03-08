using System.Collections.Generic;
using Mahjong.Model;
using UI.DataBinding;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PUNLobby
{
    public class RulePanel : MonoBehaviour, IPointerClickHandler
    {
        public RectTransform baseSettingPanel;
        public RectTransform yakuSettingPanel;
        public bool closeWhenBackgroundClicked = true;

        private readonly List<UIBinder> binders = new List<UIBinder>();
        private GameSetting gameSettings;

        private void OnEnable()
        {
            if (baseSettingPanel != null) baseSettingPanel.gameObject.SetActive(true);
            if (yakuSettingPanel != null) yakuSettingPanel.gameObject.SetActive(false);
        }

        public void Show(GameSetting settings)
        {
            gameSettings = settings;
            binders.Clear();
            binders.AddRange(GetComponentsInChildren<UIBinder>(true));
            binders.ForEach(b => b.Target = gameSettings);
            binders.ForEach(b => b?.ApplyBinds());
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        public void OpenYakuSettingPanel()
        {
            if (baseSettingPanel != null) baseSettingPanel.gameObject.SetActive(false);
            if (yakuSettingPanel != null) yakuSettingPanel.gameObject.SetActive(true);
            binders.ForEach(b => b.Target = gameSettings);
            binders.ForEach(b => b?.ApplyBinds());
        }

        public void CloseYakuSettingPanel()
        {
            if (baseSettingPanel != null) baseSettingPanel.gameObject.SetActive(true);
            if (yakuSettingPanel != null) yakuSettingPanel.gameObject.SetActive(false);
            binders.ForEach(b => b.Target = gameSettings);
            binders.ForEach(b => b?.ApplyBinds());
        }

        private void Update()
        {
            if (!gameObject.activeInHierarchy) return;
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            if (yakuSettingPanel != null && yakuSettingPanel.gameObject.activeSelf)
                CloseYakuSettingPanel();
            else
                Close();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!closeWhenBackgroundClicked || eventData == null) return;

            GameObject hit = eventData.pointerCurrentRaycast.gameObject;
            if (hit != gameObject) return;

            if (yakuSettingPanel != null && yakuSettingPanel.gameObject.activeSelf)
                CloseYakuSettingPanel();
            else
                Close();
        }
    }
}
