namespace ArknightsACT.Gameplay.Roguelite.Routing
{
    public enum RogueliteRouteNodeType
    {
        Combat,
        EmergencyCombat,
        Encounter,
        SafeHouse,
        Trader,
        Boss
    }

    public readonly struct RogueliteRouteNodeChoice
    {
        public RogueliteRouteNodeChoice(RogueliteRouteNodeType type, string title, string description)
        {
            Type = type;
            Title = title;
            Description = description;
        }

        public RogueliteRouteNodeType Type { get; }
        public string Title { get; }
        public string Description { get; }
    }
}
