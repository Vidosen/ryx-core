using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace Ryx.Core.Tutorials
{

public sealed class TutorialIntroView : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private LocalizeStringEvent text;
    [SerializeField] private Image illustration;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    private TutorialManager _manager;

    public void Bind(TutorialManager manager)
    {
        _manager = manager;
        previousButton?.onClick.AddListener(_manager.ShowPreviousIntroPage);
        nextButton?.onClick.AddListener(_manager.ShowNextIntroPage);
    }

    public void Show(TutorialIntroPage page, bool hasPrevious)
    {
        if (panel) panel.SetActive(true);
        if (text) text.StringReference = page.text;
        if (illustration)
        {
            illustration.sprite = page.illustration;
            illustration.gameObject.SetActive(page.illustration);
        }
        if (previousButton) previousButton.gameObject.SetActive(hasPrevious);
    }

    public void Hide()
    {
        if (panel) panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_manager == null) return;
        previousButton?.onClick.RemoveListener(_manager.ShowPreviousIntroPage);
        nextButton?.onClick.RemoveListener(_manager.ShowNextIntroPage);
    }
}

}
