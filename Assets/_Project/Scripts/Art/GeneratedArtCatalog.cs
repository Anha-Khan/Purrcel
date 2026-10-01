using System;
using CatCourier.Core;
using UnityEngine;

namespace CatCourier.Art
{
    [Serializable]
    public sealed class CatSkinArt
    {
        public string breedId;
        public Sprite[] actions = Array.Empty<Sprite>();
        public Sprite[] reactions = Array.Empty<Sprite>();
        public Sprite[] slideWallFinish = Array.Empty<Sprite>();
    }

    [Serializable]
    public sealed class PackageArt
    {
        public PackageType type;
        public Sprite[] frames = Array.Empty<Sprite>();
    }

    [Serializable]
    public sealed class DistrictArt
    {
        public DistrictId district;
        // Block-major, then day, sunset, dusk, night for each block.
        public Sprite[] blocksByTime = Array.Empty<Sprite>();
        public Sprite[] roadByTime = Array.Empty<Sprite>();
        public int blockCount;
    }

    [CreateAssetMenu(fileName = "GeneratedArtCatalog", menuName = "Purrcel/Generated Art Catalog")]
    public sealed class GeneratedArtCatalog : ScriptableObject
    {
        public static GeneratedArtCatalog Active { get; private set; }
        public void Activate() => Active = this;
        public CatSkinArt[] cats = Array.Empty<CatSkinArt>();
        public PackageArt[] packages = Array.Empty<PackageArt>();
        public DistrictArt[] districts = Array.Empty<DistrictArt>();
        public Sprite[] natureBlocksByTime = Array.Empty<Sprite>();
        public int natureBlockCount;
        public Sprite[] natureRoadByTime = Array.Empty<Sprite>();
        public Sprite[] oldNatureTransitionByTime = Array.Empty<Sprite>();
        public Sprite[] natureModernTransitionByTime = Array.Empty<Sprite>();
        public Sprite[] oldNatureRoadByTime = Array.Empty<Sprite>();
        public Sprite[] natureModernRoadByTime = Array.Empty<Sprite>();
        public Sprite[] oldTownCloseupByTime = Array.Empty<Sprite>();
        public Sprite[] modernTownCloseupByTime = Array.Empty<Sprite>();
        public Sprite[] clearSky = Array.Empty<Sprite>();
        public Sprite[] rainySky = Array.Empty<Sprite>();
        public Sprite[] cloudDay = Array.Empty<Sprite>();
        public Sprite[] rainFrames = Array.Empty<Sprite>();
        public Sprite[] windFrames = Array.Empty<Sprite>();
        public Sprite[] fogFrames = Array.Empty<Sprite>();
        public Sprite[] birdFrames = Array.Empty<Sprite>();
        public Sprite[] coinFrames = Array.Empty<Sprite>();
        public Sprite[] checkpointFrames = Array.Empty<Sprite>();
        public Sprite[] oldTownLaundryFrames = Array.Empty<Sprite>();
        public Sprite[] fountainFrames = Array.Empty<Sprite>();
        public Sprite[] natureGrassFrames = Array.Empty<Sprite>();
        public Sprite[] swallowFrames = Array.Empty<Sprite>();
        public Sprite[] oldTownPaverFrames = Array.Empty<Sprite>();
        public Sprite[] modernDroneFrames = Array.Empty<Sprite>();
        public Sprite[] modernAcFrames = Array.Empty<Sprite>();
        public Sprite[] naturePotFrames = Array.Empty<Sprite>();
        public Sprite[] harbourSeagullFrames = Array.Empty<Sprite>();
        public Sprite[] harbourPuddleFrames = Array.Empty<Sprite>();
        public Sprite[] hudIcons = Array.Empty<Sprite>();
        public Sprite[] packageIcons = Array.Empty<Sprite>();
        public Sprite[] upgradeIcons = Array.Empty<Sprite>();
        public Sprite[] selectionFrames = Array.Empty<Sprite>();
        public Sprite[] resultsIcons = Array.Empty<Sprite>();
        public Sprite[] premiumBenefits = Array.Empty<Sprite>();
        public Sprite premiumHero;

        public CatSkinArt Skin(string breedId)
        {
            foreach (var cat in cats)
                if (cat != null && cat.breedId == breedId) return cat;
            return cats.Length > 0 ? cats[0] : null;
        }

        public PackageArt Package(PackageType type)
        {
            foreach (var package in packages)
                if (package != null && package.type == type) return package;
            return packages.Length > 0 ? packages[0] : null;
        }

        public DistrictArt District(DistrictId district)
        {
            foreach (var set in districts)
                if (set != null && set.district == district) return set;
            return null;
        }

        public static Sprite Frame(Sprite[] frames, int index)
        {
            return frames != null && index >= 0 && index < frames.Length ? frames[index] : null;
        }
    }
}
