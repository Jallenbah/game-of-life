using PixelWindowSystem;

const uint width = 256, height = 144, scale = 4;

var appManager = new GameOfLifeAppManager();

var window = new PixelWindow(width * scale, height * scale, scale, "Game of life", appManager,
    fixedTimestep: 100, framerateLimit: 200);

window.Run();
