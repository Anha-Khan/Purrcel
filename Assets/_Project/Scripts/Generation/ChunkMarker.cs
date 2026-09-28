using UnityEngine;
using CatCourier.Core;

namespace CatCourier.Generation
{
    [DisallowMultipleComponent]
    public sealed class ChunkMarker : MonoBehaviour
    {
        [SerializeField] private Transform start;
        [SerializeField] private Transform end;
        [SerializeField] private ChunkType type;

        public Transform Start => start;
        public Transform End => end;
        public ChunkType Type => type;

        private void OnValidate()
        {
            if (start != null)
            {
                var position = start.localPosition;
                position.x = 0f;
                start.localPosition = position;
            }

            if (end != null)
            {
                var position = end.localPosition;
                position.x = Constants.CHUNK_WIDTH;
                end.localPosition = position;
            }
        }
    }
}
