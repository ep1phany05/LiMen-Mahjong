using UnityEngine;
using UnityEngine.UI;

namespace PUNLobby
{
    public class WarningPanel : MonoBehaviour
    {
        [SerializeField] private Text title;
        [SerializeField] private RectTransform window;
        [SerializeField] private Text text;

        public void Show(int width, int height, string titleString, string content)
        {
            if (title != null) title.text = titleString;
            if (window != null) window.sizeDelta = new Vector2(width, height);
            if (text != null) text.text = content;
            gameObject.SetActive(true);
        }

        public void Show(int width, int height, string content)
        {
            Show(width, height, string.Empty, content);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }
    }
}
