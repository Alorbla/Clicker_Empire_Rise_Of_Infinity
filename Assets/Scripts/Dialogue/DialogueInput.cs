using UnityEngine;
using UnityEngine.InputSystem;

public class DialogueInput : MonoBehaviour
{
    [SerializeField] private InputActionReference advanceDialogue;
    [SerializeField] private InputActionReference skipDialogue;

    private DialogueController _controller;

    private void Awake()
    {
        _controller = DialogueController.Instance;
    }

    private void OnEnable()
    {
        if (advanceDialogue != null)
        {
            advanceDialogue.action.performed += OnAdvance;
            advanceDialogue.action.Enable();
        }

        if (skipDialogue != null)
        {
            skipDialogue.action.performed += OnAdvance;
            skipDialogue.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (advanceDialogue != null)
        {
            advanceDialogue.action.performed -= OnAdvance;
            advanceDialogue.action.Disable();
        }

        if (skipDialogue != null)
        {
            skipDialogue.action.performed -= OnAdvance;
            skipDialogue.action.Disable();
        }
    }

    private void OnAdvance(InputAction.CallbackContext ctx)
    {
        if (_controller == null)
        {
            _controller = DialogueController.Instance;
        }

        _controller?.TryAdvance();
    }
}
