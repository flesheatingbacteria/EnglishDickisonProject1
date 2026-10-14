using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlappyBird
{
    // The cutscene between levels: a stanza streams in as a wave of words, the bird
    // flies through each one, and every word it touches lifts up into place until
    // the whole stanza is written across the top of the screen.
    public sealed class StanzaRibbon
    {
        private sealed class Word
        {
            public string Text;
            public int Line;
            public float SlotWidth; // width at the small size used in the finished stanza
            public float X, Y; // X along the ribbon, Y in world units
            public float CaughtAt = -1;
            public Vector2 CaughtFrom; // GUI position when caught
        }

        private const float WordGap = 0.45f, LineGap = 0.9f, CatchTime = 0.55f, Space = 8;
        private readonly List<Word> words = new List<Word>();
        private readonly int lineCount;
        private readonly float[] lineWidths;
        private float widest;
        private readonly float startX, startTime, speed;

        public float LastCatchTime { get; private set; } = -1;
        public bool AllCaught => words.TrueForAll(w => w.CaughtAt >= 0);

        public StanzaRibbon(string[] lines, float startX, float startTime, float unitsPerUiPixel)
        {
            this.startX = startX;
            this.startTime = startTime;
            lineCount = lines.Length;
            float x = 0;
            int index = 0;
            for (int line = 0; line < lines.Length; line++)
            {
                foreach (string token in Tokens(lines[line]))
                {
                    float width = PixelUi.TextWidth(token, 3) * unitsPerUiPixel;
                    // A gentle wave the bird can follow, kept clear of the floor and ceiling.
                    words.Add(new Word { Text = token, Line = line, X = x + width / 2, Y = -0.3f + 1.3f * Mathf.Sin(index * 0.75f) });
                    x += width + WordGap;
                    index++;
                }
                x += LineGap;
            }
            // Long stanzas move faster so no cutscene runs much past seven seconds.
            speed = Mathf.Max(5f, x / 7f);
            // Measure the finished stanza's layout once instead of every frame.
            lineWidths = new float[lineCount];
            foreach (Word word in words)
            {
                word.SlotWidth = PixelUi.TextWidth(word.Text, 2);
                lineWidths[word.Line] += word.SlotWidth + Space;
            }
            for (int i = 0; i < lineCount; i++) widest = Mathf.Max(widest, lineWidths[i] - Space);
        }

        // Dashes ride along with the word before them.
        private static IEnumerable<string> Tokens(string line)
        {
            var tokens = new List<string>();
            foreach (string part in line.Split(' '))
            {
                if (part.Length == 0) continue;
                if (part == "-" && tokens.Count > 0) tokens[tokens.Count - 1] += " -";
                else tokens.Add(part);
            }
            return tokens;
        }

        private float Offset(float time) => startX - Mathf.Max(0, time - startTime) * speed;

        // Catches every word the bird has reached and returns the height to fly at.
        public float Advance(float time, float birdX, Func<Vector2, Vector2> toUi)
        {
            float offset = Offset(time);
            Word before = null, after = null;
            foreach (Word word in words)
            {
                float x = offset + word.X;
                if (x <= birdX)
                {
                    if (word.CaughtAt < 0)
                    {
                        word.CaughtAt = time;
                        word.CaughtFrom = toUi(new Vector2(x, word.Y));
                        LastCatchTime = time;
                    }
                    before = word;
                }
                else if (after == null) after = word;
            }
            if (before == null) return after != null ? after.Y : 0;
            if (after == null) return 0;
            float t = Mathf.InverseLerp(offset + before.X, offset + after.X, birdX);
            return Mathf.Lerp(before.Y, after.Y, Mathf.SmoothStep(0, 1, t));
        }

        // GUI only. alpha fades the whole thing in and out.
        public void Draw(float time, float width, Color accent, float alpha, Func<Vector2, Vector2> toUi)
        {
            if (Event.current.type != EventType.Repaint) return;
            const float lineHeight = 20, top = 128;

            Color previous = GUI.color;
            GUI.color = new Color(1, 1, 1, alpha);
            Rect panel = new Rect(Mathf.Round((width - widest - 48) / 2), top, widest + 48, lineCount * lineHeight + 28);
            PixelUi.Frame(panel, "panel");

            float[] cursor = new float[lineCount];
            for (int i = 0; i < lineCount; i++) cursor[i] = (width - (lineWidths[i] - Space)) / 2;
            float offset = Offset(time);
            foreach (Word word in words)
            {
                Vector2 slot = new Vector2(cursor[word.Line] + word.SlotWidth / 2, top + 19 + word.Line * lineHeight);
                cursor[word.Line] += word.SlotWidth + Space;

                if (word.CaughtAt >= 0)
                {
                    float p = Mathf.Clamp01((time - word.CaughtAt) / CatchTime);
                    float e = Mathf.SmoothStep(0, 1, p);
                    Vector2 at = Vector2.Lerp(word.CaughtFrom, slot, e);
                    float size = p < 0.6f ? 3 : 2;
                    PixelUi.Text(word.Text, at.x, at.y - 5 * size / 2, size, Color.Lerp(Color.white, accent, e), 0.5f, p < 1);
                }
                else
                {
                    Vector2 at = toUi(new Vector2(offset + word.X, word.Y));
                    if (at.x > -200 && at.x < width + 200) PixelUi.Text(word.Text, at.x, at.y - 7.5f, 3, Color.white, 0.5f);
                }
            }
            GUI.color = previous;
        }
    }
}
