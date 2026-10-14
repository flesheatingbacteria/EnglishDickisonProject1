# Flappy Bird framework

Open `Assets/Scenes/SampleScene.unity` and press **Play**. The game creates its bird, pipes, camera setup, ground, and UI automatically; no art or prefab setup is needed.

- The bird starts perched in a nest instead of a start dialog. The nest scrolls away after takeoff and returns on reset.
- **Space, left click, or touch:** jump from the nest and flap.
- After a collision, the visible game freezes for one second. Then press Space, click, or tap to jump from the collision position and continue with the same pipes and score. The bird can pass through the obstacle during recovery until it is clear, preventing an immediate repeat collision.
- Passing a whole pipe pair awards one point. Best score persists locally.
- A dark storm cloud follows behind the bird in The Gale, with rain and occasional lightning. It keeps a safe distance and cannot damage the bird.
- **Hope:** drains every second (`energyDrain`, more each level) and by `crashCost` per crash. Hearts (`HopeHeart`) appear with every `heartEvery`th pipe, hugging a cap or off the line between gaps, and restore `heartValue`. At zero the bird falls and the level restarts.
- **Pipes** are procedural: each gap is placed relative to the last, and speed, gap size and the jumps between gaps all grow through each level.
- **The nest's tree** matches the world: a summer tree dropping leaves (Hope), a storm-bent tree that sways and sheds torn leaves (The Gale), and a frozen tree dropping snow (Extremity). See `TreeLeaves`.
- **Cutscenes** (`StanzaRibbon`): the stanza streams in as a wave of words the bird flies through; caught words gather into the stanza at the top. Space skips. Meanwhile leftover pipes fade, the backdrop, ground, sky, particles and music cross-fade, and `WeatherFx` rolls the next world's weather in (storm clouds and rain for the Gale, snow for Extremity).

## Customize

For saved Inspector settings, create an empty GameObject in the scene and add **Flappy Bird Game** before entering Play mode. The automatic bootstrap detects it and does not create a second game. Adjust gravity, flap speed, pipe speed, spawn interval, gap, width, and arena height. Keep the camera aspect ratio fixed during a run; restart Play mode after changing the Game view aspect ratio.

`BirdController` handles physics and collisions. `PipePair` handles movement, scoring, and cleanup. `FlappyBirdGame` handles input, spawning, state, restart, placeholder visuals, and UI. To use another scene, add the game component explicitly.

The bird, pipes, ground and backgrounds use the pixel art in `Assets/Resources/DickinsonSprites`, loaded by `PixelArt`; if a file is missing, the game falls back to its placeholder blocks. The levels follow the poem's stanzas: Hope, The Gale, Extremity. See [the art guide](../../Art/README.md) to change or regenerate the art. The HUD is drawn with `PixelUi` (pixel font, 9-slice frames, icons): a level card with progress pips, the score, an energy bar, a poem button, level banners, and small credits in the bottom-right corner. Input supports the project's new Input System, with a compile-time fallback for projects using the legacy input manager. No package changes are required.
