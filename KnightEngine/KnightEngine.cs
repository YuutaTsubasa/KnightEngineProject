using KnightEngine.OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL.Compatibility;

namespace KnightEngine;
using SDL3;

public class KnightEngine
{
    private SDL_Window _window;
    private SDL_GLContext _glContext;
    
    public void Initialize(string windowTitle, int windowWidth, int windowHeight)
    {
        if (!SDL3.SDL_Init(SDL_InitFlags.Video))
            throw new Exception($"Initialize SDL Error. Error: {SDL3.SDL_GetError()}");

        SDL3.SDL_GL_SetAttribute(SDL_GLattr.ContextMajorVersion, 2);
        SDL3.SDL_GL_SetAttribute(SDL_GLattr.ContextMinorVersion, 1);
        SDL3.SDL_GL_SetAttribute(SDL_GLattr.ContextProfileMask, SDL_GLprofile.Core);

        _window = SDL3.SDL_CreateWindow(
            windowTitle,
            windowWidth,
            windowHeight,
            SDL_WindowFlags.OpenGL | SDL_WindowFlags.Resizable);
        if (_window == IntPtr.Zero)
            throw new Exception($"Unable to create window: {windowTitle}");
        
        _glContext = SDL3.SDL_GL_CreateContext(_window);
        if (_glContext == IntPtr.Zero)
            throw new Exception($"Unable to create context: {windowTitle}");

        SDL3.SDL_GL_MakeCurrent(_window, _glContext);
        SDL3.SDL_GL_SetSwapInterval(1);
        
        GLLoader.LoadBindings(new SDL3BindingsContext());
    }

    public void Run()
    {
        var isQuit = false;

        while (!isQuit)
        {
            while (SDL3.SDL_PollEvent(out var sdlEvent))
            {
                switch (sdlEvent.type)
                {
                    case SDL_EventType.Quit:
                        isQuit = true;
                        break;
                }
            }

            _Render();
        }
    }

    private void _Render()
    {
        GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        
        GL.MatrixMode(MatrixMode.Projection);
        GL.LoadIdentity();
        GL.Ortho(0.0, 1920.0, 1080.0, 0.0, -1.0, 1.0);
        
        GL.MatrixMode(MatrixMode.Modelview);
        GL.LoadIdentity();
        
        GL.Color3f(1.0f, 1.0f, 1.0f);
        
        GL.Begin(PrimitiveType.Quads);
        GL.Vertex2f(100.0f, 100.0f);
        GL.Vertex2f(200.0f, 100.0f);
        GL.Vertex2f(200.0f, 200.0f);
        GL.Vertex2f(100.0f, 200.0f);
        GL.End();
        
        SDL3.SDL_GL_SwapWindow(_window);

        var error = GL.GetError();
        if (error != ErrorCode.NoError)
            Console.WriteLine($"Error: {error}");
    }
    
    public void Shutdown()
    {
        SDL3.SDL_GL_DestroyContext(_glContext);
        SDL3.SDL_DestroyWindow(_window);
        SDL3.SDL_Quit();
    }
}