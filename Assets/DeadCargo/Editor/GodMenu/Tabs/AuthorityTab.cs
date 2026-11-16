using DeadCargo.Authority;
using UnityEditor;
using UnityEngine;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>Inspect and drive the Authority's response to heat.</summary>
    internal sealed class AuthorityTab : GodMenuTab
    {
        private static readonly string[] ActionLabels = { "Spawn Ship", "Despawn All" };

        public override string Title => "Authority";
        public override int Order => 35;

        public override void OnGUI()
        {
            AuthorityResponse response = GodMenuContext.IsPlaying
                ? Object.FindFirstObjectByType<AuthorityResponse>()
                : null;

            if (GodMenuContext.IsPlaying && response == null)
                EditorGUILayout.HelpBox("No Authority response in the scene. Run the Phase 4 setup from the Project tab.", MessageType.Warning);

            using (new EditorGUI.DisabledScope(response == null))
                DrawResponse(response);
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
                        response.DespawnAll();
                        break;
                }
            }
        }
    }
}
