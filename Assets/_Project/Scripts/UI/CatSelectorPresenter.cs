using CatCourier.Audio;
using CatCourier.Core;
using CatCourier.Progression;
using UnityEngine;

namespace CatCourier.UI
{
    /// <summary>
    /// Shows the cat list and forwards selection intent to <see cref="CatBreedManager"/>.
    /// It never unlocks or persists anything itself.
    /// </summary>
    public sealed class CatSelectorPresenter : MonoBehaviour
    {
        [SerializeField] private CatBreedManager catBreeds;

        private void Awake()
        {
            catBreeds ??= FindObjectOfType<CatBreedManager>();
        }

        private void OnGUI()
        {
            if (HubTabs.Active != HubTab.Cats)
            {
                return;
            }

            catBreeds ??= FindObjectOfType<CatBreedManager>();
            HubLayout.BeginContent();
            var breeds = catBreeds?.Breeds;
            if (breeds == null || breeds.Count == 0)
            {
                GUILayout.Label("No cat breeds are registered yet.");
                HubLayout.EndContent();
                return;
            }

            foreach (var breed in breeds)
            {
                if (breed == null || string.IsNullOrEmpty(breed.id))
                {
                    continue;
                }

                var unlocked = catBreeds.IsUnlocked(breed.id);
                var selected = breed.id == catBreeds.SelectedId;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{breed.breedName}", GUILayout.Width(180f));
                GUILayout.Label($"Speed +{breed.speedBonus:0.0}  Jump +{breed.jumpBonus:0.0}  Coin x{breed.coinBonusMultiplier:0.0}", GUILayout.Width(280f));

                if (selected)
                {
                    GUILayout.Label("Selected", GUILayout.Width(90f));
                }
                else if (!unlocked)
                {
                    GUILayout.Label(catBreeds.LockReason(breed.id), GUILayout.Width(180f));
                }
                else if (GUILayout.Button("Select", GUILayout.Width(90f)))
                {
                    AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                    if (catBreeds.TrySelect(breed.id))
                    {
                        AudioManager.Instance?.PlaySfx(SfxId.Purchase);
                    }
                }

                GUILayout.EndHorizontal();
            }

            HubLayout.EndContent();
        }
    }
}
