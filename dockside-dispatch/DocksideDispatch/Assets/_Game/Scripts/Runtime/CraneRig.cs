using System;
using System.Collections;
using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// The Crane Lift booster animation: a spreader on twin cables drops from above, grips the container,
    /// hoists it clear of the stack, carries it over and lowers it into place (ship deck or holding bay),
    /// then lets go and rises away.
    /// </summary>
    public class CraneRig : MonoBehaviour
    {
        private const float CableTop = 16f;        // the cables run up off-screen to the (implied) gantry
        private const float CarryHeight = 3.1f;    // high enough to clear the yard, the quay crane and the ships

        private Transform _spreader, _cableA, _cableB, _trolley;
        private Palette _pal;

        public static CraneRig Create(Transform parent, Palette pal)
        {
            var go = new GameObject("CraneRig");
            go.transform.SetParent(parent, false);
            var rig = go.AddComponent<CraneRig>();
            rig.Build(pal);
            go.SetActive(false);
            return rig;
        }

        private void Build(Palette pal)
        {
            _pal = pal;
            var dark = Palette.Hex("3A3133");
            _spreader = new GameObject("Spreader").transform;
            _spreader.SetParent(transform, false);
            // Yellow spreader frame with dark twist-lock corners and a headblock where the cables meet.
            Shapes.Box("Frame", _spreader, new Vector3(0, 0.06f, 0), new Vector3(1.08f, 0.08f, 0.22f), pal.crane);
            Shapes.Box("FrameEndL", _spreader, new Vector3(-0.5f, 0.06f, 0), new Vector3(0.1f, 0.08f, 0.9f), pal.crane);
            Shapes.Box("FrameEndR", _spreader, new Vector3(0.5f, 0.06f, 0), new Vector3(0.1f, 0.08f, 0.9f), pal.crane);
            foreach (var x in new[] { -0.5f, 0.5f })
                foreach (var z in new[] { -0.42f, 0.42f })
                    Shapes.Box("TwistLock", _spreader, new Vector3(x, 0.0f, z), new Vector3(0.12f, 0.06f, 0.12f), dark);
            Shapes.Box("Headblock", _spreader, new Vector3(0, 0.19f, 0), new Vector3(0.34f, 0.18f, 0.26f), pal.craneDark);
            Shapes.Box("Lamp", _spreader, new Vector3(0, 0.3f, -0.13f), new Vector3(0.08f, 0.05f, 0.02f), pal.lighthouseLamp, true, false);
            _cableA = Shapes.Box("CableA", transform, Vector3.zero, Vector3.one, dark, false, false).transform;
            _cableB = Shapes.Box("CableB", transform, Vector3.zero, Vector3.one, dark, false, false).transform;
            _trolley = Shapes.Box("Trolley", transform, Vector3.zero, new Vector3(0.6f, 0.2f, 0.4f), pal.craneDark, false, false).transform;
        }

        /// <summary>Sizes the spreader to the container it holds (short or long, yard or ship scale).</summary>
        private void FitSpreader(Transform container)
        {
            var cs = container.localScale;
            _spreader.localScale = new Vector3(cs.x, Mathf.Max(0.4f, cs.y / 0.7f), cs.z / 0.95f);
        }

        private void UpdateCables()
        {
            var top = _spreader.position + Vector3.up * 0.28f;
            float len = CableTop - top.y;
            foreach (var (cable, dx) in new[] { (_cableA, -0.09f), (_cableB, 0.09f) })
            {
                cable.position = new Vector3(top.x + dx, top.y + len * 0.5f, top.z);
                cable.localScale = new Vector3(0.035f, len, 0.035f);
            }
            _trolley.position = new Vector3(top.x, CableTop, top.z);
        }

        /// <summary>
        /// Carries a container from the yard to its destination. target() is re-read while lowering, so a bobbing ship
        /// is followed. The container ends exactly at the target with the given scale and rotation.
        /// </summary>
        public IEnumerator Carry(Transform container, Func<Vector3> target, Vector3 targetScale, Quaternion targetRotation)
        {
            gameObject.SetActive(true);
            Vector3 start = container.position;
            float halfH = container.localScale.y * 0.5f;
            Vector3 grip = start + Vector3.up * halfH;

            // 1. Drop in from above onto the container.
            _spreader.rotation = Quaternion.identity;
            FitSpreader(container);
            Vector3 high = new Vector3(grip.x, CarryHeight + 2.5f, grip.z);
            yield return Tween.Run(0.42f, e => { _spreader.position = Vector3.LerpUnclamped(high, grip, e); UpdateCables(); }, Tween.EaseOutCubic);

            // 2. Grip: a little settle, and the motor whirrs.
            Sfx.Play("crane", 0.9f);
            yield return Tween.Run(0.12f, e => { _spreader.position = grip + Vector3.down * Mathf.Sin(e * Mathf.PI) * 0.05f; UpdateCables(); });

            // 3. Hoist straight up, out of the stack (with a slight sway).
            Vector3 lifted = new Vector3(start.x, CarryHeight, start.z);
            yield return Tween.Run(0.5f, e =>
            {
                container.position = Vector3.LerpUnclamped(start, lifted, e);
                container.rotation = Quaternion.Euler(0, 0, Mathf.Sin(e * Mathf.PI * 2f) * 2.5f);
                _spreader.position = container.position + Vector3.up * halfH;
                UpdateCables();
            }, Tween.EaseInOutSine);

            // 4. Carry across to above the destination, easing the container to its final size.
            Vector3 fromScale = container.localScale;
            Vector3 above = target(); above.y = CarryHeight;
            yield return Tween.Run(0.6f, e =>
            {
                container.position = Vector3.LerpUnclamped(lifted, above, e);
                container.localScale = Vector3.LerpUnclamped(fromScale, targetScale, e);
                container.rotation = Quaternion.Slerp(Quaternion.Euler(0, 0, -3f * Mathf.Sin(e * Mathf.PI)), targetRotation, e);
                _spreader.position = container.position + Vector3.up * container.localScale.y * 0.5f;
                FitSpreader(container);
                UpdateCables();
            }, Tween.EaseInOutSine);

            // 5. Lower onto the deck or into the bay.
            yield return Tween.Run(0.4f, e =>
            {
                var dest = target();
                container.position = Vector3.LerpUnclamped(new Vector3(dest.x, CarryHeight, dest.z), dest, e);
                container.rotation = targetRotation;
                _spreader.position = container.position + Vector3.up * container.localScale.y * 0.5f;
                UpdateCables();
            }, Tween.EaseOutCubic);
            Sfx.Play("land");

            // 6. Release and rise away (the game continues meanwhile).
            StartCoroutine(RiseAway());
        }

        private IEnumerator RiseAway()
        {
            Vector3 from = _spreader.position, to = from + Vector3.up * 5f;
            yield return Tween.Run(0.45f, e => { _spreader.position = Vector3.LerpUnclamped(from, to, e); UpdateCables(); }, Tween.EaseInCubic);
            gameObject.SetActive(false);
        }
    }
}
