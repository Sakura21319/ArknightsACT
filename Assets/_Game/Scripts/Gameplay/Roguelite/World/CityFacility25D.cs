using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Roguelite.Routing;
using ArknightsACT.Gameplay.Roguelite.Treasure;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    public enum CityFacilityKind { Medical, Relay, Power, Overload, PressureVent, RelaySwitch, ChargeStation, WaterStation, ScrapCache }
    public enum CityFacilityState { Ready, Armed, Spent }
    [System.Serializable]
    public struct CityFacilitySnapshot
    {
        public string Id;
        public CityFacilityKind Kind;
        public CityFacilityState State;
        public int Revision;
        public float RemainingSeconds;
    }

    public sealed class CityFacility25D : MonoBehaviour
    {
        public CityFacilityKind Kind { get; private set; }
        public int BlockIndex { get; private set; }
        public bool Used => State == CityFacilityState.Spent;
        public CityFacilityState State { get; private set; }
        public int Revision { get; private set; }
        public string StableId { get; internal set; }
        public CityRelayLink Link { get; set; }
        public GameObject WarningZone { get; set; }
        public float Deadline { get; private set; }
        public CityFacilitySnapshot CaptureState(float now) => new CityFacilitySnapshot
        {
            Id = StableId, Kind = Kind, State = State, Revision = Revision,
            RemainingSeconds = State == CityFacilityState.Armed ? Mathf.Max(0f, (Link != null ? Link.Deadline : Deadline) - now) : 0f
        };
        public float Duration => Kind == CityFacilityKind.Power || Kind == CityFacilityKind.Overload ? 4f : Kind == CityFacilityKind.Relay ? 2.5f : Kind == CityFacilityKind.ChargeStation ? 3f : Kind == CityFacilityKind.WaterStation ? 1.5f : 2f;
        public string Label => Kind switch
        {
            CityFacilityKind.Medical => "罗德岛应急补给",
            CityFacilityKind.Relay => "天灾观测中继",
            CityFacilityKind.Power => "源石配电柜",
            CityFacilityKind.Overload => "超载旁路",
            CityFacilityKind.PressureVent => "紧急泄压阀",
            CityFacilityKind.ChargeStation => "源石充能桩",
            CityFacilityKind.WaterStation => "应急净水点",
            CityFacilityKind.ScrapCache => "废料回收堆",
            _ => "双端接力开关"
        };
        public string Benefit => Kind switch
        {
            CityFacilityKind.Medical => "恢复 30% 最大生命 · 一次性",
            CityFacilityKind.Relay => "测绘周围街区 · 不会清除敌人",
            CityFacilityKind.Power => "恢复供电，解锁应急物资",
            CityFacilityKind.Overload => "代价：20% 最大生命；奖励：高级货箱（不会保底最高稀有度）",
            CityFacilityKind.PressureVent => "3 秒后半径 4m 喷汽，区内所有单位损失 15% 最大生命；随后开放物资",
            CityFacilityKind.ChargeStation => "两个技能各回复 15 点技力 · 一次性",
            CityFacilityKind.WaterStation => "恢复 12% 最大生命 · 一次性",
            CityFacilityKind.ScrapCache => "回收换得 4 源石锭 · 一次性",
            _ => "12 秒内接通另一端：档案库 + 周边测绘；超时复位，可重试"
        };
        public string ResultText => Kind switch
        {
            CityFacilityKind.Medical => "应急治疗完成 · 补给已耗尽",
            CityFacilityKind.Relay => "周边测绘完成",
            CityFacilityKind.PressureVent => "泄压预警！3 秒内离开黄色区域，喷汽会伤害所有单位",
            CityFacilityKind.RelaySwitch => Used ? "双端同步完成 · 档案库已开放" : "接力已启动 · 12 秒内到另一端操作",
            CityFacilityKind.ChargeStation => "充能完成 · 技能技力已补充",
            CityFacilityKind.WaterStation => "饮水补给完成 · 净水点已耗尽",
            CityFacilityKind.ScrapCache => "回收完成 · 源石锭 +4",
            _ => "储备已解锁 · 靠近按 F 搜索"
        };
        private CityFacilityController _controller;
        private SearchableContainer25D _cache;
        private Transform _housing;
        private Renderer _lamp;
        private Renderer _screen;
        public void Configure(CityFacilityKind kind, int block, SearchableContainer25D cache, Transform housing, Renderer lamp, Renderer screen)
        { Kind = kind; BlockIndex = block; _cache = cache; _housing = housing; _lamp = lamp; _screen = screen; }

        public bool CanUse(Health health) => State == CityFacilityState.Ready && health != null && !health.IsDead &&
            ((Kind != CityFacilityKind.Medical && Kind != CityFacilityKind.WaterStation) || health.CurrentHealth < health.MaxHealth - .01f) &&
            (Kind != CityFacilityKind.Overload || health.CurrentHealth > health.MaxHealth * .2f + .01f);

        public bool Activate(Health health, CityFacilityController controller)
        {
            if (!CanUse(health) || controller == null || !controller.IsAuthority ||
                ((Kind == CityFacilityKind.Power || Kind == CityFacilityKind.Overload || Kind == CityFacilityKind.PressureVent) && _cache == null)) return false;
            _controller = controller;
            if (Kind == CityFacilityKind.RelaySwitch) return Link != null && Link.Activate(this, controller, Time.time);
            if (Kind == CityFacilityKind.PressureVent)
            {
                Deadline = Time.time + 3f; ChangeState(CityFacilityState.Armed, controller);
                if (WarningZone != null) WarningZone.SetActive(true);
                return true;
            }
            ChangeState(CityFacilityState.Spent, controller);
            if (Kind == CityFacilityKind.Medical) health.Heal(health.MaxHealth * .3f);
            if (Kind == CityFacilityKind.WaterStation) health.Heal(health.MaxHealth * .12f);
            if (Kind == CityFacilityKind.Relay) controller.SurveyAround(BlockIndex);
            if (Kind == CityFacilityKind.Overload) health.TakeDamage(health.MaxHealth * .2f);
            if (Kind == CityFacilityKind.Power || Kind == CityFacilityKind.Overload) UnlockCache();
            if (Kind == CityFacilityKind.ChargeStation && controller.CurrentActor != null)
                controller.CurrentActor.GetComponent<PlayerSkillController>()?.GainAllSkillPoints(15f);
            if (Kind == CityFacilityKind.ScrapCache && RogueliteRunState.Instance != null)
                RogueliteRunState.Instance.AddIngots(4);
            return true;
        }
        public void Advance(float now)
        {
            if (Kind != CityFacilityKind.PressureVent || State != CityFacilityState.Armed || now < Deadline || _controller == null || !_controller.IsAuthority) return;
            ChangeState(CityFacilityState.Spent, _controller);
            if (WarningZone != null) WarningZone.SetActive(false);
            foreach (var health in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (health == null || !health.gameObject.activeInHierarchy || health.IsDead) continue;
                var delta = health.transform.position - transform.position;
                if (Mathf.Abs(delta.y) <= 3f && new Vector2(delta.x, delta.z).sqrMagnitude <= 16f) health.TakeDamage(health.MaxHealth * .15f);
            }
            UnlockCache();
        }
        internal void UnlockCache()
        {
            if (_housing != null) _housing.gameObject.SetActive(false);
            if (_cache != null) _cache.gameObject.SetActive(true);
        }
        internal void ChangeState(CityFacilityState state, CityFacilityController controller)
        {
            if (State == state) return;
            State = state; Revision++;
            var props = new MaterialPropertyBlock();
            var color = state == CityFacilityState.Spent ? new Color(.22f, .85f, .65f) : state == CityFacilityState.Armed ? new Color(1f, .25f, .05f) : new Color(.58f, .45f, .25f);
            props.SetColor("_Color", color); props.SetColor("_BaseColor", color); props.SetColor("_EmissionColor", color * .6f);
            if (_lamp != null) _lamp.SetPropertyBlock(props);
            if (_screen != null) _screen.SetPropertyBlock(props);
            controller?.NotifyStateChanged(this);
        }
    }
}
