using OpenTK;

namespace KnightEngine.OpenTK;

public unsafe class SDL3BindingsContext : IBindingsContext
{
    public IntPtr GetProcAddress(string procName)
        => new (SDL3.SDL3.SDL_GL_GetProcAddress(procName));
}