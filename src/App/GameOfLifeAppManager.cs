using PixelWindowSystem;
using SFML.Graphics;

class GameOfLifeAppManager : IPixelWindowAppManager
{
    public void OnLoad(RenderWindow renderWindow)
    {
    }

    public void Update(float frameTime)
    {
    }

    public void FixedUpdate(float timeStep)
    {
    }

    public void Render(PixelData pixelData, float frameTime)
    {
        pixelData.Clear();
    }
}