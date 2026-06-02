using UnityEngine;
using UnityEngine.Localization;

namespace Ryx.Core.Tutorials
{

[System.Serializable]
public class TutorialStep
{
    [Tooltip("Unique identifier for the tutorial step")]
    public string stepId;

    [Tooltip("Description of the tutorial task")]
    public LocalizedString description;

    [Tooltip("Optional specific UI hint text")]
    public LocalizedString hintText;

    [Tooltip("Optional UI element to highlight")]
    public RectTransform highlightUIElement;

    [Tooltip("Optional game objects to highlight")]
    public GameObject[] highlightGameObjects;

    [Tooltip("Optional target destination to show path to")]
    public Transform targetDestination;

    [Tooltip("Optional objects to enable")]
    public GameObject[] stepObjectsToEnable;

    [Tooltip("Optional trigger that completes this step when armed by TutorialManager")]
    public TutorialTrigger completionTrigger;

    [Tooltip("Color of the outline for highlighted objects")]
    public Color outlineColor = Color.yellow;

    [Tooltip("Width of the outline")]
    [Range(1f, 10f)]
    public float outlineWidth = 5f;

    [Tooltip("Event triggered when this step is completed")]
    public UnityEngine.Events.UnityEvent<string> onStepCompleted;

    [HideInInspector]
    public bool isCompleted = false;
}

}
