using UnityEngine;

namespace CatCourier.Progression
{
    [CreateAssetMenu(fileName = "Upgrade", menuName = "Purrcel/Upgrade Config")]
    public sealed class UpgradeConfig : ScriptableObject
    {
        public string id;
        public string upgradeName;
        [TextArea] public string description;
        public Sprite icon;
        public int[] costs = System.Array.Empty<int>();
        public float[] values = System.Array.Empty<float>();
        public int maxLevel;

        private void OnValidate()
        {
            maxLevel = System.Math.Max(0, maxLevel);
            if (costs != null && costs.Length > maxLevel)
            {
                System.Array.Resize(ref costs, maxLevel);
            }

            if (values != null && values.Length < maxLevel + 1)
            {
                System.Array.Resize(ref values, maxLevel + 1);
            }
        }
    }
}
