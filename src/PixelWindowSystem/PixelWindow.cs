using SFML.Graphics;
using SFML.System;
using SFML.Window;
using System.Diagnostics;

namespace PixelWindowSystem
{
    /// <summary>
    /// A window class which allows high speed rendering of direct pixel data to the screen, at either 1:1 pixel size, or enlarged pixels
    /// </summary>
    public class PixelWindow
    {
        // Width and height of the window, and size of texture pixel in screen pixels (4 would be a 4x4 "pixel" - blown up 4x the size of screen pixels)
        private readonly uint _width, _height, _pixelScale;

        // Size of the actual pixel data being rendered
        private uint _renderWidth => _width / _pixelScale;
        private uint _renderHeight => _height / _pixelScale;

        // A model representing the raw pixel data that can be directly written to in the render function
        private PixelData _pixelData;

        // Window title
        private readonly string _title;

        // App manager, implementing our onload, update, fixed update, and render functions used within the Run method
        private readonly IPixelWindowAppManager _appManager;

        // The fixed timestep in ms used for the fixed update
        private readonly float _fixedTimestep;

        // SFML objects. A render texture is used to blow up pixels to a larger size. That render texture is drawn using a sprite
        // which has been scaled to the size of the window.
        private RenderWindow? _renderWindow;
        private RenderTexture? _renderTexture;
        private Sprite? _renderTextureSprite;

        // Used for displaying a pixel grid over the top of the canvas
        private bool _showGrid = true;
        private RenderTexture? _gridRenderTexture;
        private Sprite? _gridSprite;

        // Used for displaying an outline around the edge of the canvas
        private bool _showOutline = true;
        private RenderTexture? _outlineRenderTexture;
        private Sprite? _outlineSprite;

        /// <summary>
        /// Sets up and opens a new window with the specified parameters
        /// </summary>
        /// <param name="width">The actual screen pixel width of the window</param>
        /// <param name="height">The actual screen pixel height of the window</param>
        /// <param name="pixelScale">The size of each pixel. 1 would be a 1:1 scale of screen pixels to drawn pixels. 2 would be double sized pixels etc.</param>
        /// <param name="title">The window title. Performance debug data will be periodically appended to this also.</param>
        /// <param name="appManager">An instance of <see cref="IPixelWindowAppManager"/> to control the application</param>
        /// <param name="fixedTimestep">The fixed timestep between fixed updates, in ms</param>
        /// <param name="framerateLimit">The actual screen pixel width of the window</param>
        public PixelWindow(uint width, uint height, uint pixelScale, string title, IPixelWindowAppManager appManager,
            float fixedTimestep = 20, uint framerateLimit = 300)
        {
            _width = width;
            _height = height;
            _pixelScale = pixelScale;

            _title = title ?? "Title";

            _appManager = appManager;

            _fixedTimestep = fixedTimestep;

            _pixelData = new PixelData(_renderWidth, _renderHeight);

            SetupSfmlWindow(framerateLimit);
            SetupGrid();
            SetupOutline();

            _appManager.OnLoad(_renderWindow!,
                (show) => { _showGrid = show; },
                (show) => { _showOutline = show; });
        }

        private void SetupSfmlWindow(uint framerateLimit)
        {
            _renderWindow = new RenderWindow(
                new VideoMode(_width, _height),
                _title,
                Styles.Close);

            _renderWindow.SetFramerateLimit(framerateLimit);

            _renderTexture = new RenderTexture(_renderWidth, _renderHeight);
            _renderTexture.Texture.Smooth = false; // As we are blowing up the size of the pixels, we need to do this so it doesn't end up blurring it

            _renderTextureSprite = new Sprite(_renderTexture.Texture);
            _renderTextureSprite.Scale = new Vector2f(_pixelScale, _pixelScale);

            _renderWindow.Closed += new EventHandler((object? sender, EventArgs e) => {
                ((Window)sender!).Close();
            });

        }

        // Sets up a grid to render by drawing it once to a render texture, which can be repeatedly drawn over the top of
        // everything with a sprite
        private void SetupGrid()
        {
            _gridRenderTexture = new RenderTexture(_width, _height);
            _gridRenderTexture.Clear(new Color(0, 0, 0, 0)); // Fill with fully transparent pixels

            var lineColour = new Color(100, 100, 100);

            var verticalLine = new RectangleShape(new Vector2f(1f, _height));
            verticalLine.FillColor = lineColour;

            // Vertical lines
            for (int x = 0; x <= _renderWidth; x++)
            {
                verticalLine.Position = new Vector2f(x * _pixelScale, 0);
                _gridRenderTexture.Draw(verticalLine);
            }

            var horizontalLine = new RectangleShape(new Vector2f(_width, 1f));
            horizontalLine.FillColor = lineColour;

            // Vertical lines
            for (int y = 0; y <= _renderHeight; y++)
            {
                horizontalLine.Position = new Vector2f(0, y * _pixelScale);
                _gridRenderTexture.Draw(horizontalLine);
            }

            _gridSprite = new Sprite(_gridRenderTexture.Texture);
        }

