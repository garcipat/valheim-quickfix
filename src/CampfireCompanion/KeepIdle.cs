using System.Linq;
using UnityEngine;

namespace ValheimCampfireCompanion
{
    /// <summary>
    /// Keeps the companion in its idle state. The creature's scripts are gone, so the animator flags they would set
    /// (a drake is always flying, Hildir stands when a player is near) are set here instead, and the animator is given
    /// a moment to move from its entry state into the idle those flags lead to. That state is then kept: without the
    /// scripts some controllers drift into another state or play a clip that does not loop, so it is restarted
    /// whenever that happens.
    /// </summary>
    internal sealed class KeepIdle : MonoBehaviour
    {
        // Seconds the animator gets to settle into its idle before that state is kept.
        private const float SettleTime = 1f;

        internal string[] Flags = new string[0];

        private Animator[] _animators;
        private int[][] _flags;
        private int[] _idle;
        private float _settleUntil;

        private void Start()
        {
            _animators = GetComponentsInChildren<Animator>();
            if (_animators.Length == 0)
            {
                Plugin.Log.LogWarning($"{name}: the copied model has no animator, so it cannot move");
            }

            _flags = new int[_animators.Length][];
            for (var i = 0; i < _animators.Length; i++)
            {
                var parameters = _animators[i].runtimeAnimatorController != null ? _animators[i].parameters : new AnimatorControllerParameter[0];
                _flags[i] = parameters
                    .Where(p => p.type == AnimatorControllerParameterType.Bool && Flags.Contains(p.name))
                    .Select(p => p.nameHash)
                    .ToArray();
            }

            _idle = new int[_animators.Length];
            _settleUntil = Time.time + SettleTime;
        }

        private void Update()
        {
            var settled = Time.time >= _settleUntil;
            for (var i = 0; i < _animators.Length; i++)
            {
                var animator = _animators[i];
                if (animator == null || animator.runtimeAnimatorController == null)
                {
                    continue;
                }

                foreach (var flag in _flags[i])
                {
                    animator.SetBool(flag, true);
                }

                if (!settled || animator.IsInTransition(0))
                {
                    continue;
                }

                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (_idle[i] == 0)
                {
                    _idle[i] = state.fullPathHash;
                    Plugin.Log.LogDebug($"{animator.name}: controller {animator.runtimeAnimatorController.name}, idle state {state.fullPathHash}, loops {state.loop}, {state.length:0.00} s, flags set {_flags[i].Length}/{Flags.Length}");
                }

                if (state.fullPathHash != _idle[i] || (!state.loop && state.normalizedTime >= 1f))
                {
                    animator.Play(_idle[i], 0, 0f);
                }
            }
        }
    }
}
