using KnightEngine.Model;
using KnightEngine.OpenTK;
using Newtonsoft.Json.Linq;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL.Compatibility;
using OpenTK.Mathematics;
using StbImageSharp;

namespace KnightEngine;
using SDL3;

public class KnightEngine
{
    private SDL_Window _window;
    private SDL_GLContext _glContext;
    private int _shaderProgram;
    private int _vbo;
    private int _vao;
    private int _texture;
    private VrmModel _vrmModel;
    
    public KnightEngine(string windowTitle, int windowWidth, int windowHeight)
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
        
        GLLoader.LoadBindings(new SDL3BindingsContext());

        GL.Enable(EnableCap.DepthTest);

        _LoadShaders();
        _vrmModel = VrmModel.Create("Resources/Models/4.0.vrm", _shaderProgram);
        //_CreateBufferObjects();
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
        
        GL.Uniform3f(GL.GetUniformLocation(_shaderProgram, "lightPos"), 1.2f, 1.0f, 2.0f);
        GL.Uniform3f(GL.GetUniformLocation(_shaderProgram, "viewPos"), 0.0f, 0.8f, 3.0f);
        GL.Uniform3f(GL.GetUniformLocation(_shaderProgram, "lightColor"), 1.0f, 1.0f, 1.0f);
        GL.Uniform3f(GL.GetUniformLocation(_shaderProgram, "objectColor"), 0.5f, 0.5f, 1f);
        
