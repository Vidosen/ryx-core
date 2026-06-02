using UnityEngine;

namespace Ryx.Core.Tutorials
{

public class TutorialHighlightToggle : MonoBehaviour, ITutorialHighlight
{
    [Tooltip("Components that should be enabled while highlighted")]
    [SerializeField] private Behaviour[] componentsToToggle = new Behaviour[0];

    public void SetHighlighted(bool highlighted, TutorialHighlightStyle style)
    {
        foreach (var component in componentsToToggle)
        {
            if (component)
                component.enabled = highlighted;
        }
    }
}

}
