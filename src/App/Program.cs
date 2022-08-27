using PixelWindowSystem;

const uint width = 128, height = 72, scale = 8;

var appManager = new GameOfLifeAppManager(width, height, scale);

var window = new PixelWindow(width * scale, height * scale, scale, "Game of life", appManager,
    fixedTimestep: 50, framerateLimit: 200);

window.Run();
