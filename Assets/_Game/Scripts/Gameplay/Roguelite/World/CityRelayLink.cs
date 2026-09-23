using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    public sealed class CityRelayLink : MonoBehaviour
    {
        private CityFacility25D _a, _b, _first;
        private CityFacilityController _controller;
        public float Deadline { get; private set; }
        public void Configure(CityFacility25D a, CityFacility25D b) { _a = a; _b = b; }
        public bool Activate(CityFacility25D endpoint, CityFacilityController controller, float now)
        {
            if (controller == null || !controller.IsAuthority || _a == null || _b == null || (endpoint != _a && endpoint != _b)) return false;
            Advance(now);
            if (endpoint.State != CityFacilityState.Ready || _a.Used || _b.Used) return false;
            _controller = controller;
            if (_first == null)
            {
                _first = endpoint; Deadline = now + 12f;
                endpoint.ChangeState(CityFacilityState.Armed, controller); return true;
            }
            _a.ChangeState(CityFacilityState.Spent, controller); _b.ChangeState(CityFacilityState.Spent, controller);
            _a.UnlockCache(); _b.UnlockCache(); controller.SurveyAround(endpoint.BlockIndex);
            _first = null; return true;
        }
        public void Advance(float now)
        {
            if (_first == null || now < Deadline || _controller == null || !_controller.IsAuthority) return;
            _first.ChangeState(CityFacilityState.Ready, _controller); _first = null;
        }
    }
}
