using System;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Routing
{
    [DisallowMultipleComponent]
    public sealed class RogueliteRunState : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingIngots = 6;

        public int Ingots { get; private set; }
        public int RouteDepth { get; private set; } = 1;
        public int CombatWinsSinceBoss { get; private set; }

        public event Action Changed;

        private void Awake()
        {
            Ingots = Mathf.Max(0, startingIngots);
            RouteDepth = 1;
            CombatWinsSinceBoss = 0;
        }

        public void AddIngots(int amount)
        {
            if (amount <= 0)
                return;
            Ingots += amount;
            Changed?.Invoke();
        }

        public bool TrySpendIngots(int amount)
        {
            if (amount <= 0)
                return true;
            if (Ingots < amount)
                return false;

            Ingots -= amount;
            Changed?.Invoke();
            return true;
        }

        public void AdvanceRouteDepth()
        {
            RouteDepth++;
            Changed?.Invoke();
        }

        public void RecordCombatClear(bool boss)
        {
            CombatWinsSinceBoss = boss ? 0 : CombatWinsSinceBoss + 1;
            Changed?.Invoke();
        }
    }
}
