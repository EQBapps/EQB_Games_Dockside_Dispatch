using System.Collections.Generic;
using Dockside.Core;
using UnityEngine;

namespace Dockside
{
    /// <summary>One shipping container: corrugated body, colour symbol, plus lock / key / hidden markers.</summary>
    public class ContainerView : MonoBehaviour
    {
        public int Id { get; private set; }
        public int Length { get; private set; }

        private Palette _palette;
        private Transform _body;                 // scaled to the container size; children are in unit space
        private readonly List<Renderer> _tinted = new List<Renderer>();
        private readonly List<Color> _tintBase = new List<Color>();
        private GameObject _symbol, _lock, _key, _question;
        private int _color;
        private bool _revealed, _dim;
        private MaterialPropertyBlock _mpb;

        public static ContainerView Create(Transform parent, ContainerSpec spec, Palette palette)
        {
            var go = new GameObject($"Container_{spec.id}");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<ContainerView>();
            v.Build(spec, palette);
            return v;
        }

        private void Build(ContainerSpec spec, Palette palette)
        {
            Id = spec.id;
            Length = spec.length;
            _palette = palette;
            _mpb = new MaterialPropertyBlock();
            transform.localScale = BoardLayout.ContainerScale(spec.length);

            // Collider on the root for taps (unit box, scaled with the root).
            var col = gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(1.05f, 1.2f, 1.1f);

            _body = transform;
            var main = Shapes.Box("Body", _body, Vector3.zero, Vector3.one, Color.white);
            Track(main.GetComponent<Renderer>(), 1f);
            // Darker base rail and top rim give the chunky "extruded" toy look from the mockup.
            Track(Shapes.Box("Base", _body, new Vector3(0, -0.44f, 0), new Vector3(1.02f, 0.14f, 1.02f), Color.white).GetComponent<Renderer>(), 0.72f);
            Track(Shapes.Box("Rim", _body, new Vector3(0, 0.5f, 0), new Vector3(1.0f, 0.04f, 1.0f), Color.white).GetComponent<Renderer>(), 0.88f);
            // Corrugation ridges on the camera-facing side.
            int ridges = 5 * spec.length;
            for (int i = 0; i < ridges; i++)
            {
                float x = -0.45f + 0.9f * (i + 0.5f) / ridges;
                Track(Shapes.Box("Ridge", _body, new Vector3(x, -0.02f, -0.5f), new Vector3(0.035f / spec.length, 0.72f, 0.03f), Color.white, false, false)
                    .GetComponent<Renderer>(), 0.8f);
            }

            float symbolSize = 0.42f;
            var symbolScale = new Vector3(1f / transform.localScale.x, 1f / transform.localScale.y, 1f / transform.localScale.z);
            _symbol = new GameObject("SymbolRoot");
            _symbol.transform.SetParent(_body, false);
            _symbol.transform.localPosition = new Vector3(0, 0.53f, 0);
            _symbol.transform.localScale = symbolScale;
            Shapes.Symbol(spec.color, _symbol.transform, Vector3.zero, symbolSize, palette.symbol);

            if (spec.kind == ContainerKind.Locked)
            {
                _lock = new GameObject("Lock");
                _lock.transform.SetParent(_body, false);
                _lock.transform.localPosition = new Vector3(0, 0.62f, 0);
                _lock.transform.localScale = symbolScale;
                Shapes.Box("LockBody", _lock.transform, new Vector3(0, 0.08f, 0), new Vector3(0.34f, 0.24f, 0.2f), Palette.Hex("3B3F46"));
                Shapes.Box("ShackleL", _lock.transform, new Vector3(-0.1f, 0.28f, 0), new Vector3(0.05f, 0.2f, 0.06f), Palette.Hex("C9CDD3"));
                Shapes.Box("ShackleR", _lock.transform, new Vector3(0.1f, 0.28f, 0), new Vector3(0.05f, 0.2f, 0.06f), Palette.Hex("C9CDD3"));
                Shapes.Box("ShackleT", _lock.transform, new Vector3(0, 0.37f, 0), new Vector3(0.25f, 0.05f, 0.06f), Palette.Hex("C9CDD3"));
            }
            if (spec.keyId >= 0)
            {
                _key = new GameObject("Key");
                _key.transform.SetParent(_body, false);
                _key.transform.localPosition = new Vector3(0.28f / spec.length, 0.56f, 0.2f);
                _key.transform.localScale = symbolScale;
                Shapes.Cylinder("KeyRing", _key.transform, new Vector3(-0.12f, 0.03f, 0), new Vector3(0.2f, 0.06f, 0.2f), palette.keyGold, false, false);
                Shapes.Box("KeyShaft", _key.transform, new Vector3(0.06f, 0.03f, 0), new Vector3(0.24f, 0.05f, 0.06f), palette.keyGold, false, false);
                Shapes.Box("KeyTooth", _key.transform, new Vector3(0.15f, 0.03f, -0.05f), new Vector3(0.05f, 0.05f, 0.08f), palette.keyGold, false, false);
            }
            if (spec.kind == ContainerKind.Hidden)
            {
                _question = new GameObject("Question");
                _question.transform.SetParent(_body, false);
                _question.transform.localPosition = new Vector3(0, 0.53f, 0);
                _question.transform.localRotation = Quaternion.Euler(90, 0, 0);
                _question.transform.localScale = symbolScale;
                var tm = _question.AddComponent<TextMesh>();
                tm.text = "?";
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.characterSize = 0.09f;
                tm.fontSize = 64;
                tm.fontStyle = FontStyle.Bold;
                tm.color = Color.white;
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                tm.font = font;
                _question.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }

            _color = spec.color;
            _revealed = spec.kind != ContainerKind.Hidden;
            ApplyLook();
        }

        private void Track(Renderer r, float shade)
        {
            _tinted.Add(r);
            _tintBase.Add(new Color(shade, shade, shade, 1f));
        }

        public void SetColor(int color)
        {
            _color = color;
            if (_symbol)
            {
                foreach (Transform child in _symbol.transform) Destroy(child.gameObject);
                Shapes.Symbol(color, _symbol.transform, Vector3.zero, 0.42f, _palette.symbol);
            }
            ApplyLook();
        }

        public void SetRevealed(bool revealed) { _revealed = revealed; ApplyLook(); }
        public void SetDim(bool dim) { if (_dim != dim) { _dim = dim; ApplyLook(); } }
        public void SetLocked(bool locked) { if (_lock) _lock.SetActive(locked); }
        public void HideKey() { if (_key) _key.SetActive(false); }

        private void ApplyLook()
        {
            Color baseColor = _revealed ? _palette.ContainerColor(_color) : _palette.hidden;
            if (_lock && _lock.activeSelf) baseColor = Color.Lerp(baseColor, _palette.locked, 0.65f);
            float dim = _dim ? 0.8f : 1f;
            for (int i = 0; i < _tinted.Count; i++)
            {
                var c = baseColor * _tintBase[i] * dim;
                c.a = 1f;
                _mpb.SetColor("_BaseColor", c);
                _mpb.SetColor("_Color", c);
                _tinted[i].SetPropertyBlock(_mpb);
            }
            if (_symbol) _symbol.SetActive(_revealed);
            if (_question) _question.SetActive(!_revealed);
        }

        /// <summary>Called once the container leaves the yard: no collider, no dimming.</summary>
        public void Detach()
        {
            var col = GetComponent<Collider>();
            if (col) col.enabled = false;
            SetDim(false);
            HideKey();
        }
    }
}
