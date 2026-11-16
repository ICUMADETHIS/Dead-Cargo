using UnityEngine;

namespace DeadCargo.Flow
{
    /// <summary>
    /// The scenes the game moves between. Lives in a Resources folder so the scene flow can find it from any scene.
    /// Paths are project paths, such as Assets/DeadCargo/Scenes/Mothership.unity.
    /// </summary>
    [CreateAssetMenu(fileName = ResourceName, menuName = "Dead Cargo/Scene Flow Settings")]
    public sealed class SceneFlowSettings : ScriptableObject
    {
        public const string ResourceName = "SceneFlowSettings";

        [Tooltip("The hub scene the game starts in and returns to after a run.")]
        [SerializeField] private string mothershipScene;

        [Tooltip("The sector scene a run launches into.")]
        [SerializeField] private string sectorScene;

        public string MothershipScene => mothershipScene;
        public string SectorScene => sectorScene;
    }
}
