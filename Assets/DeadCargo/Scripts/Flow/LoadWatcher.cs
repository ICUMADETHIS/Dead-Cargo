using UnityEngine;

namespace DeadCargo.Flow
{
    /// <summary>
    /// Ticks the load report once a frame, before every other script, so the report sees each frame's length and the
    /// measured work that ran in it. Lives for the whole game, created by the report.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [AddComponentMenu("")]
    internal sealed class LoadWatcher : MonoBehaviour
    {
        private void Update()
        {
            LoadReport.Tick(Time.unscaledDeltaTime);
        }
    }
}
