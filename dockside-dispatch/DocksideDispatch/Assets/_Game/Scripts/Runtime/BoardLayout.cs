using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// World layout. X = left/right, Z = towards the sea (up the screen), Y = up.
    /// Yard rows run from z = 0 (row 0, next to the quay) towards -z.
    /// </summary>
    public class BoardLayout
    {
        public const float Cell = 1.1f;
        public static readonly Vector3 ContainerSize = new Vector3(1.0f, 0.7f, 0.95f);
        public const float ShipScale = 0.4f;         // container scale when stowed on a ship
        public const float ShipSlotPitch = 0.42f;    // x distance between ship slots
        public const float QuayNearZ = 0.75f, QuayFarZ = 2.6f, BufferZ = 1.65f;
        public const float WaterNearZ = 2.6f, WaterFarZ = 7.6f, ShipZ = 4.5f;
        public const float BerthSpacing = 2.95f;
        public const float ContentHalfWidth = 4.45f; // what must stay on screen horizontally

        public readonly int Width, Height, Berths;

        public BoardLayout(int width, int height, int berths)
        {
            Width = width; Height = height; Berths = berths;
        }

        public float YardBottomZ => -(Height - 1) * Cell - 0.6f;

        /// <summary>Centre of a container whose left cell is (x, y), in world space, resting on the yard.</summary>
        public Vector3 CellCenter(int x, int y, int length)
        {
            float cx = (x + (length - 1) * 0.5f - (Width - 1) * 0.5f) * Cell;
            return new Vector3(cx, ContainerSize.y * 0.5f, -y * Cell);
        }

        public static Vector3 ContainerScale(int length) =>
            new Vector3(ContainerSize.x * length + (length - 1) * (Cell - ContainerSize.x), ContainerSize.y, ContainerSize.z);

        public float BerthX(int berth) => (berth - (Berths - 1) * 0.5f) * BerthSpacing;

        public static float BufferPitch(int capacity) => Mathf.Min(1.25f, 7.6f / Mathf.Max(1, capacity));

        /// <summary>Centre of a holding-bay item starting at slot index, spanning length slots.</summary>
        public static Vector3 BufferPosition(int slot, int length, int capacity)
        {
            float pitch = BufferPitch(capacity);
            float x = (slot + (length - 1) * 0.5f - (capacity - 1) * 0.5f) * pitch;
            return new Vector3(x, 0.12f + ContainerSize.y * 0.5f, BufferZ);
        }

        /// <summary>Scale for a container in the holding bay (shrinks slightly when the bay is extended).</summary>
        public static Vector3 BufferScale(int length, int capacity)
        {
            float k = Mathf.Min(1f, BufferPitch(capacity) / 1.25f);
            var s = ContainerScale(length);
            return new Vector3(s.x * k, s.y * k, s.z * k);
        }

        /// <summary>
        /// Fits an orthographic camera (pitched down by pitchDeg) so the whole board shows between the HUD bars.
        /// </summary>
        public void FitCamera(Camera cam, float pitchDeg, float topMargin, float bottomMargin)
        {
            cam.orthographic = true;
            cam.transform.rotation = Quaternion.Euler(pitchDeg, 0f, 0f);
            Vector3 up = cam.transform.up, fwd = cam.transform.forward;

            // Vertical screen coordinate of a world point = dot(point, up).
            float vMin = Vector3.Dot(new Vector3(0, 0, YardBottomZ - 0.4f), up);
            float vMax = Vector3.Dot(new Vector3(0, 2.2f, WaterFarZ + 1.2f), up);
            float usable = 1f - topMargin - bottomMargin;
            float sizeForHeight = (vMax - vMin) / (2f * usable);
            float sizeForWidth = ContentHalfWidth / Mathf.Max(0.1f, cam.aspect);
            float size = Mathf.Max(sizeForHeight, sizeForWidth);
            cam.orthographicSize = size;

            float contentCenter = (vMin + vMax) * 0.5f;
            float vCenter = contentCenter - size * (bottomMargin - topMargin);
            cam.transform.position = up * vCenter - fwd * 40f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 120f;
        }
    }
}
