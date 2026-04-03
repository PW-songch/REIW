using AYellowpaper.SerializedCollections;
using UnityEngine;
using UnityEngine.UI.Extensions;

namespace REIW.Animations
{
    public class AnimationStateLoader : MonoBehaviour
    {
        [SerializeField, SerializedDictionary("Attraction", "State Prefabs")]
        private SerializedDictionary<EnumAttraction, GameObject[]> _statePrefabs;


        public T[] GetStates<T>(bool includeInactive = false) where T : Component
        {
            return GetComponentsInChildren<T>(includeInactive);
        }

        public void LoadStates(EnumAttraction attractionType)
        {
            if (_statePrefabs.IsNullOrEmpty() ||
                !_statePrefabs.TryGetValue(attractionType, out var prefabs) ||
                prefabs.IsNullOrEmpty())
                return;

            for (int i = 0; i < prefabs.Length; ++i)
            {
                if (!prefabs[i] || !prefabs[i].IsPrefab())
                    continue;

                var state = Instantiate(prefabs[i], transform);
                state.transform.localPosition = Vector3.zero;
                state.transform.localRotation = Quaternion.identity;
                state.transform.localScale = Vector3.one;
            }
        }
    }
}
