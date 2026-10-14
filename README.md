# English Dickinson Project — beta-version

> **This is the beta-version (fork).** It's the main game plus one experimental idea, "the tune without the words": the poem's *form* builds each level. Every stressed beat of a line is a pipe (so the stanzas run 13, 14 and 14 pipes in common meter's four-then-three rhythm), every dash is a breath with no pipe, pipes on rhyming lines share a colour, the level card shows each stanza's rhyme scheme (ABCB, ABAB, ABBB), and the HUD's words fall away level by level until Extremity is played with none. The main version lives at [redcappyal/EnglishDickisonProject1](https://github.com/redcappyal/EnglishDickisonProject1). The title screen shows **BETA-VERSION - 1.5.1**.

A Unity 6 Flappy Bird game framework.

## Run

1. Open this folder in Unity Hub with **Unity 6000.6.5f1**.
2. Allow Unity to restore packages and import assets.
3. Open `Assets/Scenes/SampleScene.unity` and press **Play**.
4. Press **Space**, click, or tap to launch from the nest and flap.

Three levels follow the three stanzas of "Hope" is the thing with feathers: **Hope**, **The Gale** and **Extremity**, 15 pipes each. Between levels (and at the start) a cutscene flies the bird through the next stanza while the world and its weather blend into the next one.

v 1.4.1: **Hope never stops at all.** There is no game over: after a crash the game freezes for a second, then you flap and carry on. The score is simply how many times you crashed, and your record is the **fewest crashes** in a full run. Your **hope** bar drains as you fly and each crash costs some; grab hearts (placed a little out of the way) to refill it. With no hope left the bird still flies, just with weaker wingbeats. At the end of the Gale the storm surges ("And sore must be the storm"): heavier rain, lightning, gusts that shove the bird and faster pipes, until it breaks as you leave.

See [the framework guide](Assets/FlappyBird/README.md) for customization.

## Versions

The version is shown on the title screen and the poem page. Each is tagged in git (`v1.0.0` ... `v1.5.1`).

| Version | What changed |
|---|---|
| 1.0.0 | Pixel art; one level per stanza (Hope, The Gale, Extremity) |
| 1.1.0 | Pixel-art HUD, poem page and props |
| 1.2.0 | Upgraded to Unity 6000.6.5f1 |
| 1.3.0 | Hope bar and hearts, stanza cutscenes, weather, title screen |
| 1.4.0 | The nest's tree matches each world |
| 1.4.1 | Hope never stops: no game over, score is crashes; the Gale's storm surge |
| 1.5.0 | Smooth transitions everywhere; the tree glides in at each level; Reset Level button; rougher pipes; faster UI |
| 1.5.1 | Skipped cutscenes fade their words out; clearer reset icon |

A separate experimental version, **beta-version**, lives in the fork [flesheatingbacteria/EnglishDickisonProject1](https://github.com/flesheatingbacteria/EnglishDickisonProject1). It builds levels from the poem's form: meter, dashes and rhymes.

`Assets` (including `.meta` files), `Packages`, and `ProjectSettings` are versioned. Unity regenerates `Library`, `Temp`, and IDE project files locally.
