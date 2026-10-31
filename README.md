# 🪺 WPF Pelican

**A pelican. A bicycle. Pure WPF. No images, no sprites, no game engine — just vector shapes and math.**

A self-contained C# WPF app that renders a fully animated pelican riding a bike down an endless road. Every element — the bird, the bike, the scenery — is drawn from XAML primitives (paths, ellipses, lines) and animated in code with real kinematics.

![Pelican riding a bike](pelican.gif)

## Screenshots

| | |
|---|---|
| ![Scene A](screenshots/scene-a.png) | ![Scene B](screenshots/scene-b.png) |
| Pedals mid-stroke, pouch mid-bob | Wings mid-flap, knees mid-bend |

![Scene C](screenshots/scene-c.png)

## What's animating

Nothing is keyframed. Every moving part is simulated per frame on `CompositionTarget.Rendering`:

- **🦵 Two-bone inverse kinematics legs** — each foot is *locked* to its pedal on the crank circle (pedals always 180° apart); thigh and shin lengths are solved with the law of cosines, so the knees bend naturally through the whole crank cycle. Reachability is guaranteed: hip-to-pedal distance stays within the leg's range for every crank angle.
- **🚲 Kinematic wheels** — wheel angular speed = ground speed ÷ wheel radius, so the road dashes, parallax hills and drifting clouds all scroll at physically consistent speeds.
- **🪟 Secondary motion** — body bob at pedalling rhythm, gentle wing flap, throat-pouch bob, tail wag.
- **♾ Seamless world** — hills and road markings wrap with twin copies; the ride never ends and never jumps.

## Running it

```bash
git clone https://github.com/djgene01/wpf-pelican.git
cd wpf-pelican
dotnet run
```

Requires the **.NET 10 SDK** on Windows (WPF). Click the window to pause/resume.

### Headless screenshots

The app has a built-in capture mode that renders the live scene to a PNG and exits — handy for CI and for making README art:

```bash
PELICAN_SHOT=frame.png PELICAN_SHOT_FRAMES=60 dotnet run
```

## Project layout

```
PelicanBike/
├─ PelicanBike.csproj      # net10.0-windows, UseWPF
├─ MainWindow.xaml         # the whole scene as vector art (named elements)
├─ MainWindow.xaml.cs      # the animation loop: IK, parallax, spin, bob
├─ screenshots/            # stills (captured with PELICAN_SHOT)
└─ pelican.gif             # the animation (36 frames @ 12 fps)
```

## Fun fact

The pelican never falls off. The IK solver clamps the hip-pedal distance to the leg's reachable range, so even if you crank the geometry up to absurd values, the legs just stretch — they never detach.

---

Made with ♥, WPF and trigonometry. No pelicans were harmed; the bike is doing all the work.
