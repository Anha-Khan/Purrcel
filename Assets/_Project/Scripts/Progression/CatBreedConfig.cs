using UnityEngine;

namespace CatCourier.Progression
{
    [CreateAssetMenu(fileName = "CatBreed", menuName = "Purrcel/Cat Breed Config")]
    public sealed class CatBreedConfig : ScriptableObject
    {
        public string id;
        public string breedName;
        [TextArea] public string description;
        public Sprite idleSprite;
        public RuntimeAnimatorController animatorController;
        public float speedBonus;
        public float jumpBonus;
        public float coinBonusMultiplier = 1f;
        public bool isPremium;
        public bool isIAP;

        /// <summary>
        /// RevenueCat entitlement ID that grants this breed when <see cref="isIAP"/> is set, e.g.
        /// "rare_breeds_pack" or "legendary_cats_pack". An IAP breed with no pack ID stays locked
        /// and reads as unfinished paid content.
        /// </summary>
        public string iapPackId;
    }
}
