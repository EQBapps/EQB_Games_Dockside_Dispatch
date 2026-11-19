using System.Collections.Generic;
using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// Unload-mode scenery: trucks passing along the yard road behind the terminal's container stacks, at the stacks'
    /// scale and hazed into the distance. Purely decorative: they have their own road, so the puzzle trucks in the lane
    /// in front never wait for them.
    /// </summary>
    public class YardTraffic : MonoBehaviour
    {
        /// <summary>Drawn at the same scale as the container stacks in front of the road.</summary>
        public const float Scale = 0.66f;
        private const float Speed = 1.9f, MinGap = 1.2f, Haze = 0.3f;
        private const int MaxTrucks = 3;
        private static readonly Color[] Cabs = { Palette.Hex("ECE7DD"), Palette.Hex("AEB4BC"), Palette.Hex("4E5562"), Palette.Hex("8C4F4A"), Palette.Hex("3F5F7A") };
        private static readonly Color[] Cargo = { Palette.Hex("B0605A"), Palette.Hex("5F7F96"), Palette.Hex("C2A266"), Palette.Hex("6F8E74"), Palette.Hex("9A948E"), Palette.Hex("D6CFC2") };

        private readonly List<Transform> _trucks = new List<Transform>();
        private float _halfW, _z, _next;
        private Palette _pal;
        private SkyTheme _theme;

        private float Length => TruckView.Length * Scale;
        private float StartX => -_halfW - Length * 0.5f;

        public void Init(float halfW, float z, Palette pal, SkyTheme theme)
        {
            _halfW = halfW;
            _z = z;
            _pal = pal;
            _theme = theme;
            // One already on its way, so the yard isn't empty when the level opens.
            Spawn(Random.Range(-halfW * 0.6f, halfW * 0.3f));
            _next = Time.time + Random.Range(2f, 5f);
        }

        private void Spawn(float x)
        {
            Color cab = Color.Lerp(Cabs[Random.Range(0, Cabs.Length)], _theme.haze, Haze);
            Color cargo = Color.Lerp(Cargo[Random.Range(0, Cargo.Length)], _theme.haze, Haze + 0.05f);
            var t = TruckView.CreateAmbient(transform, cab, cargo, Random.value < 0.5f ? 2 : 1, _pal, _theme).transform;
            t.localScale = Vector3.one * Scale;
            t.position = new Vector3(x, 0, _z);
            _trucks.Add(t);
        }

        private void Update()
        {
            // All trucks drive at the same speed, so keeping the gap at the entry keeps them apart all the way.
            var last = _trucks.Count > 0 ? _trucks[_trucks.Count - 1] : null;
            if (Time.time >= _next && _trucks.Count < MaxTrucks && (!last || last.position.x > StartX + Length + MinGap))
            {
                Spawn(StartX);
                _next = Time.time + Random.Range(4f, 9f);
            }
            for (int i = _trucks.Count - 1; i >= 0; i--)
            {
                var t = _trucks[i];
                if (!t) { _trucks.RemoveAt(i); continue; }
                var p = t.position;
                p.x += Speed * Time.deltaTime;
                t.position = p;
                if (p.x > _halfW + Length * 0.5f) { Destroy(t.gameObject); _trucks.RemoveAt(i); }
            }
        }
    }
}
