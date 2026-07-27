using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Splice.UI
{
    [DisallowMultipleComponent]
    public sealed class PrototypeListStateView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionLabel;

        public bool IsComplete =>
            titleText != null && bodyText != null && actionButton != null && actionLabel != null;

        public void InitializeEditorReferences(TMP_Text title, TMP_Text body, Button action,
            TMP_Text label)
        {
            titleText = title;
            bodyText = body;
            actionButton = action;
            actionLabel = label;
        }

        public void Show(string title, string body, Color titleColor, string action,
            UnityAction callback, bool showAction)
        {
            gameObject.SetActive(true);
            titleText.text = title;
            titleText.color = titleColor;
            bodyText.text = body;
            actionLabel.text = action;
            actionButton.gameObject.SetActive(showAction);
            actionButton.onClick.RemoveAllListeners();
            if (callback != null) actionButton.onClick.AddListener(callback);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
