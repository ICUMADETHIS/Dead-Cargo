using System.Collections.Generic;
using DeadCargo.AI;
using DeadCargo.Authority;
using UnityEditor;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>Inspect and drive the Authority's response to heat, and see what each of its pilots is doing.</summary>
    internal sealed class AuthorityTab : GodMenuTab
    {
        private static readonly string[] ActionLabels = { "Spawn Ship", "Withdraw All", "Despawn All" };
        private static readonly string[] ShieldLabels = { "Break Hunters' Shields" };

        private readonly List<PilotSkillDefinition> skills = new List<PilotSkillDefinition>();
        private string[] skillLabels = new string[0];
        private AuthorityResponse response;

        public override string Title => "Authority";
        public override GodMenuGroup Group => GodMenuGroup.Run;
        public override int Order => 30;

        public override bool IsAvailable => response != null;

        public override string Summary
        {
            get
            {
                if (response == null)
                    return null;

                string ships = $"{response.ActiveShipCount} / {response.CurrentResponse.MaxShips} ships";
                int leaving = response.WithdrawingShips.Count;
                return leaving > 0 ? $"{ships}  ·  {leaving} leaving" : ships;
            }
        }

        public override void OnUpdate()
        {
            response = GodMenuContext.IsPlaying ? AuthorityResponse.Active : null;

            // The skills the tiers send become the spawn buttons.
            if (response != null)
                response.GetPilotSkills(skills);
            else
                skills.Clear();

            if (skillLabels.Length != skills.Count)
                skillLabels = new string[skills.Count];
            for (int i = 0; i < skills.Count; ++i)
                skillLabels[i] = skills[i].DisplayName;
        }

        public override void OnGUI()
        {
            using (new EditorGUI.DisabledScope(response == null))
                DrawResponse(response);

            if (response != null)
                DrawShips(response);
        }

        private static void DrawResponse(AuthorityResponse response)
        {
            using (var section = new GodMenuGUI.SectionScope("Response"))
            {
                if (!section.Expanded)
                    return;

                if (response == null)
                {
                    GodMenuGUI.Field("Active Ships", "0");
                    return;
                }

                int active = response.ActiveShipCount;
                int limit = response.CurrentResponse.MaxShips;
                float fraction = limit > 0 ? (float)active / limit : 0f;

                GodMenuGUI.Bar(fraction, $"{active} / {limit} ships", GodMenuGUI.Negative);
                GodMenuGUI.Field("Reinforce Every", limit > 0 ? $"{response.CurrentResponse.SpawnInterval:0.#} s" : "n/a");

                response.SpawningEnabled = EditorGUILayout.Toggle("Auto Spawn", response.SpawningEnabled);

                switch (GodMenuGUI.ButtonRow(ActionLabels))
                {
                    case 0:
                        if (response.SpawnShip() == null)
                            Debug.LogWarning("Authority ship could not spawn. Check the prefabs on the Authority Response and that the player is in a vehicle.", response);
                        break;
                    case 1:
                        response.WithdrawAll();
                        break;
                    case 2:
                        response.DespawnAll();
                        break;
                }
            }
        }

        // Spawning by skill, a shield cheat for testing breaking off, then one row per ship, hunting first, then
        // withdrawing: its skill, what its pilot is doing, its shields, speed and range.
        private void DrawShips(AuthorityResponse response)
        {
            using (var section = new GodMenuGUI.SectionScope("Ships"))
            {
                if (!section.Expanded)
                    return;

                if (skillLabels.Length > 0)
                {
                    GodMenuGUI.Hint("Spawn one pilot of a skill:");
                    int clicked = GodMenuGUI.ButtonRow(skillLabels);
                    if (clicked >= 0 && response.SpawnShip(skills[clicked]) == null)
                        Debug.LogWarning("Authority ship could not spawn. Check the prefabs on the Authority Response and that the player is in a vehicle.", response);
                }

                if (response.ActiveShipCount == 0 && response.WithdrawingShips.Count == 0)
                {
                    GodMenuGUI.Hint("No Authority ships are out.");
                    return;
                }

                if (GodMenuGUI.ButtonRow(ShieldLabels) == 0)
                    BreakShields(response);

                Vehicle player = GodMenuContext.PlayerVehicle;
                int number = 0;
                DrawShipRows(response, response.ActiveShips, player, ref number);
                DrawShipRows(response, response.WithdrawingShips, player, ref number);
            }
        }

        private static void BreakShields(AuthorityResponse response)
        {
            IReadOnlyList<Vehicle> ships = response.ActiveShips;
            for (int i = 0; i < ships.Count; ++i)
            {
                PilotBrain pilot = response.PilotOf(ships[i]);
                if (pilot != null)
                    pilot.BreakShields();
            }
        }

        private static void DrawShipRows(AuthorityResponse response, IReadOnlyList<Vehicle> ships, Vehicle player, ref int number)
        {
            for (int i = 0; i < ships.Count; ++i)
            {
                Vehicle ship = ships[i];
                if (ship == null)
                    continue;

                PilotBrain pilot = response.PilotOf(ship);
                Rigidbody body = ship.CachedRigidbody;
                string shield = pilot != null && pilot.ShieldShare >= 0f ? $"  ·  shield {pilot.ShieldShare * 100f:0}%" : string.Empty;
                string speed = body != null ? $"  ·  {body.linearVelocity.magnitude:0} m/s" : string.Empty;
                string distance = player != null ? $"  ·  {Vector3.Distance(player.transform.position, ship.transform.position):N0} m" : string.Empty;
                string label = pilot != null ? $"{++number}. {ship.Label}  {pilot.SkillName}" : $"{++number}. {ship.Label}";
                GodMenuGUI.Field(label, Describe(pilot) + shield + speed + distance);
            }
        }

        private static string Describe(PilotBrain pilot)
        {
            if (pilot == null)
                return "kit pilot";

            string state;
            switch (pilot.State)
            {
                case PilotState.Engaging:
                    state = $"ENGAGING  {PilotStateNames.Of(pilot.Manoeuvre)}";
                    break;
                case PilotState.Holding:
                    state = $"HOLDING  {pilot.HoldTimeLeft:0} s";
                    break;
                default:
                    state = PilotStateNames.Of(pilot.State);
                    break;
            }

            if (pilot.IsHoldingFire)
                state += "  holding fire";
            if (pilot.IsAvoiding)
                state += "  avoiding";
            if (pilot.IsBoosting)
                state += "  boost";
            return state;
        }
    }
}
