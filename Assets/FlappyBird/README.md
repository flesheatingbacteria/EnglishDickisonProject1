# Flappy Bird framework

Open `Assets/Scenes/SampleScene.unity` and press **Play**. The game creates its bird, pipes, camera setup, ground, and UI automatically; no art or prefab setup is needed.

- The bird starts perched in a nest instead of a start dialog. The nest scrolls away after takeoff and returns on reset.
- **Space, left click, or touch:** jump from the nest and flap.
- After a collision, the visible game freezes for one second. Then press Space, click, or tap to jump from the collision position and continue with the same pipes and score. The bird can pass through the obstacle during recovery until it is clear, preventing an immediate repeat collision.
- Passing a whole pipe pair awards one point. Best score persists locally.
- A dark storm cloud follows behind the bird with animated rain and occasional lightning. It keeps a safe distance, cannot damage the bird, and resets with the nest.

## Customize

For saved Inspector settings, create an empty GameObject in the scene and add **Flappy Bird Game** before entering Play mode. The automatic bootstrap detects it and does not create a second game. Adjust gravity, flap speed, pipe speed, spawn interval, gap, width, and arena height. Keep the camera aspect ratio fixed during a run; restart Play mode after changing the Game view aspect ratio.

`BirdController` handles physics and collisions. `PipePair` handles movement, scoring, and cleanup. `FlappyBirdGame` handles input, spawning, state, restart, placeholder visuals, and UI. To use another scene, add the game component explicitly.

The graphics and immediate-mode UI are placeholders for a future sprite/prefab and Canvas UI pass. Input supports the project's new Input System, with a compile-time fallback for projects using the legacy input manager. No package changes are required.
