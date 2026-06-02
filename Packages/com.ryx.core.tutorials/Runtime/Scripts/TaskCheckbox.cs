using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace Ryx.Core.Tutorials
{

/// <summary>
/// Represents a checkbox item in the tutorial task list.
/// </summary>
public class TaskCheckbox : MonoBehaviour
{
    [HideInInspector]
    public int stepIndex;

    public Color completedColor;
    public Color notCompletedColor;

    private Toggle toggle;
    private Text text;

    [Tooltip("The text component for the task description")]
    [SerializeField] private LocalizeStringEvent taskText = null;

    private void Awake()
    {
        toggle = GetComponentInChildren<Toggle>();
        text = GetComponentInChildren<Text>();
    }

    public LocalizeStringEvent TextComponent => taskText;

    public void SetCompleted(bool completed)
    {
        if (toggle)
        {
            toggle.isOn = completed;
        }

        if (text)
        {
            text.color = completed ? completedColor : notCompletedColor;
        }
    }
}

}
