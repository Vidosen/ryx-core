using UnityEngine;
using UnityEngine.UI;

namespace Ryx.Core.Tutorials
{

/// <summary>
/// Handles UI components for the tutorial system.
/// </summary>
public class TutorialUI : MonoBehaviour
{
    [Header("Tutorial Task Panel")]
    [Tooltip("The panel containing the task information")]
    public GameObject taskPanel;

    [Tooltip("Text component for displaying task information")]
    public Text taskText;

    [Header("Hint UI")]
    [Tooltip("Panel for displaying hints")]
    public GameObject hintPanel;

    [Tooltip("Text component for displaying hints")]
    public Text hintText;

    [Tooltip("Button to hide/dismiss the hint")]
    public Button dismissHintButton;

    [Header("UI Highlighting")]
    [Tooltip("Image used to highlight UI elements")]
    public Image uiHighlighter;

    [Tooltip("Animation for the UI highlighter")]
    public Animation highlighterAnimation;

    [Header("Tutorial Controls")]
    [Tooltip("Button to skip the entire tutorial")]
    public Button skipTutorialButton;

    [Tooltip("Button to skip to the next tutorial step")]
    public Button nextStepButton;

    private TutorialManager tutorialManager;

    private void Awake()
    {
        tutorialManager = FindAnyObjectByType<TutorialManager>();

        if (!tutorialManager)
        {
            Debug.LogError("TutorialUI: No TutorialManager found in scene!");
        }

        if (dismissHintButton)
        {
            dismissHintButton.onClick.AddListener(DismissHint);
        }

        if (skipTutorialButton)
        {
            skipTutorialButton.onClick.AddListener(SkipTutorial);
        }

        if (nextStepButton)
        {
            nextStepButton.onClick.AddListener(NextStep);
        }
    }

    public void DismissHint()
    {
        if (hintPanel)
        {
            hintPanel.SetActive(false);
        }
    }

    public void SkipTutorial()
    {
        if (tutorialManager)
        {
            tutorialManager.SkipTutorial();
        }
    }

    public void NextStep()
    {
        if (tutorialManager)
        {
            tutorialManager.CompleteCurrentStep();
        }
    }

    public void HighlightUIElement(RectTransform elementToHighlight)
    {
        if (!uiHighlighter || !elementToHighlight) return;

        uiHighlighter.gameObject.SetActive(true);
        uiHighlighter.rectTransform.position = elementToHighlight.position;
        uiHighlighter.rectTransform.sizeDelta = elementToHighlight.sizeDelta + new Vector2(20, 20);

        if (highlighterAnimation)
        {
            highlighterAnimation.Play();
        }
    }

    public void HideHighlighter()
    {
        if (uiHighlighter)
        {
            uiHighlighter.gameObject.SetActive(false);
        }
    }
}

}
