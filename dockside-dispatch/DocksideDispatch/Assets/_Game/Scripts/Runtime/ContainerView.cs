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
        private Renderer _bodyRenderer;
        private Texture2D _texture;
        private GameObject _symbol, _lock, _key, _question;
        private int _color;
        private bool _revealed, _dim;

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
            transform.localScale = BoardLayout.ContainerScale(spec.length);

            // Collider on the root for taps (unit box, scaled with the root).
            var col = gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(1.05f, 1.2f, 1.1f);

            _body = transform;
            // One textured box: frame, corner castings, corrugation and doors all come from ContainerArt.
            _texture = ContainerArt.Texture(spec.length);
            _bodyRenderer = Shapes.MakeWith("Body", ContainerArt.Mesh(spec.length), Shapes.Mat(Color.white), _body, Vector3.zero, Vector3.one)
                .GetComponent<Renderer>();

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
            // Shared, cached materials (no property blocks) so the SRP Batcher can batch every container.
            var c = _dim ? Color.Lerp(baseColor * 0.78f, Palette.Hex("6E5D57"), 0.12f) : baseColor;
            c.a = 1f;
            _bodyRenderer.sharedMaterial = Shapes.MatTex(c, _texture);
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
