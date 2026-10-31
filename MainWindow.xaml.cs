using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PelicanBike;

public partial class MainWindow : Window
{
    // --- scene constants (Canvas units) ---
    private const double CrankX = 460, CrankY = 445;   // bottom-bracket centre
    private const double CrankR = 32;                  // crank arm length
    private const double HipX = 412, HipY = 334;       // pelican hip (under saddle)
    private const double Thigh = 78, Shin = 78;        // leg segment lengths
    private const double WheelR = 55;                  // wheel radius
    private const double Speed = 150;                  // px/sec ground speed

    private TimeSpan _lastTime;
    private bool _haveLast;
    private double _wheelAngle;     // accumulated wheel angle (rad)
    private double _scrollBack, _scrollFront, _scrollDash, _scrollCloud;
    private double _wingPhase, _bobPhase;
    private bool _paused;

    public MainWindow()
    {
        InitializeComponent();
        BuildRoadDashes();

        // place hills copies for seamless parallax wrap
        Canvas.SetLeft(hillsBackB, 1000);
        Canvas.SetLeft(hillsFrontB, 1000);

        _lastTime = default;
        MouseLeftButtonDown += (_, _) => _paused = !_paused;
        Closed += (_, _) => CompositionTarget.Rendering -= OnRendering;
        CompositionTarget.Rendering += OnRendering;

        var shot = Environment.GetEnvironmentVariable("PELICAN_SHOT");
        if (!string.IsNullOrEmpty(shot))
            CaptureShot(shot);
    }

    private void CaptureShot(string path)
    {
        // wait a couple of animation frames, then render the scene to PNG and exit
        int frames = 0;
        int target = int.TryParse(Environment.GetEnvironmentVariable("PELICAN_SHOT_FRAMES"), out var tf) ? tf : 60;
        void Handler(object? s, EventArgs e)
        {
            if (++frames < target) return;
            CompositionTarget.Rendering -= Handler;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    int w = (int)scene.Width, h = (int)scene.Height;
                    var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
                        w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    rtb.Render(scene);
                    var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                    using (var fs = File.Create(path))
                        enc.Save(fs);
                }
                finally
                {
                    Close();
                }
            }), DispatcherPriority.Background);
        }
        CompositionTarget.Rendering += Handler;
    }

    private void BuildRoadDashes()
    {
        for (double x = 0; x < 1000; x += 80)
        {
            var r = new Rectangle
            {
                Width = 44,
                Height = 5,
                RadiusX = 2.5,
                RadiusY = 2.5,
                Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xE9, 0x4A)),
            };
            Canvas.SetLeft(r, x);
            roadDashes.Children.Add(r);
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs rea) return;
        var now = rea.RenderingTime;
        if (_haveLast)
        {
            double dt = (now - _lastTime).Ticks / (double)TimeSpan.TicksPerSecond;
            if (dt > 0 && dt < 0.25 && !_paused) Step(dt);
        }
        _lastTime = now;
        _haveLast = true;
    }

    private void Step(double dt)
    {
        double omega = Speed / WheelR;          // wheel angular speed rad/s
        _wheelAngle += omega * dt;
        double crank = _wheelAngle * 0.55;      // pedals turn slower than wheels

        // ---- wheels spin ----
        double deg = _wheelAngle * 180 / Math.PI;
        rearSpokes.RenderTransform = new RotateTransform(deg, 55, 55);
        frontSpokes.RenderTransform = new RotateTransform(deg, 55, 55);

        // ---- parallax scroll ----
        _scrollBack = Wrap(_scrollBack + Speed * 0.10 * dt, 1000);
        _scrollFront = Wrap(_scrollFront + Speed * 0.25 * dt, 1000);
        _scrollDash = Wrap(_scrollDash + Speed * dt, 80);
        _scrollCloud = Wrap(_scrollCloud + Speed * 0.05 * dt, 1000);
        Canvas.SetLeft(hillsBackA, -_scrollBack);
        Canvas.SetLeft(hillsBackB, 1000 - _scrollBack);
        Canvas.SetLeft(hillsFrontA, -_scrollFront);
        Canvas.SetLeft(hillsFrontB, 1000 - _scrollFront);
        Canvas.SetLeft(roadDashes, -_scrollDash);
        Canvas.SetLeft(cloud1, WrapPos(120 - _scrollCloud, 1000, 1100));
        Canvas.SetLeft(cloud2, WrapPos(480 - _scrollCloud * 1.4, 1000, 1100));
        Canvas.SetLeft(cloud3, WrapPos(780 - _scrollCloud * 0.7, 1000, 1100));

        // ---- pelican body bob (pedalling rhythm) ----
        double bob = Math.Sin(crank * 2) * 2.2;
        Canvas.SetTop(pelican, bob);

        // ---- wing flap (slow, gentle) ----
        _wingPhase += dt * 2.2;
        wing.RenderTransform = new RotateTransform(Math.Sin(_wingPhase) * 5, 392, 262);

        // ---- lower beak / pouch bob ----
        _bobPhase += dt * 3.0;
        beakLower.RenderTransform = new TranslateTransform(0, Math.Sin(_bobPhase) * 2.5);

        // ---- tail wags ----
        tail.RenderTransform = new RotateTransform(Math.Sin(_bobPhase * 0.8) * 4, 352, 272);

        // ---- legs: crank -> pedal -> two-bone IK ----
        UpdateLeg(crank, bob, true);
        UpdateLeg(crank + Math.PI, bob, false);
    }

    private void UpdateLeg(double angle, double bob, bool near)
    {
        // pedal position on the crank circle
        double px = CrankX + CrankR * Math.Cos(angle);
        double py = CrankY + CrankR * Math.Sin(angle);

        // hip rides with the body bob
        double hx = HipX, hy = HipY + bob;

        double dx = px - hx, dy = py - hy;
        double dist = Math.Clamp(Math.Sqrt(dx * dx + dy * dy), Math.Abs(Thigh - Shin) + 0.1, Thigh + Shin - 0.1);

        double baseA = Math.Atan2(dy, dx);
        // knee angle via law of cosines; knee bends forward (toward the bike front)
        double cosA = (Thigh * Thigh + dist * dist - Shin * Shin) / (2 * Thigh * dist);
        double kneeA = baseA - Math.Acos(Math.Clamp(cosA, -1, 1));

        double kx = hx + Thigh * Math.Cos(kneeA);
        double ky = hy + Thigh * Math.Sin(kneeA);

        var thigh = near ? thighNear : thighFar;
        var shin = near ? shinNear : shinFar;
        var foot = near ? footNear : footFar;
        var pedal = near ? pedalNear : pedalFar;

        SetLine(thigh, hx, hy, kx, ky);
        SetLine(shin, kx, ky, px, py);
        SetLine(foot, px, py, px + 18, py + 2);

        Canvas.SetLeft(pedal, px - 9);
        Canvas.SetTop(pedal, py - 3);

        if (near)
        {
            crankArm.X1 = CrankX; crankArm.Y1 = CrankY;
            crankArm.X2 = px;     crankArm.Y2 = py;
        }
    }

    private static void SetLine(Line l, double x1, double y1, double x2, double y2)
    {
        l.X1 = x1; l.Y1 = y1; l.X2 = x2; l.Y2 = y2;
    }

    private static double Wrap(double v, double period) => ((v % period) + period) % period;

    /// <summary>wrap a canvas position into [-margin, 1000+margin]</summary>
    private static double WrapPos(double v, double span, double margin)
        => ((v + margin) % (span + 2 * margin) + (span + 2 * margin)) % (span + 2 * margin) - margin;
}