using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class DialogueViewUITK : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private DialogueController controller;
    [Header("Portrait Layout")]
    [SerializeField] private Vector2 portraitOffset = Vector2.zero;
    [SerializeField] private Vector2 portraitSize = new Vector2(220f, 220f);

    private VisualElement _root;
    private VisualElement _dialogueRoot;
    private VisualElement _portrait;
    private Label _dialogueText;
    private Button _nextButton;

    private void Awake()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (controller == null)
        {
            controller = DialogueController.Instance;
        }

        _root = uiDocument.rootVisualElement;
        _dialogueRoot = _root.Q<VisualElement>("dialogueRoot");
        _portrait = _root.Q<VisualElement>("portrait");
        _dialogueText = _root.Q<Label>("dialogueText");
        _nextButton = _root.Q<Button>("nextButton");

        if (_nextButton != null)
        {
            _nextButton.clicked += () => controller?.TryAdvance();
        }

        SetVisible(false);
        ApplyPortraitLayout();
    }

    public void ShowLine(DialogueLine line)
    {
        if (line == null)
        {
            return;
        }

        if (_dialogueText != null)
        {
            _dialogueText.text = line.text;
        }

        if (_portrait != null)
        {
            if (line.portrait != null)
            {
                _portrait.style.display = DisplayStyle.Flex;
                _portrait.style.backgroundImage = new StyleBackground(line.portrait);
            }
            else
            {
                _portrait.style.display = DisplayStyle.None;
            }
        }
    }

    public void SetVisible(bool visible)
    {
        if (_dialogueRoot == null)
        {
            return;
        }

        _dialogueRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void SetNextEnabled(bool enabled)
    {
        if (_nextButton == null)
        {
            return;
        }

        _nextButton.SetEnabled(enabled);
    }

    private void ApplyPortraitLayout()
    {
        if (_portrait == null)
        {
            return;
        }

        _portrait.style.left = portraitOffset.x;
        _portrait.style.bottom = portraitOffset.y;
        _portrait.style.width = portraitSize.x;
        _portrait.style.height = portraitSize.y;
    }
}
