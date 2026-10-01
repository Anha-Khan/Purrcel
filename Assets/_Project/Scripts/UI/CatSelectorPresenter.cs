using CatCourier.Art;
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
            var scale = UiTheme.Scale;
            var width = HubLayout.ContentRect.width;

            HubLayout.BeginContent(HubTab.Cats);
            if (catBreeds != null && !string.IsNullOrEmpty(catBreeds.SelectionFallbackReason))
            {
                // A lapsed premium used to drop the cat's bonuses with nothing on screen
                // explaining why the numbers changed.
                GUILayout.Label(catBreeds.SelectionFallbackReason, HubSkin.Small, GUILayout.Height(24f * scale));
            }

            GUILayout.Label("YOUR CATS", HubSkin.Heading, GUILayout.Height(34f * scale));
            GUILayout.Space(6f * scale);

            var breeds = catBreeds?.Breeds;
            if (breeds == null || breeds.Count == 0)
            {
                GUILayout.Label("No cat breeds are registered yet.", HubSkin.Row);
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
                var fallback = breed.id == catBreeds.ActiveBreed?.id &&
                               !string.IsNullOrEmpty(catBreeds.SelectionFallbackReason);

                // Card rect comes FROM the layout, so the list scrolls. Drawing into an
                // absolute rect instead pins each card to the screen.
                var card = GUILayoutUtility.GetRect(width - 8f * scale, 76f * scale,
                    GUILayout.Width(width - 8f * scale));
                var face = selected ? UiTheme.Fade(HubSkin.Honey, 0.26f)
                    : fallback ? UiTheme.Fade(UiTheme.Muted, 0.16f)
                    : UiTheme.Fade(HubSkin.PaperEdge, 0.22f);
                UiTheme.DrawTexture(card, UiTheme.Solid(face));
                GeneratedUiSprite.Draw(
                    new Rect(card.x + 10f * scale, card.y + 8f * scale, 60f * scale, 60f * scale),
                    breed.idleSprite);

                var textX = card.x + 80f * scale;
                var actionWidth = 136f * scale;
                // The text column stops short of the action button, and the name is measured
                // rather than given a fixed box: "Long Haired Tuxedo" is far wider than the
                // 200px this used to allow, and the overflow wrapped to a second line the
                // row had no height for, so the name vanished entirely.
                var textWidth = Mathf.Max(
                    40f * scale,
                    card.xMax - actionWidth - 10f * scale - textX);

                var nameRect = new Rect(textX, card.y + 10f * scale, textWidth, 26f * scale);
                var stats = $"Speed +{breed.speedBonus:0.0}   Jump +{breed.jumpBonus:0.0}   " +
                            $"Coin x{breed.coinBonusMultiplier:0.0}";
                var statsRect = new Rect(textX, card.y + 38f * scale, textWidth, 24f * scale);

                // A breed name is one line by definition here; clip rather than wrap so it
                // can never push itself out of the card.
                UiTheme.LabelClipped(nameRect, breed.breedName, HubSkin.Heading, HubSkin.Ink);
                UiTheme.Label(statsRect, stats, HubSkin.Small, HubSkin.InkSoft);

                var action = new Rect(card.xMax - actionWidth, card.y + 20f * scale,
                    120f * scale, 38f * scale);
                if (selected)
                {
                    HubSkin.Chip(action, "IN USE", HubSkin.Leaf);
                }
                else if (!unlocked)
                {
                    UiTheme.Label(action, catBreeds.LockReason(breed.id), HubSkin.Small, HubSkin.InkSoft);
                }
                else if (fallback)
                {
                    HubSkin.Chip(action, "FALLBACK", UiTheme.Muted);
                }
                else if (HubSkin.Pill(action, "SELECT", UiTheme.Fade(HubSkin.Terracotta, 0.85f)))
                {
                    AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                    if (catBreeds.TrySelect(breed.id))
                    {
                        AudioManager.Instance?.PlaySfx(SfxId.Purchase);
                    }
                }

                GUILayout.Space(14f * scale);
            }

            HubLayout.EndContent();
        }
    }
}