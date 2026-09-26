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
    /// Visual: round spoked wheels with neon rims, glow strips, a safety flag and
    /// ground skid trails drawn from both rear wheels while rolling.
    /// </summary>
    public sealed class Wheelchair25D : MonoBehaviour
    {
        private const float RearWheelRadius = .31f;
        private const float VisualScale = 1.22f;
        private const float RiderBackOffset = .10f;
        private const float CameraDepthBackOffset = .16f;
        private const float MaxLeanDegrees = 14f;

        public bool Occupied { get; private set; }
        public Transform Rider { get; private set; }

        /// <summary>Side position a rider stands at after dismounting.</summary>
        public Vector3 DismountPosition => transform.position + transform.right * .85f;

        private Transform[] _spinningWheels;
        private TrailRenderer[] _trails;
        private float _wheelSpin;
        private float _lean;

        public void SetRider(Transform rider)
        {
            Rider = rider;
            Occupied = rider != null;
            if (!Occupied)
                SetTrailEmitting(false);
        }

        /// <summary>Called by the rider-side locomotion while seated.</summary>
        /// <param name="rollDistance">Planar distance travelled this frame; spins the wheels and drives the trails.</param>
        /// <param name="leanDegrees">Body roll into the current turn/slide (drift lean).</param>
        public void SyncPose(Vector3 riderPosition, Vector3 heading, float rollDistance, float leanDegrees)
        {
            if (!Occupied) return;
            heading.y = 0f;
            var planarHeading = heading.sqrMagnitude > .001f
                ? heading.normalized
                : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (planarHeading.sqrMagnitude < .001f)
                planarHeading = Vector3.forward;

            // Keep the larger chair slightly behind the rider root and also a little farther
            // from the gameplay camera. The latter is important in the 2.5D view: it prevents the
            // rear wheels/frame from depth-occluding the seated Spine body while preserving the
            // feeling that the character is inside the seat/backrest.
            var camera = Camera.main;
            var cameraDepthOffset = camera != null
                ? camera.transform.forward.normalized * CameraDepthBackOffset
                : Vector3.zero;
            var chairPosition = riderPosition - planarHeading * RiderBackOffset + cameraDepthOffset;
            transform.position = new Vector3(chairPosition.x, riderPosition.y, chairPosition.z);
            if (heading.sqrMagnitude > .001f)
            {
                _lean = Mathf.MoveTowards(_lean, Mathf.Clamp(leanDegrees, -MaxLeanDegrees, MaxLeanDegrees), 70f * Time.deltaTime);
                var target = Quaternion.LookRotation(planarHeading, Vector3.up) * Quaternion.Euler(0f, 0f, _lean);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, .3f);
            }
            if (Mathf.Abs(rollDistance) > .0001f && _spinningWheels != null)
            {
                _wheelSpin += rollDistance / (RearWheelRadius * VisualScale) * Mathf.Rad2Deg;
                var spin = Quaternion.Euler(_wheelSpin, 0f, 0f);
                foreach (var wheel in _spinningWheels)
                    if (wheel != null)
                        wheel.localRotation = spin;
            }
        }

        /// <summary>Rear-wheel ground trails; the locomotion enables them while rolling.</summary>
        public void SetTrailEmitting(bool emitting)
        {
            if (_trails == null) return;
            foreach (var trail in _trails)
                if (trail != null && trail.emitting != emitting)
                    trail.emitting = emitting;
        }

        /// <summary>Builds the wheelchair visual under <paramref name="parent"/>.</summary>
        public static Wheelchair25D Create(Transform parent, Vector3 localPosition, Material frame, Material seat, Material accent)
        {
            var root = new GameObject("Wheelchair");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            root.transform.localScale = Vector3.one * VisualScale;

            // Neon accent: cloned from the warm window material, boosted into a bright cyan glow.
            var neon = new Material(accent != null ? accent : frame) { name = "WheelchairNeon" };
            foreach (var property in new[] { "_BaseColor", "_Color" })
                if (neon.HasProperty(property)) neon.SetColor(property, new Color(.3f, .85f, .8f));
            neon.EnableKeyword("_EMISSION");
            if (neon.HasProperty("_EmissionColor")) neon.SetColor("_EmissionColor", new Color(.32f, 1.05f, .9f));

            var wheels = new System.Collections.Generic.List<Transform>();
            var trails = new System.Collections.Generic.List<TrailRenderer>();
            foreach (var side in new[] { -1f, 1f })
            {
                // Big rear wheel: round tire + glowing rim + hub + spokes, all on a spinning pivot.
                var pivot = new GameObject("RearWheelSpin").transform;
                pivot.SetParent(root.transform, false);
                pivot.localPosition = new Vector3(side * .34f, RearWheelRadius, -.12f);
                Disc(pivot, "Tire", .62f, .075f, frame);
                Disc(pivot, "NeonRim", .47f, .088f, neon);
                Disc(pivot, "Hub", .15f, .1f, accent);
                for (var spoke = 0; spoke < 3; spoke++)
                    Box(pivot, "Spoke", Vector3.zero, new Vector3(.02f, .54f, .045f), .008f, seat, false, Quaternion.Euler(spoke * 60f, 0f, 0f));
                wheels.Add(pivot);

                // Front caster: small solid wheel on a fork.
                Disc(root.transform, "Caster", .2f, .06f, frame, new Vector3(side * .26f, .1f, .34f));
                Box(root.transform, "CasterFork", new Vector3(side * .26f, .26f, .3f), new Vector3(.05f, .22f, .05f), .01f, frame, false, Quaternion.Euler(-18f, 0f, 0f));

                Box(root.transform, "Armrest", new Vector3(side * .3f, .68f, .02f), new Vector3(.07f, .06f, .44f), .015f, seat, false);
                Box(root.transform, "PushHandle", new Vector3(side * .24f, 1.02f, -.26f), new Vector3(.06f, .18f, .06f), .015f, frame, false, Quaternion.Euler(20f, 0f, 0f));
                Box(root.transform, "HandleGrip", new Vector3(side * .24f, 1.11f, -.29f), new Vector3(.065f, .05f, .065f), .015f, neon, false, Quaternion.Euler(20f, 0f, 0f));
                Box(root.transform, "Footrest", new Vector3(side * .14f, .12f, .48f), new Vector3(.2f, .04f, .16f), .01f, frame, false);
                // Glowing side strip along the seat frame.
                Box(root.transform, "NeonSideStrip", new Vector3(side * .285f, .47f, 0f), new Vector3(.025f, .05f, .46f), .008f, neon, false);

                // Ground trail under each rear wheel.
                var trailGo = new GameObject("WheelTrail");
                trailGo.transform.SetParent(root.transform, false);
                trailGo.transform.localPosition = new Vector3(side * .34f, .03f, -.12f);
                trails.Add(BuildTrail(trailGo));
            }
            Box(root.transform, "Seat", new Vector3(0f, .5f, 0f), new Vector3(.56f, .09f, .48f), .02f, seat, false);
            Box(root.transform, "Backrest", new Vector3(0f, .82f, -.24f), new Vector3(.54f, .56f, .08f), .02f, seat, false, Quaternion.Euler(-6f, 0f, 0f));
            Box(root.transform, "SeatFrame", new Vector3(0f, .4f, 0f), new Vector3(.5f, .18f, .42f), .02f, frame, false);
            // Underglow + backrest accent stripe.
            Box(root.transform, "Underglow", new Vector3(0f, .29f, 0f), new Vector3(.46f, .025f, .38f), .008f, neon, false);
            Box(root.transform, "BackrestStripe", new Vector3(0f, .86f, -.285f), new Vector3(.5f, .06f, .02f), .006f, neon, false, Quaternion.Euler(-6f, 0f, 0f));
            // Safety flag on a springy pole at the back-left.
            Box(root.transform, "FlagPole", new Vector3(-.24f, .95f, -.28f), new Vector3(.025f, 1.15f, .025f), .005f, frame, false, Quaternion.Euler(0f, 0f, 6f));
            Box(root.transform, "Flag", new Vector3(-.18f, 1.48f, -.28f), new Vector3(.26f, .16f, .02f), .005f, neon, false, Quaternion.Euler(0f, 0f, 6f));

            var chair = root.AddComponent<Wheelchair25D>();
            chair._spinningWheels = wheels.ToArray();
            chair._trails = trails.ToArray();
            return chair;
        }

        private static TrailRenderer BuildTrail(GameObject go)
        {
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 2.6f;
            trail.startWidth = .075f;
            trail.endWidth = .02f;
            trail.minVertexDistance = .07f;
            trail.emitting = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var material = new Material(shader != null ? shader : Shader.Find("Standard")) { name = "WheelchairTrail" };
            var color = new Color(1f, .72f, .3f, .6f);
            foreach (var property in new[] { "_BaseColor", "_Color", "_TintColor" })
                if (material.HasProperty(property)) material.SetColor(property, color);
            trail.sharedMaterial = material;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(.85f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            return trail;
        }

        /// <summary>Round wheel disc built from the builtin cylinder mesh (no collider).</summary>
        private static void Disc(Transform parent, string name, float diameter, float thickness, Material material, Vector3? localPosition = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Object.Destroy(collider);
                else Object.DestroyImmediate(collider);
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition ?? Vector3.zero;
            // Cylinder axis (local Y) is laid onto the parent's lateral X axis so the disc reads as a wheel.
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            go.transform.localScale = new Vector3(diameter, thickness * .5f, diameter);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
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