        // Sets up an outline around the edge of the screen
        private void SetupOutline()
        {
            _outlineRenderTexture = new RenderTexture(_width, _height);
            _outlineRenderTexture.Clear(new Color(0, 0, 0, 0)); // Fill with fully transparent pixels

            var lineColour = new Color(255, 0, 0);

            var outlineRect = new RectangleShape(new Vector2f(_width, _height));
            outlineRect.OutlineColor = lineColour;
            outlineRect.OutlineThickness = -2; // Negative means outline goes into shape rather than outside of boundaries
            outlineRect.FillColor = new Color(0, 0, 0, 0); // Fully transparent as we only want the outline

            _outlineRenderTexture.Draw(outlineRect);

            _outlineSprite = new Sprite(_outlineRenderTexture.Texture);
        }

        /// <summary>
        /// Runs the window loop. Should be called after creation.
        /// </summary>
        public void Run()
        {
            // Used for displaying debug performance info in the titlebar, along with stopwatch timings and iteration counts
            const int titleDebugInfoFrequencyMs = 500;

            double perf_totalUpdateMs = 0, perf_totalFixedUpdateMs = 0, perf_totalPreRenderMs = 0,
                   perf_totalRenderMs = 0, perf_totalPostRenderMs = 0;
            int perf_numberOfIterationsTimed = 0;
            int perf_numberOfFixedTimestepIterationsTimed = 0;

            var performanceStopwatch = new Stopwatch();
            var debugInfoUpdateStopwatch = new Stopwatch();
            debugInfoUpdateStopwatch.Start();

            double frameTimeAccumulatorMs = 0;
            var frameTimeStopwatch = new Stopwatch();
            frameTimeStopwatch.Start();

            // Main update loop
            while (_renderWindow!.IsOpen)
            {
                var frameTime = (float)frameTimeStopwatch.Elapsed.TotalMilliseconds;
                frameTimeAccumulatorMs += frameTime;
                frameTimeStopwatch.Restart();

                _renderWindow.DispatchEvents();

                RunProcessAndAddToTotalTime(() => { _appManager.Update(frameTime); }, ref perf_totalUpdateMs, performanceStopwatch);

                while (frameTimeAccumulatorMs >= _fixedTimestep)
                {
                    RunProcessAndAddToTotalTime( () => { _appManager.FixedUpdate(_fixedTimestep); }, ref perf_totalFixedUpdateMs, performanceStopwatch);
                    perf_numberOfFixedTimestepIterationsTimed++;
                    frameTimeAccumulatorMs -= _fixedTimestep;
                }

                RunProcessAndAddToTotalTime(Prerender, ref perf_totalPreRenderMs, performanceStopwatch);
                RunProcessAndAddToTotalTime(() => { _appManager.Render(_pixelData, frameTime); }, ref perf_totalRenderMs, performanceStopwatch);
                RunProcessAndAddToTotalTime(Postrender, ref perf_totalPostRenderMs, performanceStopwatch);

                perf_numberOfIterationsTimed++;

                // Update title bar with debug info
                if (debugInfoUpdateStopwatch.ElapsedMilliseconds >= titleDebugInfoFrequencyMs)
                {
                    double getAverageAndResetTime(ref double totalMs, int iterationCount)
                    {
                        var averageMs = totalMs / iterationCount;
                        totalMs = 0;
                        return averageMs;
                    };

                    //var newTitle = $"{_title} - " +
                    //    $"Update: {       getAverageAndResetTime(ref perf_totalUpdateMs, perf_numberOfIterationsTimed)                   :0.0}ms | " +
                    //    $"Fixed Update: { getAverageAndResetTime(ref perf_totalFixedUpdateMs, perf_numberOfFixedTimestepIterationsTimed) :0.0}ms | " +
                    //    $"Prerender: {    getAverageAndResetTime(ref perf_totalPreRenderMs, perf_numberOfIterationsTimed)                :0.0}ms | " +
                    //    $"Render: {       getAverageAndResetTime(ref perf_totalRenderMs, perf_numberOfIterationsTimed)                   :0.0}ms | " +
                    //    $"Postrender: {   getAverageAndResetTime(ref perf_totalPostRenderMs, perf_numberOfIterationsTimed)               :0.0}ms";
                    //_renderWindow.SetTitle(newTitle);

                    perf_numberOfIterationsTimed = 0;
                    perf_numberOfFixedTimestepIterationsTimed = 0;
                    debugInfoUpdateStopwatch.Restart();
                }
            }
        }

        private void RunProcessAndAddToTotalTime(Action? action, ref double totalTime, Stopwatch stopwatch)
        {
            stopwatch.Reset();
            stopwatch.Start();
            action!();
            stopwatch.Stop();
            totalTime += stopwatch.Elapsed.TotalMilliseconds;
        }

        private void Prerender()
        {
            _renderTexture!.Clear();
        }

        private void Postrender()
        {
            _renderTexture!.Texture.Update(_pixelData.RawData);
            _renderWindow!.Clear();
            _renderWindow.Draw(_renderTextureSprite);

            if (_showGrid)
            {
                _renderWindow.Draw(_gridSprite);
            }

            if (_showOutline)
            {
                _renderWindow.Draw(_outlineSprite);
            }

            _renderWindow.Display();
        }
    }
}
