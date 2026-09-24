using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Rideable wheelchair placed in utility courts. Mounting is owned by
    /// <see cref="CityFacilityController"/> (single G-key interaction owner per stage);
    /// while occupied, a player-side locomotion component drives the rider's
    /// CharacterController and calls <see cref="SyncPose"/> every frame so the
    /// chair stays under the rider. The chair itself has no collider: interaction
    /// is distance based and the rider's CharacterController resolves obstacles.
    /// </summary>
    public sealed class Wheelchair25D : MonoBehaviour
    {
        public bool Occupied { get; private set; }
        public Transform Rider { get; private set; }

        /// <summary>Side position a rider stands at after dismounting.</summary>
        public Vector3 DismountPosition => transform.position + transform.right * .85f;

        public void SetRider(Transform rider)
        {
            Rider = rider;
            Occupied = rider != null;
        }

        /// <summary>Called by the rider-side locomotion while seated.</summary>
        public void SyncPose(Vector3 riderPosition, Vector3 heading)
        {
            if (!Occupied) return;
            transform.position = new Vector3(riderPosition.x, riderPosition.y, riderPosition.z);
            heading.y = 0f;
            if (heading.sqrMagnitude > .001f)
            {
                var target = Quaternion.LookRotation(heading.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, .35f);
            }
        }

        /// <summary>Builds the box-model wheelchair visual under <paramref name="parent"/>.</summary>
        public static Wheelchair25D Create(Transform parent, Vector3 localPosition, Material frame, Material seat, Material accent)
        {
            var root = new GameObject("Wheelchair");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;

            // Big rear wheels (flattened boxes read as wheels at prototype fidelity).
            foreach (var side in new[] { -1f, 1f })
            {
                Box(root.transform, "RearWheel", new Vector3(side * .34f, .31f, -.12f), new Vector3(.07f, .62f, .62f), .05f, frame, false);
                Box(root.transform, "RearWheelHub", new Vector3(side * .36f, .31f, -.12f), new Vector3(.05f, .16f, .16f), .02f, accent, false);
                Box(root.transform, "Caster", new Vector3(side * .26f, .09f, .34f), new Vector3(.09f, .18f, .18f), .03f, frame, false);
                Box(root.transform, "CasterFork", new Vector3(side * .26f, .25f, .3f), new Vector3(.05f, .2f, .05f), .01f, frame, false, Quaternion.Euler(-18f, 0f, 0f));
                Box(root.transform, "Armrest", new Vector3(side * .3f, .68f, .02f), new Vector3(.07f, .06f, .44f), .015f, seat, false);
                Box(root.transform, "PushHandle", new Vector3(side * .24f, 1.02f, -.26f), new Vector3(.06f, .18f, .06f), .015f, frame, false, Quaternion.Euler(20f, 0f, 0f));
                Box(root.transform, "Footrest", new Vector3(side * .14f, .12f, .48f), new Vector3(.2f, .04f, .16f), .01f, frame, false);
            }
            Box(root.transform, "Seat", new Vector3(0f, .5f, 0f), new Vector3(.56f, .09f, .48f), .02f, seat, false);
            Box(root.transform, "Backrest", new Vector3(0f, .82f, -.24f), new Vector3(.54f, .56f, .08f), .02f, seat, false, Quaternion.Euler(-6f, 0f, 0f));
            Box(root.transform, "SeatFrame", new Vector3(0f, .4f, 0f), new Vector3(.5f, .18f, .42f), .02f, frame, false);

            return root.AddComponent<Wheelchair25D>();
        }

        private static void Box(Transform parent, string name, Vector3 localPosition, Vector3 size, float bevel,
            Material material, bool collider, Quaternion? localRotation = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = ChernobogBeveledMeshFactory.GetBox(size,
                Mathf.Min(bevel, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * .22f));
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (collider)
            {
                var box = go.AddComponent<BoxCollider>();
                box.size = size;
            }
        }
    }
}
