using System.Collections.Generic;
using UnityEngine;

public class DialogueController : MonoBehaviour
{
    public static DialogueController Instance { get; private set; }

    [SerializeField] private DialogueViewUITK view;
    [SerializeField] private DialogueConditions conditions;
    [SerializeField] private DialogueEvents eventsBus;
    [SerializeField] private string tutorialCompletedPrefKey = "ClickerKingdom.Tutorial.Completed";

    public System.Action<bool> OnInputLockChanged;

    private List<DialogueLine> _sequenceLines;
    private int _sequenceIndex;

    private List<DialogueStep> _tutorialSteps;
    private int _stepIndex;

    private DialogueStep.CompletionMode _activeMode;
    private string _activeCondition;
    private string _activeEventId;
    private bool _activeLockInput;
    private bool _isOpen;
    private bool _tutorialCompleted;

    /*
    README: Trigger tutorial gates from gameplay by calling:
    - DialogueController.Instance.Conditions.SetFlag("FarmBuilt", true)
    - DialogueController.Instance.Events.Raise("SomeEventId")
    */

    public DialogueConditions Conditions => conditions;
    public DialogueEvents Events => eventsBus;
    public bool IsOpen => _isOpen;
    public bool TutorialCompleted => _tutorialCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        _tutorialCompleted = PlayerPrefs.GetInt(tutorialCompletedPrefKey, 0) == 1;
        if (_tutorialCompleted && conditions != null)
        {
            conditions.SetFlag("TutorialCompleted", true);
        }
    }

    private void Update()
    {
        if (!_isOpen)
        {
            return;
        }

        if (_activeMode == DialogueStep.CompletionMode.OnCondition && !string.IsNullOrEmpty(_activeCondition))
        {
            if (conditions != null && conditions.Evaluate(_activeCondition))
            {
                TryAdvance();
            }
        }
    }

    public void StartSequence(DialogueSequence seq)
    {
        if (seq == null || seq.lines == null || seq.lines.Count == 0)
        {
            return;
        }

        _tutorialSteps = null;
        _sequenceLines = seq.lines;
        _sequenceIndex = 0;

        Open();
        ShowLine(_sequenceLines[_sequenceIndex]);
        SetActiveMode(DialogueStep.CompletionMode.OnNext, null, null, false);
    }

    public void StartTutorial(List<DialogueStep> steps)
    {
        if (_tutorialCompleted)
        {
            if (conditions != null)
            {
                conditions.SetFlag("TutorialCompleted", true);
            }
            return;
        }

        if (steps == null || steps.Count == 0)
        {
            return;
        }

        _sequenceLines = null;
        _tutorialSteps = steps;
        _stepIndex = 0;
        _tutorialCompleted = false;

        Open();
        ApplyStep(_tutorialSteps[_stepIndex]);
    }

    public void TryAdvance()
    {
        if (!_isOpen)
        {
            return;
        }

        if (_activeMode == DialogueStep.CompletionMode.OnCondition)
        {
            if (conditions == null || !conditions.Evaluate(_activeCondition))
            {
                return;
            }
        }

        AdvanceInternal();
    }

    public void Close()
    {
        SetActiveMode(DialogueStep.CompletionMode.OnNext, null, null, false);
        _sequenceLines = null;
        _tutorialSteps = null;
        _sequenceIndex = 0;
        _stepIndex = 0;
        _isOpen = false;
        view?.SetVisible(false);
        SetInputLock(false);
    }

    private void AdvanceInternal()
    {
        if (_sequenceLines != null)
        {
            _sequenceIndex++;
            if (_sequenceIndex >= _sequenceLines.Count)
            {
                Close();
                return;
            }

            ShowLine(_sequenceLines[_sequenceIndex]);
            SetActiveMode(DialogueStep.CompletionMode.OnNext, null, null, false);
            return;
        }

        if (_tutorialSteps != null)
        {
            _stepIndex++;
            if (_stepIndex >= _tutorialSteps.Count)
            {
                _tutorialCompleted = true;
                if (conditions != null)
                {
                    conditions.SetFlag("TutorialCompleted", true);
                }

                PlayerPrefs.SetInt(tutorialCompletedPrefKey, 1);
                PlayerPrefs.Save();

                Close();
                return;
            }

            ApplyStep(_tutorialSteps[_stepIndex]);
        }
    }

    private void ApplyStep(DialogueStep step)
    {
        ShowLine(step.line);
        SetActiveMode(step.completionMode, step.conditionExpressionOrKey, step.eventId, step.lockInput);
    }

    private void SetActiveMode(DialogueStep.CompletionMode mode, string condition, string eventId, bool lockInput)
    {
        if (!string.IsNullOrEmpty(_activeEventId) && eventsBus != null)
        {
            eventsBus.Unsubscribe(_activeEventId, OnEventAdvance);
        }

        _activeMode = mode;
        _activeCondition = condition;
        _activeEventId = eventId;
        _activeLockInput = lockInput;

        if (_activeMode == DialogueStep.CompletionMode.OnEvent && !string.IsNullOrEmpty(_activeEventId) && eventsBus != null)
        {
            eventsBus.Subscribe(_activeEventId, OnEventAdvance);
        }

        view?.SetNextEnabled(_activeMode == DialogueStep.CompletionMode.OnNext);
        SetInputLock(_activeLockInput);
    }

    private void OnEventAdvance()
    {
        TryAdvance();
    }

    private void ShowLine(DialogueLine line)
    {
        _isOpen = true;
        view?.SetVisible(true);
        view?.ShowLine(line);
    }

    private void Open()
    {
        _isOpen = true;
        view?.SetVisible(true);
    }

    private void SetInputLock(bool locked)
    {
        OnInputLockChanged?.Invoke(locked);
    }
}
