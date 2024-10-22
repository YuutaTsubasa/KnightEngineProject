using KnightEngine.Utils;

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

        SDL3.SDL_GL_SetAttribute(SDL_GLattr.ContextMajorVersion, 3);
        SDL3.SDL_GL_SetAttribute(SDL_GLattr.ContextMinorVersion, 3);
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

            SDL3.SDL_GL_SwapWindow(_window);
        }
    }

    public void Shutdown()
    {
        SDL3.SDL_GL_DestroyContext(_glContext);
        SDL3.SDL_DestroyWindow(_window);
        SDL3.SDL_Quit();
    }
}