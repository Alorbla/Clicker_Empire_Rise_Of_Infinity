using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class DialogueDemoStarter : MonoBehaviour
{
    [SerializeField] private DialogueSequence sequence;
    [SerializeField] private TutorialSequence tutorial;
    [SerializeField] private bool startOnPlay = true;
    [SerializeField] private Key startKey = Key.F1;

    private void Start()
    {
        if (startOnPlay)
        {
            TryStartDemo();
        }
    }

    private void Update()
    {
        if (!startOnPlay && Keyboard.current != null && Keyboard.current[startKey].wasPressedThisFrame)
        {
            TryStartDemo();
        }
    }

    private void TryStartDemo()
    {
        var controller = DialogueController.Instance;
        if (controller == null)
        {
            return;
        }

        if (sequence != null)
        {
            controller.StartSequence(sequence);
        }

        if (tutorial != null && tutorial.steps != null && tutorial.steps.Count > 0)
        {
            if (!controller.TutorialCompleted)
            {
                controller.StartTutorial(tutorial.steps);
            }
        }
    }
}
