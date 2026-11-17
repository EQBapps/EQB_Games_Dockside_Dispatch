using System.Collections.Generic;
using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// Periodic fly-bys: every so often a small flock of gulls crosses the sky in a loose V, flapping as it goes.
    /// Birds are flat camera-facing "V" meshes; flapping squashes each wing pair vertically.
    /// </summary>
    public class BirdFlocks : MonoBehaviour
    {
        private Material _mat;
        private Mesh _bird;
        private Vector3 _up, _fwd;
        private Quaternion _facing;
        private float _horizonV, _skyTopV, _halfW, _meanInterval, _next;
        private readonly List<Flock> _flocks = new List<Flock>();

        private class Flock
        {
            public Transform root;
            public readonly List<(Transform bird, float phase, float rate, float scale)> birds = new List<(Transform, float, float, float)>();
            public float speed, dir, endX;
        }

        public void Init(Color color, Vector3 up, Vector3 fwd, float horizonV, float skyTopV, float halfW, float meanInterval)
        {
            _mat = Shapes.Mat(color, true);
            _up = up; _fwd = fwd; _facing = Quaternion.LookRotation(fwd, up);
            _horizonV = horizonV; _skyTopV = skyTopV; _halfW = halfW; _meanInterval = meanInterval;
            _bird = new FlatMesh()
                .Line(new Vector2(-0.13f, 0.05f), new Vector2(-0.03f, 0.0f), 0.028f)
                .Line(new Vector2(-0.035f, 0.0f), new Vector2(0.035f, 0.0f), 0.03f)
                .Line(new Vector2(0.03f, 0.0f), new Vector2(0.13f, 0.05f), 0.028f)
                .Build("Gull");
            _next = Time.time + Random.Range(3f, 8f);
        }

        private void Update()
        {
            if (_mat == null) return;
            if (Time.time >= _next)
            {
                Spawn();
                _next = Time.time + _meanInterval * Random.Range(0.65f, 1.5f);
            }
            for (int i = _flocks.Count - 1; i >= 0; i--)
            {
                var f = _flocks[i];
                if (!f.root) { _flocks.RemoveAt(i); continue; }
                f.root.position += _facing * Vector3.right * (f.dir * f.speed * Time.deltaTime);
                foreach (var (bird, phase, rate, scale) in f.birds)
                {
                    // Flap: wings swing from up to down and back; a short glide every few beats.
                    float s = Mathf.Sin(Time.time * rate + phase);
                    float glide = Mathf.Sin(Time.time * 0.7f + phase) > 0.6f ? 0.4f : 1f;
                    bird.localScale = new Vector3(scale, scale * Mathf.Lerp(0.85f, 0.85f * s - 0.15f, glide), scale);
                }
                if ((f.dir > 0 && f.root.position.x > f.endX) || (f.dir < 0 && f.root.position.x < f.endX))
                {
                    Destroy(f.root.gameObject);
                    _flocks.RemoveAt(i);
                }
            }
        }

        private void Spawn()
        {
            float dir = Random.value < 0.5f ? 1f : -1f;
            float v = _horizonV + Random.Range(0.9f, Mathf.Max(1.2f, _skyTopV - _horizonV - 0.8f));
            float depth = Random.Range(63f, 69f);
            float startX = -dir * (_halfW + 0.6f);
            var f = new Flock { speed = Random.Range(0.75f, 1.25f), dir = dir, endX = dir * (_halfW + 1.2f) };
            f.root = new GameObject("Flock").transform;
            f.root.SetParent(transform, false);
            f.root.position = new Vector3(startX, 0, 0) + _up * v + _fwd * depth;
            f.root.rotation = _facing;
            int n = Random.Range(3, 7);
            float scale = Random.Range(0.8f, 1.15f) * Mathf.Lerp(1.1f, 0.85f, (depth - 63f) / 6f);
            for (int i = 0; i < n; i++)
            {
                // Loose V: the leader in front, the others staggered behind on alternating sides.
                int rank = (i + 1) / 2, side = i % 2 == 0 ? 1 : -1;
                var offset = new Vector3(-dir * rank * 0.28f, side * rank * 0.13f + Random.Range(-0.03f, 0.03f), 0) * scale;
                var bird = Shapes.MakeWith("Gull", _bird, _mat, f.root, offset, Vector3.one, false).transform;
                bird.localScale = Vector3.one * scale;
                f.birds.Add((bird, Random.Range(0f, 6.28f), Random.Range(9f, 12f), scale));
            }
            _flocks.Add(f);
        }
    }
}
