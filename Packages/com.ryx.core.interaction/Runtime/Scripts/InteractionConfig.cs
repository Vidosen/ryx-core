using UnityEngine;
using UnityEngine.Localization;

namespace Ryx.Core.Interaction
{
    [CreateAssetMenu(fileName = "InteractionConfig", menuName = "Ryx/Interaction/Config")]
    public class InteractionConfig : ScriptableObject
    {
        [SerializeField] private InteractionType interactionType = InteractionType.None;
        [SerializeField] private LocalizedString label;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField] private Color color;

        [Header("UI Context")]
        [Tooltip("Contexts where this button is visible. Empty = visible in default context only.")]
        [SerializeField] private InteractionUIContext[] _validContexts;
        [Tooltip("Global buttons are visible in ANY context (e.g., system/pause buttons).")]
        [SerializeField] private bool _isGlobalButton;

        public string Id => name;
        public InteractionType Type => interactionType;
        public LocalizedString Label => label;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public Color Color => color;
        /// <summary>
        /// Contexts where this button is visible. Empty array means default context only.
        /// </summary>
        public InteractionUIContext[] ValidContexts => _validContexts ?? System.Array.Empty<InteractionUIContext>();

        /// <summary>
        /// Global buttons are visible in any context.
        /// </summary>
        public bool IsGlobalButton => _isGlobalButton;
    }
}





