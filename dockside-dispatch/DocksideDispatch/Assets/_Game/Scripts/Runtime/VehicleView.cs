using System.Collections;
using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// A vehicle that takes containers in one colour: a ship at a berth (Load) or a truck at a loading bay (Unload).
    /// The game controller only talks to vehicles through this class, so both modes share the same playback code.
    /// </summary>
    public abstract class VehicleView : MonoBehaviour
    {
        public int ShipIndex { get; protected set; }
        public int Capacity { get; protected set; }
        public int Color { get; protected set; }
        public int Loaded { get; protected set; }

        /// <summary>Scale applied to a container stowed on this vehicle (relative to its yard size).</summary>
        public abstract float StowScale { get; }

        /// <summary>Updates the load counter; called as containers land.</summary>
        public abstract void SetLoaded(int slots);

        /// <summary>Local position for an item starting at slot index, spanning length slots.</summary>
        public abstract Vector3 SlotLocalPosition(int slot, int length);

        public Vector3 SlotWorldPosition(int slot, int length) => transform.TransformPoint(SlotLocalPosition(slot, length));

        /// <summary>Comes in from the left and stops at pos.</summary>
        public abstract IEnumerator Arrive(Vector3 pos, float duration);

        /// <summary>Leaves to the right, full.</summary>
        public abstract IEnumerator Depart(float duration);

        /// <summary>Sound played as it leaves (a ship's horn, a truck's horn).</summary>
        public virtual string DepartSound => "horn";
    }
}
