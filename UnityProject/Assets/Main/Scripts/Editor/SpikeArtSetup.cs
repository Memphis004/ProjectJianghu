using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectJianghu.Game.Editor
{
    /// <summary>
    /// Generates the placeholder pixel art the HD-2D spike renders, and imports it with the settings
    /// the spike's ADR depends on: point filter, one pixels-per-unit for the whole project, and a
    /// bottom-centre pivot so a sprite's transform is the tile it stands on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The art is generated here rather than committed as a drawing because there is no artist in the
    /// loop yet: the point of Stage 03 is the <i>pipeline</i> (filter, PPU, pivot, sorting), and a
    /// 32×48 silhouette file is enough to measure it. A real sprite replaces the PNG and keeps the same
    /// importer settings — which is why those settings are applied here, once, instead of in a note.
    /// </para>
    /// <para>
    /// <b>Why the banner is a sprite.</b> 武 is decorative: it never changes and never needs a Thai or
    /// any other glyph next to it. Adding CJK to the font asset would rasterise a glyph block into the
    /// atlas and grow the WebGL payload for one picture (ADR 0112). Drawing its strokes into a PNG at
    /// authoring time keeps the font at ASCII + Thai, and the banner costs 72×72 px.
    /// </para>
    /// </remarks>
    public static class SpikeArtSetup
    {
        /// <summary>Where the generated sprites live.</summary>
        public const string ArtFolder = "Assets/Main/Art/Sprites";

        /// <summary>The standing fighter sprite, 32×48 at 32 PPU (one tile wide, 1.5 tiles tall).</summary>
        public const string FighterPath = ArtFolder + "/fighter_idle.png";

        /// <summary>The second fighter sprite, a recoloured guard for the depth test.</summary>
        public const string RivalPath = ArtFolder + "/fighter_rival.png";

        /// <summary>The banner's decorative 武, drawn as pixels and never as a font glyph.</summary>
        public const string BannerPath = ArtFolder + "/banner_wu.png";

        /// <summary>One sprite for each of a 12×12 arena's edges, kept small on purpose.</summary>
        public const string ShadowPath = ArtFolder + "/shadow_soft.png";

        /// <summary>Creates every sprite that is missing, then imports it with the spike's settings.</summary>
        public static void EnsureSpikeArt()
        {
            ProjectSetup.EnsureFolder(ArtFolder);

            WriteIfMissing(FighterPath, () => DrawFighter(new Color32(46, 62, 96, 255), new Color32(214, 180, 120, 255)));
            WriteIfMissing(RivalPath, () => DrawFighter(new Color32(96, 44, 44, 255), new Color32(190, 190, 200, 255)));
            WriteIfMissing(BannerPath, DrawBannerGlyph);
            WriteIfMissing(ShadowPath, DrawShadow);

            Import(FighterPath);
            Import(RivalPath);
            Import(BannerPath);
            Import(ShadowPath);

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// The importer settings every game sprite must have. Public so a test can assert the same
        /// contract on any sprite the game adds.
        /// </summary>
        public static void ApplySpriteImportSettings(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spritePixelsPerUnit = ProjectJianghu.Game.Presentation.Isometric.IsometricLayout.PixelsPerUnit;
            settings.filterMode = FilterMode.Point;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }

        private static void Import(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer is null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
            }

            if (importer is null)
            {
                throw new InvalidOperationException("Could not import " + path + " as a texture.");
            }

            ApplySpriteImportSettings(importer);
        }

        private static void WriteIfMissing(string path, Func<Texture2D> draw)
        {
            if (File.Exists(path))
            {
                return;
            }

            Texture2D texture = draw();
            try
            {
                byte[] png = texture.EncodeToPNG();
                File.WriteAllBytes(path, png);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        // ------------------------------------------------------------------
        // Drawing
        // ------------------------------------------------------------------

        /// <summary>
        /// A 32×48 wuxia silhouette: robe, sash, head and a sheathed blade. Pivot is the feet, so the
        /// bottom row is the character's soles.
        /// </summary>
        private static Texture2D DrawFighter(Color32 robe, Color32 accent)
        {
            const int width = 32;
            const int height = 48;
            var pixels = new Color32[width * height];
            var outline = new Color32(18, 18, 24, 255);
            var skin = new Color32(226, 190, 154, 255);
            var hair = new Color32(28, 24, 30, 255);
            var steel = new Color32(170, 180, 194, 255);

            // Robe: a tapered body plus two sleeves, drawn in the outline colour first by one pixel,
            // which is all the read a 32-px sprite needs.
            FillTrapezoid(pixels, width, height, 10, 18, 22, 43, robe);
            FillRect(pixels, width, height, 6, 22, 6, 18, robe);
            FillRect(pixels, width, height, 20, 22, 6, 18, robe);
            FillRect(pixels, width, height, 6, 22, 3, 12, robe);
            FillRect(pixels, width, height, 23, 22, 3, 12, robe);

            // Sash and hem.
            FillRect(pixels, width, height, 9, 28, 14, 3, accent);
            FillRect(pixels, width, height, 10, 43, 12, 2, outline);

            // Head, hair and topknot: a 10-px circle, with the lower half skin.
            FillCircle(pixels, width, height, 16, 12, 6, hair);
            FillRect(pixels, width, height, 11, 11, 10, 7, skin);
            FillRect(pixels, width, height, 12, 6, 3, 6, hair);
            FillRect(pixels, width, height, 16, 5, 4, 4, hair);

            // A sheathed blade along the character's side.
            DrawLine(pixels, width, height, 24, 40, 29, 18, steel, 2);

            // Feet.
            FillRect(pixels, width, height, 11, 44, 4, 4, outline);
            FillRect(pixels, width, height, 17, 44, 4, 4, outline);

            AddOutline(pixels, width, height, outline, robe);

            // The drawing is authored top-down (row 0 is the hair), while a texture's row 0 is its
            // bottom. Flipping here is what makes the authored feet the sprite's import pivot.
            FlipVertically(pixels, width, height);
            return ToTexture(pixels, width, height);
        }

        /// <summary>A soft elliptical shadow the fighter stands on, at one tile wide.</summary>
        private static Texture2D DrawShadow()
        {
            const int width = 32;
            const int height = 16;
            var pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = (x - (width / 2f)) / (width / 2f);
                    float ny = (y - (height / 2f)) / (height / 2f);
                    float distance = (nx * nx) + (ny * ny);
                    if (distance > 1f)
                    {
                        continue;
                    }

                    byte alpha = (byte)Mathf.RoundToInt(90f * (1f - distance));
                    pixels[(y * width) + x] = new Color32(0, 0, 0, alpha);
                }
            }

            return ToTexture(pixels, width, height);
        }

        /// <summary>
        /// 武 as strokes on a 24×24 grid, scaled ×3 to 72×72 so the stroke width survives the scale.
        /// </summary>
        /// <remarks>
        /// The strokes are the eight of the real character: the top horizontal, 止 on the left (vertical,
        /// middle horizontal, short vertical, foot) and 戈 on the right (the hook, the crossing slash and
        /// the dot). They are drawn as lines rather than copied from a font: a rasterised glyph would be
        /// a picture of a typeface, while these are the character's own strokes, authored here.
        /// </remarks>
        private static Texture2D DrawBannerGlyph()
        {
            const int grid = 24;
            const int scale = 3;
            var cells = new bool[grid * grid];
            var ink = new Color32(20, 16, 14, 255);

            Stroke(cells, grid, 3, 3, 18, 3);    // 一 (top)
            Stroke(cells, grid, 7, 4, 7, 20);    // 止: vertical
            Stroke(cells, grid, 7, 11, 12, 11);  // 止: middle horizontal
            Stroke(cells, grid, 12, 11, 12, 18); // 止: short vertical
            Stroke(cells, grid, 3, 20, 15, 20);  // 止: foot
            Stroke(cells, grid, 17, 4, 20, 17);  // 戈: hook
            Stroke(cells, grid, 20, 17, 16, 21);
            Stroke(cells, grid, 11, 7, 19, 19);  // 戈: crossing slash
            Stroke(cells, grid, 19, 3, 21, 5);   // 戈: dot

            int size = grid * scale;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int gx = x / scale;
                    int gy = (size - 1 - y) / scale; // texture rows go up, the grid goes down
                    if (cells[(gy * grid) + gx])
                    {
                        pixels[(y * size) + x] = ink;
                    }
                }
            }

            return ToTexture(pixels, size, size);
        }

        private static void Stroke(bool[] cells, int grid, int x0, int y0, int x1, int y1)
        {
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int error = dx - dy;
            int x = x0;
            int y = y0;

            while (true)
            {
                SetCell(cells, grid, x, y);
                // A 2-px stroke: the glyph is decorative and is read at banner size, and a 1-px
                // stroke at 24×24 disappears when the 32-PPU sprite is drawn across two tiles.
                SetCell(cells, grid, x + 1, y);

                if (x == x1 && y == y1)
                {
                    break;
                }

                int doubled = error * 2;
                if (doubled > -dy)
                {
                    error -= dy;
                    x += sx;
                }

                if (doubled < dx)
                {
                    error += dx;
                    y += sy;
                }
            }
        }

        private static void SetCell(bool[] cells, int grid, int x, int y)
        {
            if (x < 0 || y < 0 || x >= grid || y >= grid)
            {
                return;
            }

            cells[(y * grid) + x] = true;
        }

        private static void FillRect(
            Color32[] pixels, int width, int height, int x, int y, int w, int h, Color32 colour)
        {
            for (int iy = y; iy < y + h; iy++)
            {
                if (iy < 0 || iy >= height)
                {
                    continue;
                }

                for (int ix = x; ix < x + w; ix++)
                {
                    if (ix < 0 || ix >= width)
                    {
                        continue;
                    }

                    pixels[(iy * width) + ix] = colour;
                }
            }
        }

        private static void FillTrapezoid(
            Color32[] pixels, int width, int height, int topWidth, int topY, int bottomWidth, int bottomY,
            Color32 colour)
        {
            for (int y = topY; y <= bottomY; y++)
            {
                float t = bottomY == topY ? 0f : (y - topY) / (float)(bottomY - topY);
                int w = Mathf.RoundToInt(Mathf.Lerp(topWidth, bottomWidth, t));
                int x = (width / 2) - (w / 2);
                FillRect(pixels, width, height, x, y, w, 1, colour);
            }
        }

        private static void FillCircle(
            Color32[] pixels, int width, int height, int cx, int cy, int radius, Color32 colour)
        {
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    int dx = x - cx;
                    int dy = y - cy;
                    if ((dx * dx) + (dy * dy) > radius * radius)
                    {
                        continue;
                    }

                    if (x < 0 || y < 0 || x >= width || y >= height)
                    {
                        continue;
                    }

                    pixels[(y * width) + x] = colour;
                }
            }
        }

        private static void DrawLine(
            Color32[] pixels, int width, int height, int x0, int y0, int x1, int y1, Color32 colour, int thickness)
        {
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int error = dx - dy;
            int x = x0;
            int y = y0;

            while (true)
            {
                FillRect(pixels, width, height, x, y, thickness, thickness, colour);
                if (x == x1 && y == y1)
                {
                    break;
                }

                int doubled = error * 2;
                if (doubled > -dy)
                {
                    error -= dy;
                    x += sx;
                }

                if (doubled < dx)
                {
                    error += dx;
                    y += sy;
                }
            }
        }

        /// <summary>Adds a one-pixel dark rim around the robe so the silhouette reads on a light floor.</summary>
        private static void AddOutline(
            Color32[] pixels, int width, int height, Color32 outline, Color32 robe)
        {
            var copy = (Color32[])pixels.Clone();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width) + x;
                    if (!Equals(copy[index], robe))
                    {
                        continue;
                    }

                    if (IsOutside(copy, width, height, x - 1, y)
                        || IsOutside(copy, width, height, x + 1, y)
                        || IsOutside(copy, width, height, x, y - 1)
                        || IsOutside(copy, width, height, x, y + 1))
                    {
                        pixels[index] = outline;
                    }
                }
            }
        }

        private static void FlipVertically(Color32[] pixels, int width, int height)
        {
            for (int y = 0; y < height / 2; y++)
            {
                int opposite = height - 1 - y;
                for (int x = 0; x < width; x++)
                {
                    int a = (y * width) + x;
                    int b = (opposite * width) + x;
                    Color32 swap = pixels[a];
                    pixels[a] = pixels[b];
                    pixels[b] = swap;
                }
            }
        }

        private static bool IsOutside(Color32[] pixels, int width, int height, int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return false;
            }

            return pixels[(y * width) + x].a == 0;
        }

        private static Texture2D ToTexture(Color32[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                name = "Generated",
            };

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>One line naming every sprite and its import contract, for the spike's log.</summary>
        public static string Describe()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "spike art: {0}/{1}/{2}/{3} at {4} PPU, point filter, bottom-centre pivot",
                FighterPath,
                RivalPath,
                BannerPath,
                ShadowPath,
                ProjectJianghu.Game.Presentation.Isometric.IsometricLayout.PixelsPerUnit);
        }
    }
}