        var view = Matrix4.LookAt(new Vector3(0.0f, 0.8f, 3.0f), new Vector3(0.0f, 0.8f, 0.0f), Vector3.UnitY);
        var projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45.0f), 1920f / 1080f, 0.1f, 100.0f);

        int viewLoc = GL.GetUniformLocation(_shaderProgram, "view");
        int projectionLoc = GL.GetUniformLocation(_shaderProgram, "projection");

        unsafe
        {
            fixed (float* viewPtr = view.ToFloatArray())
            fixed (float* projectionPtr = projection.ToFloatArray())
            {
                GL.UniformMatrix4fv(viewLoc, 1, false, viewPtr);
                GL.UniformMatrix4fv(projectionLoc, 1, false, projectionPtr);
            }
        }
        
        var time = SDL3.SDL_GetTicks() / 1000.0f;
        _vrmModel.Render(time);

        SDL3.SDL_GL_SwapWindow(_window);

        var error = GL.GetError();
        if (error != ErrorCode.NoError)
            Console.WriteLine($"Error: {error}");
    }

    private void _LoadShaders()
    {
        var vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, File.ReadAllText("Resources/Shaders/default_vertex_shader.glsl"));
        GL.CompileShader(vertexShader);

        GL.GetShaderi(vertexShader, ShaderParameterName.CompileStatus, out var vertexCompileStatus);
        if (vertexCompileStatus == 0)
        {
            GL.GetShaderInfoLog(vertexShader, out var infoLog);
            throw new Exception($"Error compiling vertex shader: {infoLog}");
        }
        
        var fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, File.ReadAllText("Resources/Shaders/default_fragment_shader.glsl"));
        GL.CompileShader(fragmentShader);
        
        GL.GetShaderi(fragmentShader, ShaderParameterName.CompileStatus, out var fragmentCompileStatus);
        if (fragmentCompileStatus == 0)
        {
            GL.GetShaderInfoLog(fragmentShader, out var infoLog);
            throw new Exception($"Error compiling fragment shader: {infoLog}");
        }
        
        _shaderProgram = GL.CreateProgram();
        GL.AttachShader(_shaderProgram, vertexShader);
        GL.AttachShader(_shaderProgram, fragmentShader);
        GL.LinkProgram(_shaderProgram);
        
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);
    }

    private void _CreateBufferObjects()
    {
        var vertices = new []
        {
            // 顏色          // 位置            // 紋理座標
            // 前面 (兩個三角形)
            1.0f, 0.0f, 0.0f,   -0.5f, -0.5f,  0.5f,   0.0f, 0.0f,  // 左下
            0.0f, 1.0f, 0.0f,    0.5f, -0.5f,  0.5f,   1.0f, 0.0f,  // 右下
            0.0f, 0.0f, 1.0f,    0.5f,  0.5f,  0.5f,   1.0f, 1.0f,  // 右上

            1.0f, 0.0f, 0.0f,   -0.5f, -0.5f,  0.5f,   0.0f, 0.0f,  // 左下
            0.0f, 0.0f, 1.0f,    0.5f,  0.5f,  0.5f,   1.0f, 1.0f,  // 右上
            1.0f, 1.0f, 0.0f,   -0.5f,  0.5f,  0.5f,   0.0f, 1.0f,  // 左上

            // 後面 (兩個三角形)
            1.0f, 0.0f, 1.0f,   -0.5f, -0.5f, -0.5f,   0.0f, 0.0f,  // 左下
            0.0f, 1.0f, 1.0f,    0.5f, -0.5f, -0.5f,   1.0f, 0.0f,  // 右下
            0.0f, 0.0f, 1.0f,    0.5f,  0.5f, -0.5f,   1.0f, 1.0f,  // 右上

            1.0f, 0.0f, 1.0f,   -0.5f, -0.5f, -0.5f,   0.0f, 0.0f,  // 左下
            0.0f, 0.0f, 1.0f,    0.5f,  0.5f, -0.5f,   1.0f, 1.0f,  // 右上
            1.0f, 1.0f, 1.0f,   -0.5f,  0.5f, -0.5f,   0.0f, 1.0f,  // 左上

            // 左面 (兩個三角形)
            1.0f, 1.0f, 0.0f,   -0.5f,  0.5f,  0.5f,   0.0f, 1.0f,  // 左上
            1.0f, 1.0f, 1.0f,   -0.5f,  0.5f, -0.5f,   1.0f, 1.0f,  // 右上
            0.0f, 1.0f, 0.0f,   -0.5f, -0.5f, -0.5f,   1.0f, 0.0f,  // 右下

            1.0f, 1.0f, 0.0f,   -0.5f,  0.5f,  0.5f,   0.0f, 1.0f,  // 左上
            0.0f, 1.0f, 0.0f,   -0.5f, -0.5f, -0.5f,   1.0f, 0.0f,  // 右下
            0.0f, 1.0f, 1.0f,   -0.5f, -0.5f,  0.5f,   0.0f, 0.0f,  // 左下

            // 右面 (兩個三角形)
            0.0f, 1.0f, 0.0f,    0.5f, -0.5f, -0.5f,   1.0f, 0.0f,  // 右下
            1.0f, 0.0f, 0.0f,    0.5f,  0.5f, -0.5f,   1.0f, 1.0f,  // 右上
            1.0f, 1.0f, 0.0f,    0.5f,  0.5f,  0.5f,   0.0f, 1.0f,  // 左上

            0.0f, 1.0f, 0.0f,    0.5f, -0.5f, -0.5f,   1.0f, 0.0f,  // 右下
            1.0f, 1.0f, 1.0f,    0.5f, -0.5f,  0.5f,   0.0f, 0.0f,  // 左下
            1.0f, 1.0f, 0.0f,    0.5f,  0.5f,  0.5f,   0.0f, 1.0f,  // 左上

            // 上面 (兩個三角形)
            0.0f, 0.0f, 1.0f,   -0.5f,  0.5f, -0.5f,   0.0f, 0.0f,  // 左下
            1.0f, 1.0f, 1.0f,    0.5f,  0.5f, -0.5f,   1.0f, 0.0f,  // 右下
            0.0f, 1.0f, 1.0f,    0.5f,  0.5f,  0.5f,   1.0f, 1.0f,  // 右上

            0.0f, 0.0f, 1.0f,   -0.5f,  0.5f, -0.5f,   0.0f, 0.0f,  // 左下
            1.0f, 1.0f, 0.0f,   -0.5f,  0.5f,  0.5f,   0.0f, 1.0f,  // 左上
            0.0f, 1.0f, 1.0f,    0.5f,  0.5f,  0.5f,   1.0f, 1.0f,  // 右上

            // 下面 (兩個三角形)
            1.0f, 0.0f, 1.0f,   -0.5f, -0.5f, -0.5f,   0.0f, 0.0f,  // 左下
            0.0f, 1.0f, 0.0f,    0.5f, -0.5f, -0.5f,   1.0f, 0.0f,  // 右下
            0.0f, 1.0f, 1.0f,    0.5f, -0.5f,  0.5f,   1.0f, 1.0f,  // 右上

            1.0f, 0.0f, 1.0f,   -0.5f, -0.5f, -0.5f,   0.0f, 0.0f,  // 左下
            1.0f, 1.0f, 1.0f,   -0.5f, -0.5f,  0.5f,   0.0f, 1.0f,  // 左上
            0.0f, 1.0f, 1.0f,    0.5f, -0.5f,  0.5f,   1.0f, 1.0f,  // 右上
        };
        
        _vao = GL.GenVertexArray();
        GL.BindVertexArray(_vao);
        
        _vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsage.StaticDraw);
        
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 3 * sizeof(float));
        GL.EnableVertexAttribArray(0);
        
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
        GL.EnableVertexAttribArray(1);
        
        GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), 6 * sizeof(float));
        GL.EnableVertexAttribArray(2);
    }

    private void _LoadTexture(string filePath)
    {
        var image = ImageResult.FromStream(File.OpenRead(filePath), ColorComponents.RedGreenBlueAlpha);

        _texture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2d, _texture);
        
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        
        GL.TexImage2D(TextureTarget.Texture2d, 0, InternalFormat.Rgba, image.Width, image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, image.Data);
        GL.GenerateMipmap(TextureTarget.Texture2d);
    }
    
    public void Shutdown()
    {
        SDL3.SDL_GL_DestroyContext(_glContext);
        SDL3.SDL_DestroyWindow(_window);
        SDL3.SDL_Quit();
    }
}