using UnityEngine;
using UnityEngine.UIElements;

public class BottomUIController : MonoBehaviour
{
    private VisualElement _root;
    private Button _buildingsButton;
    private Button _upgradesButton;
    private Button _menuButton;
    private VisualElement _buildPanel;
    private VisualElement _upgradePanel;
    private VisualElement _otherPanel;

    private void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null)
        {
            Debug.LogError("BottomUIController requires a UIDocument on the same GameObject.");
            return;
        }

        _root = doc.rootVisualElement;
        _buildingsButton = _root.Q<Button>("BuildingsButton");
        _upgradesButton = _root.Q<Button>("UpgradesButton");
        _menuButton = _root.Q<Button>("MenuButton");
        _buildPanel = _root.Q<VisualElement>("BuildMenuPanel");
        _upgradePanel = _root.Q<VisualElement>("UpgradePanel");
        _otherPanel = _root.Q<VisualElement>("OtherPanels");

        _buildingsButton.clicked += ShowBuild;
        _upgradesButton.clicked += ShowUpgrade;
        _menuButton.clicked += ShowOther;
    }

    private void OnDisable()
    {
        if (_buildingsButton != null) _buildingsButton.clicked -= ShowBuild;
        if (_upgradesButton != null) _upgradesButton.clicked -= ShowUpgrade;
        if (_menuButton != null) _menuButton.clicked -= ShowOther;
    }

    private void ShowBuild()
    {
        SetActivePanel(_buildPanel);
    }

    private void ShowUpgrade()
    {
        SetActivePanel(_upgradePanel);
    }

    private void ShowOther()
    {
        SetActivePanel(_otherPanel);
    }

    private void SetActivePanel(VisualElement panel)
    {
        if (_buildPanel != null) _buildPanel.RemoveFromClassList("active");
        if (_upgradePanel != null) _upgradePanel.RemoveFromClassList("active");
        if (_otherPanel != null) _otherPanel.RemoveFromClassList("active");
        if (panel != null) panel.AddToClassList("active");
    }
}
