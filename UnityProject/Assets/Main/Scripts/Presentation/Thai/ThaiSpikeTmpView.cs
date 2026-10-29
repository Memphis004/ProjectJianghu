using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace ProjectJianghu.Game.Presentation.Thai
{
    /// <summary>
    /// The uGUI + TextMeshPro side of the Thai text spike.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Coverage is asked per character with <c>HasCharacter</c>, not with
    /// <c>TryAddCharacters</c>. Measured on this machine during Stage 03:
    /// <c>TryAddCharacters</c> reports its <i>entire</i> input as missing whenever its
    /// internal <c>LoadFontFace</c> call fails — which happens on a font asset whose
    /// face is already loaded, i.e. on every call after the first. Its return value
    /// is therefore only trustworthy on the very first population of a fresh asset,
    /// and the glyphs it says are missing can be sitting in the atlas already. See
    /// <c>llmwiki/gotchas/tmp-tryaddcharacters-lies.md</c>.
    /// </para>
    /// <para>
    /// <c>HasCharacter</c> reads the character lookup table, so it answers the
    /// question the ADR actually needs: can the <i>shipped</i> font asset draw this
    /// string? The atlas is populated at build time by
    /// <c>ProjectSetup.BakeGlyphCoverage</c>.
    /// </para>
    /// <para>
    /// This type is a measurement harness, not UI: it touches no gameplay state and
    /// is expected to be deleted once the ADR is settled.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ThaiSpikeTmpView : MonoBehaviour
    {
        /// <summary>Label used in the report rows.</summary>
        public const string StackName = "uGUI+TMP";

        [SerializeField] private TMP_Text _label;

        [Tooltip("The TMP font asset generated from the Thai font (Editor-created).")]
        [SerializeField] private TMP_FontAsset _font;

        [Tooltip("A label the size of the arena HUD's skill slot, used for the 1280x720 fit test.")]
        [SerializeField] private TMP_Text _skillSlotLabel;

        /// <summary>
        /// Measures every string and appends the rows to <paramref name="report"/>.
        /// </summary>
        public void AppendRows(string[] texts, ThaiSpikeReport report)
        {
            string fontName = _font is null ? "<none>" : _font.name;
            if (_label is null)
            {
                report.Add(new ThaiSpikeRow(StackName, fontName, "<no label assigned>", 0, string.Empty, 0f, 0f));
                return;
            }

            if (_font is not null)
            {
                _label.font = _font;
            }

            for (int i = 0; i < texts.Length; i++)
            {
                string text = texts[i];
                string missing = CountMissingCharacters(text);

                _label.text = text;
                _label.ForceMeshUpdate();
                Vector2 size = _label.GetPreferredValues(text);

                report.Add(new ThaiSpikeRow(StackName, fontName, text, missing.Length, missing, size.x, size.y));
            }
        }

        /// <summary>
        /// Measures a skill name inside a slot the HUD really has: no wrapping, clipped, at the slot's
        /// own font size. A name that wants more width than the slot has is a name the player would
        /// see cut off, which is what the fit test asserts against.
        /// </summary>
        /// <param name="text">The longest skill name in the current language.</param>
        /// <param name="slotWidth">The slot's width in canvas units.</param>
        /// <param name="report">The report to record the measurement in.</param>
        public void AppendSkillSlot(string text, float slotWidth, ThaiSpikeReport report)
        {
            if (_skillSlotLabel is null)
            {
                report.RecordSkillSlot(text ?? string.Empty, 0f, 0f, slotWidth);
                return;
            }

            if (_font is not null)
            {
                _skillSlotLabel.font = _font;
            }

            _skillSlotLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _skillSlotLabel.overflowMode = TextOverflowModes.Overflow;
            _skillSlotLabel.text = text;
            _skillSlotLabel.ForceMeshUpdate();

            Vector2 size = _skillSlotLabel.GetPreferredValues(text);
            report.RecordSkillSlot(text, size.x, size.y, slotWidth);
        }

        /// <summary>
        /// The distinct characters of <paramref name="text"/> this font asset cannot
        /// draw, in first-appearance order.
        /// </summary>
        private string CountMissingCharacters(string text)
        {
            if (_font is null)
            {
                return string.Empty;
            }

            var missing = new StringBuilder();
            var checkedCharacters = new System.Collections.Generic.HashSet<char>();

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ' ' || !checkedCharacters.Add(c))
                {
                    continue;
                }

                if (!_font.HasCharacter(c))
                {
                    missing.Append(c);
                }
            }

            return missing.ToString();
        }
    }
}
