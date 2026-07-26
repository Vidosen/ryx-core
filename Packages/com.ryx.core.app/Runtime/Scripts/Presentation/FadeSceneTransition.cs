using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Ryx.Core.App
{
    /// <summary>
    /// Presentation-side <see cref="ISceneTransition"/>: a full-screen <see cref="CanvasGroup"/>
    /// that fades in before a scene swap and out after it. Put this on a canvas inside the
    /// persistent boot scene and register it with the composition root.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class FadeSceneTransition : MonoBehaviour, ISceneTransition
    {
        [Header("Timing")]
        [SerializeField, Min(0f)] private float _coverDuration = 0.25f;
        [SerializeField, Min(0f)] private float _revealDuration = 0.25f;

        [Header("Optional progress readout")]
        [SerializeField] private Image _progressFill;
        [SerializeField] private CanvasGroup _progressGroup;

        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            SetCovered(false);
        }

        public UniTask CoverAsync(CancellationToken cancellationToken = default)
        {
            EnsureCanvasGroup();
            if (_progressGroup) _progressGroup.alpha = 1f;
            if (_progressFill) _progressFill.fillAmount = 0f;
            _canvasGroup.blocksRaycasts = true;
            return FadeAsync(1f, _coverDuration, cancellationToken);
        }

        public async UniTask RevealAsync(CancellationToken cancellationToken = default)
        {
            EnsureCanvasGroup();
            if (_progressGroup) _progressGroup.alpha = 0f;
            await FadeAsync(0f, _revealDuration, cancellationToken);
            _canvasGroup.blocksRaycasts = false;
        }

        public void ReportProgress(float normalized)
        {
            if (_progressFill) _progressFill.fillAmount = Mathf.Clamp01(normalized);
        }

        /// <summary>Snap to fully covered / fully revealed without animating.</summary>
        public void SetCovered(bool covered)
        {
            EnsureCanvasGroup();
            _canvasGroup.alpha = covered ? 1f : 0f;
            _canvasGroup.blocksRaycasts = covered;
            if (_progressGroup) _progressGroup.alpha = 0f;
        }

        /// <remarks>
        /// The composition root instantiates this prefab from a lazy factory, so a transition
        /// can be requested in the same frame — before Unity has dispatched <c>Awake</c>.
        /// </remarks>
        private void EnsureCanvasGroup()
        {
            if (!_canvasGroup) _canvasGroup = GetComponent<CanvasGroup>();
        }

        private async UniTask FadeAsync(float targetAlpha, float duration, CancellationToken cancellationToken)
        {
            var startAlpha = _canvasGroup.alpha;

            if (duration <= 0f || Mathf.Approximately(startAlpha, targetAlpha))
            {
                _canvasGroup.alpha = targetAlpha;
                return;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            _canvasGroup.alpha = targetAlpha;
        }
    }
}
