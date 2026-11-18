using UnityEngine;

namespace DeadCargo.AI
{
    /// <summary>A named pilot skill, such as Rookie, Regular or Veteran. The Authority sends one per heat tier.</summary>
    [CreateAssetMenu(fileName = "Pilot", menuName = "Dead Cargo/Pilot Skill")]
    public sealed class PilotSkillDefinition : ScriptableObject
    {
        [Tooltip("Name shown in readouts.")]
        [SerializeField] private string displayName = "Regular";

        [SerializeField] private PilotSkill skill = new PilotSkill();

        public string DisplayName => displayName;
        public PilotSkill Skill => skill;

        /// <summary>Sets the name and skill. For setup tools creating the presets.</summary>
        public void Configure(string name, PilotSkill preset)
        {
            displayName = name;
            skill = preset;
        }
    }
}
