using UnityEngine;
using UnityEngine.UI;

namespace PUNLobby.Room
{
    public class RoomSlotPanel : MonoBehaviour
    {
        public Image roomMaster;
        public Image readySign;
        public Text playerNameText;

        public void Set(bool isMaster, string playerName, bool isReady)
        {
            if (roomMaster != null) roomMaster.gameObject.SetActive(isMaster);
            if (readySign != null) readySign.gameObject.SetActive(isMaster || isReady);
            if (playerNameText != null) playerNameText.text = playerName;
        }
    }
}
