using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CatCourier.Editor
{
    // Import settings stay with the PNGs when Unity creates their .meta files.
    public sealed class GeneratedArtImporter : AssetPostprocessor
    {
        public const string Root = "Assets/_Project/Art/Generated/";
        private static readonly Regex Grid = new Regex(@"_(\d)x(\d)(?:\.|_)", RegexOptions.Compiled);

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root, StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.spritePixelsPerUnit = assetPath.Contains("/PlayableCats/") ? 200f : 100f;
            importer.maxTextureSize = 4096;
            var bottom = assetPath.Contains("/Backgrounds/") || assetPath.Contains("/Streets/");
            var pivot = bottom ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.12f);
            if (!TryLayout(assetPath, out var columns, out var rows))
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = bottom ? pivot : new Vector2(0.5f, 0.5f);
                importer.SetTextureSettings(settings);
                return;
            }

            importer.spriteImportMode = SpriteImportMode.Multiple;
            var width = 0;
            var height = 0;
            if (File.Exists(assetPath))
            {
                // PNG header stores dimensions at bytes 16-23 in big-endian order.
                using var stream = File.OpenRead(assetPath);
                var header = new byte[24];
                if (stream.Read(header, 0, header.Length) != header.Length) return;
                width = ReadBigEndian(header, 16);
                height = ReadBigEndian(header, 20);
            }
            if (width < columns || height < rows) return;
            var sprites = new SpriteMetaData[columns * rows];
            for (var y = 0; y < rows; y++)
            for (var x = 0; x < columns; x++)
            {
                var left = width * x / columns;
                var right = width * (x + 1) / columns;
                var top = height * y / rows;
                var bottomPixel = height * (y + 1) / rows;
                if (assetPath.EndsWith("ui_upgrade_badges_3x1.png", StringComparison.Ordinal))
                {
                    left = x == 0 ? 0 : x == 1 ? 715 : 1430;
                    right = x == 0 ? 715 : x == 1 ? 1430 : width;
                }
                sprites[y * columns + x] = new SpriteMetaData
                {
                    name = "frame_" + (y * columns + x).ToString("D2"),
                    rect = new Rect(left, height - bottomPixel, right - left, bottomPixel - top),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = pivot
                };
            }
            importer.spritesheet = sprites;
        }

        private static bool TryLayout(string path, out int columns, out int rows)
        {
            columns = rows = 0;
            var file = Path.GetFileName(path);
            var match = Grid.Match(file);
            if (match.Success)
            {
                columns = int.Parse(match.Groups[1].Value);
                rows = int.Parse(match.Groups[2].Value);
                return true;
            }
            if (file.Contains("_4frames")) { columns = 4; rows = 1; return true; }
            if (file.Contains("_3sprites") || file.Contains("_3poses")) { columns = 3; rows = 1; return true; }
            if (file.Contains("_6frames")) { columns = 3; rows = 2; return true; }
            return false;
        }

        private static int ReadBigEndian(byte[] bytes, int at)
        {
            return (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];
        }
    }
}
