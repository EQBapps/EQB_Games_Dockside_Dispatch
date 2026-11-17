using System;
using System.Collections;
using UnityEngine;

namespace Dockside
{
    /// <summary>Tiny coroutine tween helpers, so the prototype needs no third-party packages.</summary>
    public static class Tween
    {
        public static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float EaseInOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
        public static float EaseInCubic(float t) => t * t * t;
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static IEnumerator Run(float duration, Action<float> step, Func<float, float> ease = null)
        {
            ease ??= EaseOutCubic;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                step(ease(Mathf.Clamp01(t / duration)));
                yield return null;
            }
            step(1f);
        }

        /// <summary>Moves in world space along an arc (crane lift), optionally scaling and turning.</summary>
        public static IEnumerator Arc(Transform tr, Vector3 to, float height, float duration, Vector3? toScale = null,
                                      Quaternion? toRotation = null)
        {
            Vector3 from = tr.position;
            Vector3 s0 = tr.localScale, s1 = toScale ?? s0;
            Quaternion r0 = tr.rotation, r1 = toRotation ?? r0;
            yield return Run(duration, e =>
            {
                if (!tr) return;
                var p = Vector3.LerpUnclamped(from, to, e);
                p.y += Mathf.Sin(e * Mathf.PI) * height;
                tr.position = p;
                tr.localScale = Vector3.LerpUnclamped(s0, s1, e);
                tr.rotation = Quaternion.SlerpUnclamped(r0, r1, e);
            }, EaseInOutSine);
        }

        public static IEnumerator Move(Transform tr, Vector3 to, float duration, Func<float, float> ease = null)
        {
            Vector3 from = tr.position;
            yield return Run(duration, e => { if (tr) tr.position = Vector3.LerpUnclamped(from, to, e); }, ease);
        }

        public static IEnumerator ScaleTo(Transform tr, Vector3 to, float duration, Func<float, float> ease = null)
        {
            Vector3 from = tr.localScale;
            yield return Run(duration, e => { if (tr) tr.localScale = Vector3.LerpUnclamped(from, to, e); }, ease);
        }

        /// <summary>Quick squash-and-stretch "pop" used when something lands.</summary>
        public static IEnumerator Pop(Transform tr, float amount = 0.18f, float duration = 0.22f)
        {
            Vector3 baseScale = tr.localScale;
            yield return Run(duration, e =>
            {
                if (!tr) return;
                float k = Mathf.Sin(e * Mathf.PI) * amount;
                tr.localScale = new Vector3(baseScale.x * (1 + k), baseScale.y * (1 - k * 0.8f), baseScale.z * (1 + k));
            }, t => t);
            if (tr) tr.localScale = baseScale;
        }

        /// <summary>Side-to-side shake for a rejected tap.</summary>
        public static IEnumerator Shake(Transform tr, float amount = 0.12f, float duration = 0.3f)
        {
            Vector3 basePos = tr.localPosition;
            yield return Run(duration, e =>
            {
                if (!tr) return;
                float k = Mathf.Sin(e * Mathf.PI * 6f) * amount * (1f - e);
                tr.localPosition = basePos + new Vector3(k, 0, 0);
            }, t => t);
            if (tr) tr.localPosition = basePos;
        }
    }
}
