using PixelWindowSystem;
using SFML.Graphics;
using SFML.Window;

/// <summary>
/// Implementation of game of life. Implements <see cref="IPixelWindowAppManager"/> for rendering.
/// </summary>
internal class GameOfLifeAppManager : IPixelWindowAppManager
{
    // Width of canvas, height of canvas, and how many screen pixels there are per canvas pixel
    private uint _width, _height, _scale;

    // Array of cell data.
    // First dimension is the frame index which essentially allows easy swapping between two 2d arrays. This will always be 2.
    // Second dimension is width, third dimension is height.
    private CellState[,,] _gridData;

    // When true, simulation is paused and won't update, otherwise it will be updated in the fixed update method.
    private bool _paused = true;

    // The SFML render window. Stored from the OnLoad method for reading input.
    private RenderWindow? _renderWindow;

    // Functions we get from the PixelWindow class, passed in through OnLoad for us to toggle the grid and screen outline
    private Action<bool>? ShowGrid, ShowOutline;

    // Tracks the current state of whether the grid is shown, for use with the ShowGrid function
    private bool _showGrid = true;

    /// <summary>
    /// Initialise the game of life app to run within a <see cref="PixelWindow"/>
    /// </summary>
    /// <param name="width">Width of canvas</param>
    /// <param name="height">Height of canvas</param>
    /// <param name="scale">How many screen pixels there are per canvas pixel</param>
    public GameOfLifeAppManager(uint width, uint height, uint scale)
    {
        _width = width;
        _height = height;
        _scale = scale;

        // First dimension is frame index. Swapping between 2 sets of grid data allows the 
        _gridData = new CellState[2, _width, _height];
    }

    // An index which basically flicks between 1 and 0 so we can reference one frame whilst writing to another.
    private int _currentFrameIndex = 0;
    private int _otherFrameIndex => _currentFrameIndex == 0 ? 1 : 0;
    private void SwapFrame() { _currentFrameIndex = _otherFrameIndex; }

    public void OnLoad(RenderWindow renderWindow, Action<bool> showGrid, Action<bool> showOutline)
    {
        _renderWindow = renderWindow;
        _renderWindow.KeyPressed += KeyPressedHandler;
        ShowGrid = showGrid;
        ShowOutline = showOutline;

        ShowOutline(_paused);
        ShowGrid(_showGrid);
    }

    // Handler for all key press events sent to the window
    private void KeyPressedHandler(object? sender, KeyEventArgs e)
    {
        switch (e.Code)
        {
            case Keyboard.Key.Space: // Pause
                _paused = !_paused;
                ShowOutline!(_paused);
                break;
            case Keyboard.Key.C: // Clear screen
                ClearFrame(_currentFrameIndex);
                _paused = true; // Always pause when screen is cleared
                ShowOutline!(_paused);
                break;
            case Keyboard.Key.G: // Toggle grid
                _showGrid = !_showGrid;
                ShowGrid!(_showGrid);
                break;
            default:
                return;
        }
    }

    public void Update(float frameTime)
    {
        HandleDrawingCells();

        _renderWindow!.SetTitle($"Game of Life - {(_paused ? "PAUSED -" : "")} Clear [C], Pause [Space], Toggle Grid [G]");
    }

    // Function used in the Update method to handle drawing live and dead cells with the mouse
    private void HandleDrawingCells()
    {
        var mousePos = Mouse.GetPosition(_renderWindow);
        var lmbDown = Mouse.IsButtonPressed(Mouse.Button.Left);
        var rmbDown = Mouse.IsButtonPressed(Mouse.Button.Right);

        CellState? newCellState = null;
        if (lmbDown)
        {
            newCellState = CellState.Live;
        }
        else if (rmbDown)
        {
            newCellState = CellState.Dead;
        }

        // No buttons pressed, do nothing
        if (newCellState == null)
        {
            return;
        }

        // Clicked outside of window, do nothing (and therefore avoid trying to write pixels outside of the bounds of the array)
        if (IsMouseOutsideOfWindow(mousePos.X, mousePos.Y))
        {
            return;
        }

        var canvasPos = (x: mousePos.X / _scale, y: mousePos.Y / _scale);
        _gridData[_currentFrameIndex, canvasPos.x, canvasPos.y] = newCellState.Value;
    }

    private bool IsMouseOutsideOfWindow(int x, int y)
    {
        return
            x < 0 || y < 0 ||
            x >= _width * _scale ||
            y >= _height * _scale;
    }

    // Sets all cells in the given frame to dead
    private void ClearFrame(int frameIndex)
    {
        for (uint x = 0; x < _width; x++)
        {
            for (uint y = 0; y < _height; y++)
            {
                _gridData[frameIndex, x, y] = CellState.Dead;
            }
        }
    }

    public void FixedUpdate(float timeStep)
    {
        if (_paused)
        {
            return;
        }

        // Clear other frame first, ready for us to write to it
        ClearFrame(_otherFrameIndex);

        // Calculate state of each cell in other frame for next render. Only set cells if they are live, as we cleared everything to empty.
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                int liveNeighbours = 0;
                liveNeighbours += IsNeighbourAlive(x, y - 1) ? 1 : 0; // top
                liveNeighbours += IsNeighbourAlive(x, y + 1) ? 1 : 0; // bottom
                liveNeighbours += IsNeighbourAlive(x - 1, y) ? 1 : 0; // left
                liveNeighbours += IsNeighbourAlive(x + 1, y) ? 1 : 0; // right

                liveNeighbours += IsNeighbourAlive(x + 1, y - 1) ? 1 : 0; // top right
                liveNeighbours += IsNeighbourAlive(x + 1, y + 1) ? 1 : 0; // bottom right
                liveNeighbours += IsNeighbourAlive(x - 1, y - 1) ? 1 : 0; // top left
                liveNeighbours += IsNeighbourAlive(x - 1, y + 1) ? 1 : 0; // bottom left

                // The actual rules of Conway's game of life!
                if (_gridData[_currentFrameIndex, x, y] == CellState.Live)
                {
                    if (liveNeighbours == 2 || liveNeighbours == 3)
                    {
                        _gridData[_otherFrameIndex, x, y] = CellState.Live;
                    }
                }
                else
                {
                    if (liveNeighbours == 3)
                    {
                        _gridData[_otherFrameIndex, x, y] = CellState.Live;
                    }
                }
            }
        }

        // We've written to the other frame, so swap to it so we can render it.
        SwapFrame();
    }

    /// <summary>
    /// Checks if a neighbour at x,y is alive on the current frame.
    /// If x or y are out of bounds, it returns <see cref="CellState.Dead"/>
    /// </summary>
    private bool IsNeighbourAlive(int x, int y)
    {
        // Got to make sure we don't go outside of the canvas. If we do, assume dead neighbour.
        if (x < 0 || y < 0 || x >= _width || y >= _height)
        {
            return false;
        }
        else
        {
            return _gridData[_currentFrameIndex, x, y] == CellState.Live;
        }
    }

    public void Render(PixelData pixelData, float frameTime)
    {
        pixelData.Clear();
        for (uint x = 0; x < _width; x++)
        {
            for (uint y = 0; y < _height; y++)
            {
                pixelData[x, y] = _gridData[_currentFrameIndex, x, y] switch
                {
                    CellState.Dead => (50, 50, 50),
                    CellState.Live => (255, 255, 255),
                    _ => (255, 0, 0) // If we see red, we dun goof'd
                };
            }
        }
    }
}

/// <summary>
/// Enum representing the state of a cell in the grid
/// </summary>
internal enum CellState
{
    Dead = 0,
    Live = 1
}