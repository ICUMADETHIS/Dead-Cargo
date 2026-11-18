namespace DeadCargo.AI
{
    /// <summary>Display names for pilot states and manoeuvres, shared by the debug overlay and the God Menu. Constant strings, so nothing is allocated.</summary>
    public static class PilotStateNames
    {
        public static string Of(PilotState state)
        {
            switch (state)
            {
                case PilotState.Engaging: return "ENGAGING";
                case PilotState.Pursuing: return "PURSUING";
                case PilotState.Withdrawing: return "WITHDRAWING";
                case PilotState.Regrouping: return "REGROUPING";
                default: return "HOLDING";
            }
        }

        public static string Of(DogfightManoeuvre manoeuvre)
        {
            return manoeuvre == DogfightManoeuvre.Attacking ? "attack run" : "break away";
        }
    }
}
