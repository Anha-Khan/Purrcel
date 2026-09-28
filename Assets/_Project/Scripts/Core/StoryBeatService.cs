using System;
using System.Collections.Generic;
using CatCourier.Core;

namespace CatCourier.Core
{
    public static class StoryBeatService
    {
        private static readonly Dictionary<string, string> TextById = new()
        {
            ["oldtown_checkpoint_1"] = "The cat's name is Pip. She's been running deliveries since before the pigeons took over the square.",
            ["oldtown_checkpoint_3"] = "Old Marko at the café always leaves a window open. Pip never stops to rest — but she glances.",
            ["downtown_checkpoint_1"] = "The glass towers are cold. The drones don't like cats. Good thing the feeling is mutual.",
            ["downtown_checkpoint_3"] = "Pip finds a package addressed to no one. She delivers it anyway.",
            ["harbour_checkpoint_1"] = "Salt air. The seagulls remember when this was their territory. They haven't forgiven.",
            ["suburbs_checkpoint_1"] = "A dog. A very loud, very slow dog. Pip has met worse."
        };

        public static StoryBeat GetFirstTimeBeat(DistrictId district, int checkpointNumber)
        {
            var id = BuildId(district, checkpointNumber);
            if (!TextById.TryGetValue(id, out var text))
            {
                return default;
            }

            var save = SaveSystem.Instance;
            var data = save?.Data;
            if (data?.seenStoryBeatIds == null || data.seenStoryBeatIds.Contains(id))
            {
                return default;
            }

            data.seenStoryBeatIds.Add(id);
            save.Save();
            return new StoryBeat(id, text);
        }

        public static string BuildId(DistrictId district, int checkpointNumber)
        {
            var code = district switch
            {
                DistrictId.OldTown => "oldtown",
                DistrictId.Downtown => "downtown",
                DistrictId.Harbour => "harbour",
                DistrictId.Suburbs => "suburbs",
                _ => district.ToString().ToLowerInvariant()
            };
            return $"{code}_checkpoint_{Math.Max(1, checkpointNumber)}";
        }
    }
}
