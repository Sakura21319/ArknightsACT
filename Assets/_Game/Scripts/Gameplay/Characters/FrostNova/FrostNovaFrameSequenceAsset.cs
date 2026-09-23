using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.FrostNova
{
    [CreateAssetMenu(menuName = "ArknightsACT/FrostNova/Frame Sequence")]
    public sealed class FrostNovaFrameSequenceAsset : ScriptableObject
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(1f)] private float framesPerSecond = 30f;

        public Sprite[] Frames => frames;
        public float FramesPerSecond => Mathf.Max(1f, framesPerSecond);
        public int FrameCount => frames != null ? frames.Length : 0;

        public void Configure(Sprite[] sprites, float fps)
        {
            frames = sprites;
            framesPerSecond = Mathf.Max(1f, fps);
        }
    }
}
