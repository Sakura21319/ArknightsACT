using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Enemies;
using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    [DisallowMultipleComponent]
    public sealed class BillboardPresentation25D : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        private Vector3 _baseScale;
        private IPlayerLocomotion _playerMotor;
        private PrototypeEnemyCombatBrain25D _enemyBrain;

        public void Configure(Camera cameraValue) => targetCamera = cameraValue;

        private void Awake()
        {
            _baseScale = transform.localScale;
            _playerMotor = FindPlayerLocomotion();
            _enemyBrain = GetComponentInParent<PrototypeEnemyCombatBrain25D>();
            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (targetCamera != null)
                transform.rotation = targetCamera.transform.rotation;

            var sign = _playerMotor != null ? _playerMotor.FacingSign : (_enemyBrain != null ? _enemyBrain.FacingSign : 1);
            transform.localScale = new Vector3(Mathf.Abs(_baseScale.x) * sign, _baseScale.y, _baseScale.z);
        }

        private IPlayerLocomotion FindPlayerLocomotion()
        {
            var behaviours = GetComponentsInParent<MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerLocomotion locomotion)
                    return locomotion;
            }
            return null;
        }
    }
}
