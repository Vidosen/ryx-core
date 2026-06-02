using UnityEngine;

namespace Ryx.Core.Tutorials
{

public readonly struct TutorialHighlightStyle
{
    public TutorialHighlightStyle(Color color, float width)
    {
        Color = color;
        Width = width;
    }

    public Color Color { get; }
    public float Width { get; }
}

public interface ITutorialHighlight
{
    void SetHighlighted(bool highlighted, TutorialHighlightStyle style);
}

}
