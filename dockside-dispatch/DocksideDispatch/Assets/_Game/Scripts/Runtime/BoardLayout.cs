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
        public const float ContentHalfWidth = 4.7f;  // what must stay on screen horizontally (ships incl. bows)

        public readonly int Width, Height, Berths;

        /// <summary>
        /// The yard is scaled up so it spans most of the screen width (as in the art direction), capped so tall
        /// yards still fit above the booster bar on a 9:19.5 phone without the camera zooming out.
        /// </summary>
        public readonly float YardScale;
        public float CellSize => Cell * YardScale;

        public const float MaxYardScale = 1.38f;
        private const float YardWidthBudget = ContentHalfWidth * 2f - 0.55f;   // leave room for the kerbs
        private const float YardDepthBudget = 8.7f;                            // world z from row 0 to the yard's bottom edge

        public BoardLayout(int width, int height, int berths)
        {
            Width = width; Height = height; Berths = berths;
            float byWidth = YardWidthBudget / (Mathf.Max(1, width) * Cell);
            float byDepth = YardDepthBudget / ((Mathf.Max(1, height) - 1) * Cell + 0.6f);
            YardScale = Mathf.Clamp(Mathf.Min(byWidth, byDepth, MaxYardScale), 1f, MaxYardScale);
        }

        public float YardBottomZ => -(Height - 1) * CellSize - 0.6f * YardScale;

        /// <summary>Scale of a container sitting in the yard.</summary>
        public Vector3 YardContainerScale(int length) => ContainerScale(length) * YardScale;

        /// <summary>Centre of a container whose left cell is (x, y), in world space, resting on the yard.</summary>
        public Vector3 CellCenter(int x, int y, int length)
        {
            float cx = (x + (length - 1) * 0.5f - (Width - 1) * 0.5f) * CellSize;
            return new Vector3(cx, ContainerSize.y * YardScale * 0.5f, -y * CellSize);
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
            // Top: the horizon plus a band of sunset sky (anything spare above that also goes to the sky).
            float vMax = Vector3.Dot(new Vector3(0, EnvironmentBuilder.WaterY, EnvironmentBuilder.WaterEdgeZ), up) + 1.4f;
            float usable = 1f - topMargin - bottomMargin;
            float sizeForHeight = (vMax - vMin) / (2f * usable);
            float sizeForWidth = ContentHalfWidth / Mathf.Max(0.1f, cam.aspect);
            float size = Mathf.Max(sizeForHeight, sizeForWidth);
            cam.orthographicSize = size;

            // Anchor the board just above the bottom HUD bar; any spare height goes to the sunset sky above.
            float vCenter = vMin + size - 2f * size * bottomMargin;
            cam.transform.position = up * vCenter - fwd * 40f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 120f;
        }
    }
}
