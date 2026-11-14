using UnityEngine;

namespace ValheimCampfireCompanion
{
    /// <summary>
    /// Keeps the companion in the state its animator starts in. Without the creature's scripts some controllers drift
    /// into another state or play a clip that does not loop (the drake freezes after a few seconds), so the first
    /// state is restarted whenever that happens.
    /// </summary>
    internal sealed class KeepIdle : MonoBehaviour
    {
        private Animator[] _animators;
        private int[] _idle;

        private void Start()
        {
            _animators = GetComponentsInChildren<Animator>();
            _idle = new int[_animators.Length];
        }

        private void Update()
        {
            for (var i = 0; i < _animators.Length; i++)
            {
                var animator = _animators[i];
                if (animator == null || animator.runtimeAnimatorController == null || animator.IsInTransition(0))
                {
                    continue;
                }

                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (_idle[i] == 0)
                {
                    _idle[i] = state.fullPathHash;
                }

                if (state.fullPathHash != _idle[i] || (!state.loop && state.normalizedTime >= 1f))
                {
                    animator.Play(_idle[i], 0, 0f);
                }
            }
        }
    }
}
