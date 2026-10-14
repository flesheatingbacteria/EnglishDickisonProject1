using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FlappyBird
{
    // The finale: Emily Dickinson herself, fought the way a certain skeleton fights. She talks
    // between attacks in dashes and Capitals; throws her dashes, quills and capital letters at
    // the bird; flips gravity to "tell it Slant"; and can't be hit back - the bird only has to
    // keep flying until she tires. Hits count as crashes and cost hope, but never end the run.
    public sealed class DickinsonBoss : MonoBehaviour
    {
        private enum Kind { Dash, Blue, Letter }

        private sealed class Shot
        {
            public Kind Kind;
            public Vector2 Position, Velocity, Half; // Half: half the size of the hit box
            public string Letter;
        }

        // A giant quill that slides in at the edge, charges, and fires a beam straight across.
        private sealed class Quill
        {
            public float Y, Born;
            public bool FromLeft, Charged, Fired;
        }

        private const float QuillIn = 0.35f, QuillCharge = 0.6f, QuillFire = 0.45f, QuillOut = 0.3f, BeamHalf = 0.42f;
        private const float BirdRadius = 0.19f, Invulnerable = 1.2f, HopePerHit = 8, CharsPerSecond = 26;
        private const float FightLength = 78; // seconds of attacks in the whole fight, for the progress bar
        private const float Pixel = 2 / PixelArt.PixelsPerUnit; // she's drawn at twice the usual scale
        private static readonly string[] Nouns = { "HOPE", "SOUL", "GALE", "STORM", "BIRD", "SEA", "LAND", "CRUMB" };
        private static readonly Color Ink = new Color(0.23f, 0.14f, 0.09f), Glow = new Color(0.55f, 0.92f, 1f);
        private static readonly Color BlueDash = new Color(0.35f, 0.72f, 1f), Gold = new Color(1f, 0.85f, 0.45f);

        private FlappyBirdGame game;
        private DickinsonMusic music;
        private SoundFx sfx;
        private SpriteRenderer art;
        private Sprite idle, asleep, smiling;
        private readonly List<Shot> shots = new List<Shot>();
        private readonly List<Quill> quills = new List<Quill>();

        // Talking: the bird hovers. Fighting: the bird flies under its own wings.
        private bool hovering, fighting, attacking, sleeping, waitingToPerch, perching, advance;
        private string line;
        private readonly List<string> lineRows = new List<string>();
        private float lineStart, hurtUntil = -10, fightTime, cutUntil = -10, eyeAt = -10, dawnAt = -10, slamAt = -10;
        private int blips;

        public bool Active { get; private set; }
        // The bird sits on her shoulder at the very end.
        public bool Perched { get; private set; }
        public bool Flickering => Active && Time.time < hurtUntil;
        public bool BirdStill => Perched || perching;

        private Camera View => Camera.main;
        private float Right => View.orthographicSize * View.aspect;
        private float Top => game.cameraHalfHeight;
        private float Floor => game.Floor;
        private float BirdX => game.Bird.transform.position.x;
        private float BirdY => game.Bird.transform.position.y;
        // She floats over the sea on the right, feet a little above the water.
        private Vector2 Home => new Vector2(Right - 1.9f, Floor + 2.3f);
        private int Shown => line == null ? 0 : Mathf.Min(line.Length, Mathf.FloorToInt((Time.time - lineStart) * CharsPerSecond));

        public void Initialize(FlappyBirdGame owner, DickinsonMusic soundtrack, SoundFx sounds)
        {
            game = owner;
            music = soundtrack;
            sfx = sounds;
            art = GetComponent<SpriteRenderer>();
            idle = PixelArt.Prop("dickinson", new Vector2(0.5f, 0));
            asleep = PixelArt.Prop("dickinson_sleep", new Vector2(0.5f, 0));
            smiling = PixelArt.Prop("dickinson_smile", new Vector2(0.5f, 0));
            art.sortingOrder = 4;
            transform.localScale = new Vector3(2, 2, 1);
            ResetBoss();
        }

        public void Begin()
        {
            ResetBoss();
            Active = hovering = true;
            game.BeginHover();
            music.Silence();
            StartCoroutine(Fight());
        }

        public void ResetBoss()
        {
            StopAllCoroutines();
            Active = Perched = hovering = fighting = attacking = sleeping = waitingToPerch = perching = advance = false;
            shots.Clear();
            quills.Clear();
            line = null;
            fightTime = 0;
            hurtUntil = cutUntil = eyeAt = dawnAt = slamAt = -10;
            art.enabled = false;
            art.sprite = idle;
            if (game.Bird != null) game.Bird.FlipGravity(false);
        }

        // ---- The script ----------------------------------------------------------------------

        private IEnumerator Fight()
        {
            yield return Wait(1.6f); // the world glides to a stop and the music fades away
            yield return Cut(0.5f);
            art.enabled = true; // ...and out of the dark, she's simply there
            yield return Wait(0.8f);
            yield return Say("so -");
            yield return Say("you're the Thing - with Feathers -");
            yield return Say("i've heard you - you know -");
            yield return Say("in the chillest Land - and on the strangest Sea -");
            yield return Say("and not once - did you ask me - for a Crumb -");
            yield return Say("very polite.");
            yield return Say("but i wonder -");
            yield return Say("do you ever - Stop - at all?");
            eyeAt = Time.time;
            yield return Say("let's find out.");

            yield return Cut(0.25f);
            music.SetLevel(DickinsonMusic.BossTheme);
            hovering = false;
            fighting = true;
            game.RestoreHope(100);
            game.Bird.Flap();
            yield return Attack(Dashes(9, 0, 6.5f, 0.85f));
            yield return Talk("not bad - for a Bird -");
            yield return Talk("the Blue ones - only sting - the Restless -");
            yield return Attack(Dashes(10, 0.45f, 6, 1.0f));
            yield return Talk("this next one - is a real - Page-turner -");
            yield return Attack(Quills(10, 0.75f));
            yield return Talk("tell all the truth - but tell it - Slant -");
            yield return Attack(Slant(11, 1.15f));
            yield return Talk("time to - Capitalize -");
            yield return Attack(Capitals(10, 0.16f), Quills(10, 1.6f));
            yield return Talk("alright. this is my Letter - to the World -");

            // Her special attack: everything at once, in quick black cuts...
            yield return Cut(0.15f);
            yield return Attack(Quills(4, 0.4f));
            yield return Cut(0.15f);
            yield return Attack(Dashes(5, 0.3f, 7.5f, 0.7f));
            yield return Cut(0.15f);
            yield return Attack(Slant(7, 0.9f));
            yield return Cut(0.15f);
            yield return Attack(Capitals(6, 0.12f), Quills(6, 0.9f));
            // ...until she slows, and stops.
            yield return Attack(Dashes(6, 0, 4, 1.5f));
            music.Silence();
            eyeAt = -10; // the glow goes out of her eye
            yield return Wait(1.2f);
            yield return Talk("...");
            yield return Talk("you're still - singing -");

            fighting = false;
            hovering = true;
            fightTime = FightLength;
            game.BeginHover();
            yield return Say("i wrote that - didn't i -");
            yield return Say("that you'd never stop - at all -");
            yield return Say("guess i was right.");
            yield return Say("i'll just - rest my Eyes - a moment -");
            sleeping = true;
            art.sprite = asleep;
            yield return Wait(1.2f);

            // The bird flies to her and perches on her shoulder.
            waitingToPerch = true;
            advance = false;
            while (!advance) yield return null;
            waitingToPerch = false;
            yield return Perch();
            yield return Wait(1.2f);
            sleeping = false;
            art.sprite = smiling;
            yield return Say("...oh.");
            yield return Say("you stayed.");
            yield return Say("that's Hope - i suppose.");
            yield return Say("go on then - sing me - one more -");
            music.SetLevel(1); // the warm theme from the first stanza
            sfx.Play(Sfx.Win);
            dawnAt = Time.time;
            yield return Wait(2.5f);
            game.FinishBoss();
        }

        private IEnumerator Wait(float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime) yield return null;
        }

        // A black cut, the way the fight jumps between attacks. Clears the screen.
        private IEnumerator Cut(float seconds)
        {
            cutUntil = Time.time + seconds;
            shots.Clear();
            quills.Clear();
            sfx.Play(Sfx.Cut);
            yield return Wait(seconds);
        }

        private void ShowLine(string text)
        {
            line = text.ToUpperInvariant();
            lineStart = Time.time;
            blips = 0;
            // Wrap once, up front, so the bubble doesn't change shape as the words type out.
            lineRows.Clear();
            string row = "";
            foreach (string word in line.Split(' '))
            {
                string next = row.Length == 0 ? word : row + " " + word;
                if (next.Length > 24 && row.Length > 0) { lineRows.Add(row); row = word; }
                else row = next;
            }
            lineRows.Add(row);
        }

        // A line while the bird hovers: Space finishes it, then moves on (or it moves on by itself).
        private IEnumerator Say(string text)
        {
            ShowLine(text);
            advance = false;
            float typing = text.Length / CharsPerSecond;
            while (true)
            {
                float t = Time.time - lineStart;
                if (advance)
                {
                    advance = false;
                    if (t < typing) lineStart = Time.time - typing;
                    else break;
                }
                if (t > typing + 2.4f) break;
                yield return null;
            }
            line = null;
            yield return Wait(0.15f);
        }

        // A line mid-fight: the bird keeps flying, and it goes by on its own. She lets it catch
        // its breath, too: some hope comes back while she talks.
        private IEnumerator Talk(string text)
        {
            ShowLine(text);
            if (fighting) game.RestoreHope(25, true);
            yield return Wait(text.Length / CharsPerSecond + 1.4f);
            line = null;
        }

        // Runs attacks side by side and waits for all of them.
        private IEnumerator Attack(params IEnumerator[] parts)
        {
            attacking = true;
            int running = parts.Length;
            foreach (IEnumerator part in parts) StartCoroutine(Track(part, () => running--));
            while (running > 0) yield return null;
            attacking = false;
        }

        private IEnumerator Track(IEnumerator part, System.Action done)
        {
            yield return StartCoroutine(part);
            done();
        }

        // ---- Attacks ---------------------------------------------------------------------------

        // Walls of dashes with a gap to fly through. Blue walls have no gap: they only sting a
        // bird that's rising, so the way through is to stop flapping and drop.
        private IEnumerator Dashes(float seconds, float blueChance, float speed, float every)
        {
            float gapY = BirdY;
            for (float t = 0, next = 0; t < seconds; t += Time.deltaTime)
            {
                if (t >= next)
                {
                    bool blue = Random.value < blueChance;
                    gapY = Mathf.Clamp(gapY + Random.Range(-1.7f, 1.7f), Floor + 1.6f, Top - 1.6f);
                    Wall(blue ? float.NaN : gapY, 2.6f, speed, blue);
                    next += blue ? every * 1.3f : every;
                }
                yield return null;
            }
        }

        private void Wall(float gapY, float gap, float speed, bool blue)
        {
            for (float y = Floor + 0.15f; y < Top; y += 0.42f)
            {
                if (!float.IsNaN(gapY) && Mathf.Abs(y - gapY) < gap / 2) continue;
                shots.Add(new Shot
                {
                    Kind = blue ? Kind.Blue : Kind.Dash, Position = new Vector2(Right + 0.6f, y),
                    Velocity = new Vector2(-speed, 0), Half = new Vector2(0.32f, 0.06f)
                });
            }
        }

        // Quills aimed at the bird's height; now and then a pair either side of it, to hold still between.
        private IEnumerator Quills(float seconds, float every)
        {
            for (float t = 0, next = 0; t < seconds; t += Time.deltaTime)
            {
                if (t >= next)
                {
                    if (Random.value < 0.25f)
                    {
                        AddQuill(BirdY + 1.05f);
                        AddQuill(BirdY - 1.05f);
                    }
                    else AddQuill(BirdY + Random.Range(-0.35f, 0.35f));
                    next += every;
                }
                yield return null;
            }
        }

        private void AddQuill(float y) =>
            quills.Add(new Quill { Y = Mathf.Clamp(y, Floor + 0.5f, Top - 0.5f), Born = Time.time, FromLeft = Random.value < 0.35f });

        // "Tell all the truth but tell it slant": gravity flips back and forth while dashes come.
        private IEnumerator Slant(float seconds, float every)
        {
            bool flipped = false;
            float gapY = BirdY;
            for (float t = 0, nextFlip = 0, nextWall = 0.9f; t < seconds; t += Time.deltaTime)
            {
                if (t >= nextFlip)
                {
                    flipped = !flipped;
                    Slam(flipped);
                    nextFlip += 3.6f;
                }
                if (t >= nextWall)
                {
                    gapY = Mathf.Clamp(gapY + Random.Range(-1.5f, 1.5f), Floor + 1.7f, Top - 1.7f);
                    Wall(gapY, 2.8f, 5, false);
                    nextWall += every;
                }
                yield return null;
            }
            if (flipped) Slam(false);
        }

        private void Slam(bool flipped)
        {
            game.Bird.FlipGravity(flipped);
            game.Bird.Bounce(0);
            game.Shake(0.2f);
            sfx.Play(Sfx.Slam);
            slamAt = Time.time;
        }

        // Capitalized Nouns, spelled out letter by letter, falling across the bird's path.
        private IEnumerator Capitals(float seconds, float every)
        {
            string word = "";
            int index = 0;
            for (float t = 0, next = 0; t < seconds; t += Time.deltaTime)
            {
                if (t >= next)
                {
                    if (index >= word.Length) { word = Nouns[Random.Range(0, Nouns.Length)]; index = 0; }
                    shots.Add(new Shot
                    {
                        Kind = Kind.Letter, Letter = word[index++].ToString(),
                        Position = new Vector2(Random.Range(BirdX - 1f, BirdX + 4.5f), Top + 0.6f),
                        Velocity = new Vector2(-Random.Range(0.8f, 2f), -Random.Range(2.6f, 3.6f)), Half = new Vector2(0.14f, 0.22f)
                    });
                    next += every;
                }
                yield return null;
            }
        }

        // ---- The ending ------------------------------------------------------------------------

        private IEnumerator Perch()
        {
            perching = true;
            Transform bird = game.Bird.transform;
            Vector3 from = bird.position;
            // Her shoulder, on the side away from the bird's flight.
            Vector3 to = transform.position + new Vector3(5 * Pixel, 30 * Pixel + 0.2f, 0);
            for (float t = 0; t < 1; t += Time.deltaTime / 1.6f)
            {
                float e = Mathf.SmoothStep(0, 1, t);
                Vector3 at = Vector3.Lerp(from, to, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * 1.2f;
                bird.SetPositionAndRotation(at, Quaternion.Euler(0, 0, Mathf.Lerp(15, 0, e)));
                yield return null;
            }
            bird.SetPositionAndRotation(to, Quaternion.identity);
            perching = false;
            Perched = true;
        }

        // ---- Every frame -----------------------------------------------------------------------

        private void Update()
        {
            if (!Active || Time.deltaTime <= 0) return; // nothing moves under the poem page
            float dt = Time.deltaTime;
            // She floats, bobbing a pixel or two - and keeps still once she's asleep.
            float bob = sleeping || Perched ? 0 : Mathf.Round(Mathf.Sin(Time.time * 1.6f) * 1.5f) * Pixel;
            transform.position = Home + Vector2.up * bob;
            if (attacking) fightTime += dt;

            if (FlappyBirdGame.FlapPressed() && !game.PointerOverControls()) OnFlap();
            if (hovering && !BirdStill) game.HoverBird(0.3f);

            // Her voice: a blip every other letter as the line types out.
            if (line != null)
                for (int shown = Shown; blips < shown; blips++)
                    if (char.IsLetter(line[blips]) && blips % 2 == 0) sfx.Play(Sfx.Voice, 0.7f, Random.Range(0.94f, 1.06f));

            UpdateShots(dt);
            UpdateQuills();
            if (fighting)
            {
                // The edges of the sky sting too, and bounce the bird back.
                if (BirdY > Top - 0.25f || BirdY < Floor + 0.27f) Hurt(true);
            }
        }

        // A flap: a wingbeat in the fight, otherwise it moves the talking along.
        private void OnFlap()
        {
            if (fighting)
            {
                game.Bird.Flap(game.Energy > 0 ? 1 : 0.8f);
                sfx.Play(Sfx.Flap, 0.6f);
            }
            else advance = true;
        }

        private void UpdateShots(float dt)
        {
            Vector2 bird = game.Bird.transform.position;
            bool rising = game.Bird.VerticalSpeed * game.Bird.GravitySign > 0.3f;
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                Shot shot = shots[i];
                shot.Position += shot.Velocity * dt;
                if (shot.Position.x < -Right - 1 || shot.Position.y < Floor - 1) { shots.RemoveAt(i); continue; }
                if (!fighting || shot.Kind == Kind.Blue && !rising) continue;
                Vector2 near = shot.Position + Vector2.Max(-shot.Half, Vector2.Min(shot.Half, bird - shot.Position));
                if ((near - bird).sqrMagnitude < BirdRadius * BirdRadius) Hurt(false);
            }
        }

        private void UpdateQuills()
        {
            for (int i = quills.Count - 1; i >= 0; i--)
            {
                Quill quill = quills[i];
                float age = Time.time - quill.Born;
                if (!quill.Charged && age >= QuillIn) { quill.Charged = true; sfx.Play(Sfx.Charge, 0.5f); }
                if (!quill.Fired && age >= QuillIn + QuillCharge) { quill.Fired = true; sfx.Play(Sfx.Beam, 0.6f); game.Shake(0.1f); }
                bool firing = age >= QuillIn + QuillCharge && age < QuillIn + QuillCharge + QuillFire;
                if (firing && fighting && Mathf.Abs(BirdY - quill.Y) < BeamHalf + BirdRadius) Hurt(false);
                if (age > QuillIn + QuillCharge + QuillFire + QuillOut) quills.RemoveAt(i);
            }
        }

        // A hit costs hope and counts as a crash, then the bird flickers, safe, for a moment.
        public void Hurt(bool surface)
        {
            if (!fighting) return;
            if (surface) game.Bird.Bounce(BirdY < 0 ? 5.5f : -4.5f);
            if (Time.time < hurtUntil) return;
            hurtUntil = Time.time + Invulnerable;
            game.Wound(HopePerHit);
            sfx.Play(Sfx.Hurt);
        }

        // ---- Drawing (GUI, in the HUD's 800-unit space) ----------------------------------------

        public void Draw(float width)
        {
            if (!Active || Event.current.type != EventType.Repaint) return;
            float now = Time.time;
            DrawDawn(width, now);
            DrawQuills(width, now);
            foreach (Shot shot in shots)
            {
                Vector2 at = game.ToUi(shot.Position);
                if (shot.Kind == Kind.Letter)
                    PixelUi.Text(shot.Letter, at.x, at.y - 20, 8, Color.white, 0.5f);
                else
                {
                    Rect dash = new Rect(Mathf.Round(at.x - 27), Mathf.Round(at.y - 6), 54, 12);
                    PixelUi.Fill(new Rect(dash.x - 3, dash.y - 3, dash.width + 6, dash.height + 6), PixelUi.Outline);
                    PixelUi.Fill(dash, shot.Kind == Kind.Blue ? BlueDash : Color.white);
                }
            }

            Vector2 head = game.ToUi((Vector2)transform.position + new Vector2(0, 38 * Pixel));
            // Her eye glows while she means it.
            if (eyeAt > 0 && art.enabled)
            {
                Vector2 eye = game.ToUi((Vector2)transform.position + new Vector2(-2 * Pixel, 37.5f * Pixel));
                float flicker = Mathf.Repeat(now * 9, 1) < 0.5f ? 1 : 0.6f;
                PixelUi.Fill(new Rect(Mathf.Round(eye.x - 6), Mathf.Round(eye.y - 6), 12, 12), new Color(Glow.r, Glow.g, Glow.b, 0.35f * flicker));
                PixelUi.Fill(new Rect(Mathf.Round(eye.x - 3), Mathf.Round(eye.y - 3), 6, 6), new Color(Glow.r, Glow.g, Glow.b, flicker));
                for (int i = 0; i < 3; i++)
                {
                    float rise = Mathf.Repeat(now * 1.5f + i / 3f, 1);
                    PixelUi.Fill(new Rect(Mathf.Round(eye.x - 9 - rise * 18), Mathf.Round(eye.y - 3 - rise * 24), 3, 3),
                        new Color(Glow.r, Glow.g, Glow.b, 1 - rise));
                }
            }
            if (sleeping)
                for (int i = 0; i < 3; i++)
                {
                    float rise = Mathf.Repeat(now * 0.45f + i / 3f, 1);
                    PixelUi.Text("Z", head.x - 30 - rise * 40, head.y - 60 - rise * 70, 2 + Mathf.Round(rise * 2), new Color(1, 1, 1, 1 - rise), 0.5f);
                }
            if (line != null) DrawBubble(width, head);
            DrawNamePlate(width);
            if (waitingToPerch && Mathf.Repeat(now, 1.1f) < 0.8f)
                PixelUi.Text("SPACE OR CLICK - FLY TO HER", width / 2, 520, 3, Color.white, 0.5f);
            // Gravity slams flash the bird's new "down".
            float sinceSlam = now - slamAt;
            if (sinceSlam < 0.25f)
            {
                float edge = game.Bird.GravitySign < 0 ? 0 : 800 - 12;
                PixelUi.Fill(new Rect(0, edge, width, 12), new Color(BlueDash.r, BlueDash.g, BlueDash.b, 1 - sinceSlam / 0.25f));
            }
            if (now < cutUntil) PixelUi.Fill(new Rect(0, 0, width, 800), Color.black);
        }

        private void DrawQuills(float width, float now)
        {
            const float scale = 6;
            Vector2 size = PixelUi.IconSize("quill", scale);
            foreach (Quill quill in quills)
            {
                float age = now - quill.Born;
                float leave = age - (QuillIn + QuillCharge + QuillFire);
                float slide = leave > 0 ? 1 - leave / QuillOut : Mathf.Clamp01(age / QuillIn);
                slide = Mathf.SmoothStep(0, 1, slide);
                float y = game.ToUi(new Vector2(0, quill.Y)).y;
                // The quill's back stays at the screen edge; its nib points across the sky.
                float x = quill.FromLeft ? -size.x + (size.x - 12) * slide : width - (size.x - 12) * slide;
                float nib = quill.FromLeft ? x + size.x : x;
                bool charging = age >= QuillIn && age < QuillIn + QuillCharge;
                bool firing = age >= QuillIn + QuillCharge && leave < 0;
                if (charging && Mathf.Repeat(now * 14, 1) < 0.6f)
                {
                    Rect warn = quill.FromLeft ? Rect.MinMaxRect(nib, y - 1.5f, width, y + 1.5f) : Rect.MinMaxRect(0, y - 1.5f, nib, y + 1.5f);
                    PixelUi.Fill(warn, new Color(Gold.r, Gold.g, Gold.b, 0.7f));
                }
                if (firing)
                {
                    // The beam narrows as it fades out.
                    float half = BeamHalf * 80 * Mathf.Clamp01((QuillIn + QuillCharge + QuillFire - age) / 0.15f);
                    float a = quill.FromLeft ? nib : 0, b = quill.FromLeft ? width : nib;
                    PixelUi.Fill(Rect.MinMaxRect(a, Mathf.Round(y - half), b, Mathf.Round(y + half)), new Color(Gold.r, Gold.g, Gold.b, 0.9f));
                    PixelUi.Fill(Rect.MinMaxRect(a, Mathf.Round(y - half * 0.55f), b, Mathf.Round(y + half * 0.55f)), Color.white);
                }
                PixelUi.Icon(charging || firing ? "quill_open" : "quill", x, y - size.y / 2, scale, quill.FromLeft);
            }
        }

        private void DrawBubble(float width, Vector2 head)
        {
            const float size = 3, rowHeight = 22;
            float textWidth = 0;
            foreach (string row in lineRows) textWidth = Mathf.Max(textWidth, PixelUi.TextWidth(row, size));
            Rect bubble = new Rect(0, 0, textWidth + 42, lineRows.Count * rowHeight + 30);
            bubble.x = Mathf.Max(24, Mathf.Round(head.x - bubble.width - 60));
            bubble.y = Mathf.Clamp(Mathf.Round(head.y - bubble.height / 2 - 40), 130, 800 - bubble.height - 120);
            PixelUi.Frame(bubble, "parchment");
            // A little tail pointing at her.
            Color paper = new Color(0.95f, 0.9f, 0.78f);
            float tailY = Mathf.Round(Mathf.Clamp(head.y - 30, bubble.y + 12, bubble.yMax - 24));
            PixelUi.Fill(new Rect(bubble.xMax - 3, tailY, 12, 12), paper);
            PixelUi.Fill(new Rect(bubble.xMax + 9, tailY + 3, 9, 6), paper);
            int left = Shown;
            for (int i = 0; i < lineRows.Count && left > 0; i++)
            {
                string row = lineRows[i];
                PixelUi.Text(row.Substring(0, Mathf.Min(row.Length, left)), bubble.x + 21, bubble.y + 16 + i * rowHeight, size, Ink, 0, false);
                left -= row.Length + 1;
            }
        }

        private void DrawNamePlate(float width)
        {
            if (!fighting && fightTime <= 0 || dawnAt > 0) return;
            Rect plate = new Rect(Mathf.Round(width / 2 - 170), 800 - 24 - 60, 340, 60);
            PixelUi.Frame(plate, "panel");
            PixelUi.Text("EMILY DICKINSON", plate.x + 18, plate.y + 14, 2, Color.white);
            PixelUi.Text("HP 1/1", plate.xMax - 18, plate.y + 14, 2, new Color(0.86f, 0.82f, 0.9f), 1);
            Rect track = new Rect(plate.x + 18, plate.y + 34, plate.width - 36, 9);
            PixelUi.Fill(track, new Color(0.16f, 0.15f, 0.25f));
            float progress = Mathf.Clamp01(fightTime / FightLength);
            PixelUi.Fill(new Rect(track.x, track.y, Mathf.Round(track.width * progress / 3) * 3, track.height), Gold);
        }

        // Once she smiles, dawn comes up over the frozen sea: warm bands strongest at the horizon.
        private static readonly Color[] DawnBands = BuildDawn();

        private static Color[] BuildDawn()
        {
            var bands = new Color[8];
            for (int i = 0; i < bands.Length; i++) bands[i] = new Color(1f, 0.45f + 0.04f * i, 0.62f - 0.03f * i, 0.03f + 0.03f * i);
            return bands;
        }

        private void DrawDawn(float width, float now)
        {
            if (dawnAt < 0) return;
            float dawn = Mathf.SmoothStep(0, 1, (now - dawnAt) / 2.5f);
            for (int i = 0; i < DawnBands.Length; i++)
            {
                Color band = DawnBands[i];
                PixelUi.Fill(new Rect(0, 300 + i * 40, width, 40), new Color(band.r, band.g, band.b, band.a * dawn));
            }
            PixelUi.Fill(new Rect(0, 620, width, 6), new Color(1f, 0.8f, 0.5f, 0.35f * dawn));
        }
    }
}
