using System.Collections.Generic;
using UnityEngine;

namespace DeadCargo.Destruction
{
    /// <summary>
    /// Reuses effect prefabs that switch themselves off when done, such as the kit's explosions. An effect is played
    /// by turning an idle copy back on; new copies are made only when every copy is busy, up to a limit per prefab.
    /// </summary>
    internal sealed class EffectPool
    {
        private const int MaxCopiesPerPrefab = 12;

        private readonly Transform parent;
        private readonly Dictionary<GameObject, List<GameObject>> copies = new Dictionary<GameObject, List<GameObject>>();

        public EffectPool(Transform parent)
        {
            this.parent = parent;
        }

        public void Play(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
                return;

            if (!copies.TryGetValue(prefab, out List<GameObject> list))
            {
                list = new List<GameObject>();
                copies.Add(prefab, list);
            }

            for (int i = list.Count - 1; i >= 0; --i)
            {
                if (list[i] == null)
                {
                    list.RemoveAt(i);
                    continue;
                }

                if (list[i].activeSelf)
                    continue;

                list[i].transform.SetPositionAndRotation(position, rotation);
                list[i].SetActive(true);
                return;
            }

            if (list.Count < MaxCopiesPerPrefab)
                list.Add(Object.Instantiate(prefab, position, rotation, parent));
        }
    }
}
