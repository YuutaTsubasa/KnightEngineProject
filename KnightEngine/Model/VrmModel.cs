using Newtonsoft.Json.Linq;
using OpenTK.Graphics.OpenGL.Compatibility;
using OpenTK.Mathematics;

namespace KnightEngine.Model;

public class VrmModel
{
    private record NodeInfo(
        int Vao,
        int Vbo,
        int Ebo,
        float[] Vertices,
        int[] Indices,
        float[] Normals,
        Vector3 Translation,
        Quaternion Rotation,
        Vector3 Scale,
        int Index,
        int ParentIndex
    )
    {
        public int VertexCount => Indices.Length;
    }
    
    private readonly int _shaderProgram;
    private readonly NodeInfo[] _nodes;

    public static VrmModel Create(string filePath, int shaderProgram)
    {
        var (gltfJson, binaryBytes) = _LoadVrmGltf(filePath);
        var nodes = _ParseNodes(gltfJson, binaryBytes);
        return new VrmModel(
            shaderProgram,
            nodes.ToArray());
    }
    
    private static (JObject, byte[]) _LoadVrmGltf(string filePath)
    {
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var binaryReader = new BinaryReader(fileStream);

        // 檢查開頭是否為 "glTF"
        var magic = new string(binaryReader.ReadChars(4));
        if (magic != "glTF")
        {
            throw new Exception("Invalid VRM file format: Missing 'glTF' magic header.");
        }

        binaryReader.ReadInt32(); // 版本
        binaryReader.ReadInt32(); // 整體長度

        // 讀取 JSON 區段
        int jsonLength = binaryReader.ReadInt32();
        string jsonType = new string(binaryReader.ReadChars(4));
        if (jsonType != "JSON")
        {
            throw new Exception("Invalid VRM file format: Expected JSON chunk type.");
        }

        // 將 JSON 資料轉為字串並解析成 JObject
        var jsonContent = new string(binaryReader.ReadChars(jsonLength));
        var gltfJson = JObject.Parse(jsonContent);

        // 讀取二進位資料區段
        int binLength = binaryReader.ReadInt32();
        string binType = new string(binaryReader.ReadChars(4));
        if (binType != "BIN\u0000")
        {
            throw new Exception($"Invalid VRM file format: Expected BIN chunk type but got {binType}.");
        }

        // 將二進位內容讀入 byte array
        var binaryBytes = binaryReader.ReadBytes(binLength);
        return (gltfJson, binaryBytes);
    }

    private static List<NodeInfo> _ParseNodes(JObject gltfJson, byte[] binaryBytes)
    {
        var nodesList = new List<NodeInfo>();
        var nodes = gltfJson["nodes"] as JArray;

        if (nodes == null)
            throw new Exception("Invalid GLTF format: nodes section not found.");

        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var translation = node["translation"] != null ? _ParseVector3(node["translation"] ?? throw new InvalidOperationException()) : Vector3.Zero;
            var rotation = node["rotation"] != null ? _ParseQuaternion(node["rotation"] ?? throw new InvalidOperationException()) : Quaternion.Identity;
            var scale = node["scale"] != null ? _ParseVector3(node["scale"] ?? throw new InvalidOperationException()) : Vector3.One;

            var mesh = node["mesh"] != null ? (int)(node["mesh"] ?? throw new InvalidOperationException()) : -1;
            var (vertices, normals, indices) = mesh >= 0 
                ? _LoadMeshVertices(gltfJson, mesh, binaryBytes) 
                : (Array.Empty<float>(), Array.Empty<float>(), Array.Empty<int>());

            // 建立 VAO、VBO 和 EBO
            var (vao, vbo, ebo) = _CreateBufferObjects(vertices, normals, indices);

            var parentIndex = node["parentIndex"]?.Value<int>() ?? -1;

            var nodeInfo = new NodeInfo(
                Vao: vao,
                Vbo: vbo,
                Ebo: ebo,
                Vertices: vertices,
                Indices: indices,
                Normals: normals,
                Translation: translation,
                Rotation: rotation,
                Scale: scale,
                Index: i,
                ParentIndex: parentIndex
            );

            nodesList.Add(nodeInfo);
        }

        return nodesList;
    }

    
    private static Vector3 _ParseVector3(JToken token)
    {
        if (token is not JArray array)
            throw new InvalidOperationException();
        
        return new Vector3(
            (float)array[0],
            (float)array[1],
            (float)array[2]
        );
    }

    private static Quaternion _ParseQuaternion(JToken token)
    {
        if (token is not JArray array)
            throw new InvalidOperationException();
        
        return new Quaternion(
            (float)array[0], 
            (float)array[1], 
            (float)array[2], 
            (float)array[3]
        );
    }

    private static (float[] vertices, float[] normals, int[] indices) _LoadMeshVertices(JObject gltfJson, int meshIndex, byte[] binaryBytes)
    {
        if (gltfJson["meshes"] is not JArray meshes || meshIndex >= meshes.Count)
            throw new Exception("Invalid GLTF format: meshes section not found or mesh index out of range.");

        var mesh = meshes[meshIndex];
        var vertices = new List<float>();
        var normals = new List<float>();
        var indices = new List<int>();

        foreach (var primitive in mesh["primitives"]!)
        {
            // Load POSITION data
            int positionAccessorIndex = (int)(primitive["attributes"]?["POSITION"] ?? throw new InvalidOperationException());
            vertices.AddRange(_LoadAccessorData(gltfJson, positionAccessorIndex, binaryBytes));

            // Load NORMAL data
            int normalAccessorIndex = (int)(primitive["attributes"]?["NORMAL"] ?? throw new InvalidOperationException());
            normals.AddRange(_LoadAccessorData(gltfJson, normalAccessorIndex, binaryBytes));
            
            // Load indices
            int indicesAccessorIndex = (int)(primitive["indices"] ?? throw new InvalidOperationException());
            indices.AddRange(_LoadIndicesData(gltfJson, indicesAccessorIndex, binaryBytes));
        }

        return (vertices.ToArray(), normals.ToArray(), indices.ToArray());
    }

    private static List<float> _LoadAccessorData(JObject gltfJson, int accessorIndex, byte[] binaryBytes)
    {
        var bufferViews = gltfJson["bufferViews"] as JArray;

        if (gltfJson["accessors"] is not JArray accessors || accessorIndex >= accessors.Count)
            throw new Exception("Invalid GLTF format: accessors section not found or accessor index out of range.");
        if (bufferViews == null)
            throw new Exception("Invalid GLTF format: bufferViews section not found.");

        var accessor = accessors[accessorIndex];
        int bufferViewIndex = (int)(accessor["bufferView"] ?? throw new InvalidOperationException());
        int count = (int)(accessor["count"] ?? throw new InvalidOperationException());
        string type = (string)accessor["type"]!;

        var bufferView = bufferViews[bufferViewIndex];
        int byteOffset = bufferView["byteOffset"]?.Value<int>() ?? 0;
        int byteLength = (int)(bufferView["byteLength"] ?? throw new InvalidOperationException());
        int byteStride = bufferView["byteStride"]?.Value<int>() ?? 0;

        int typeCount = type switch
        {
            "SCALAR" => 1,
            "VEC2" => 2,
            "VEC3" => 3,
            "VEC4" => 4,
            _ => throw new Exception($"Unsupported accessor type: {type}")
        };

        var result = new List<float>(count * typeCount);
        for (int i = 0; i < count; i++)
        {
            int offset = byteOffset + i * (byteStride > 0 ? byteStride : typeCount * sizeof(float));
            for (int j = 0; j < typeCount; j++)
            {
                float value = BitConverter.ToSingle(binaryBytes, offset + j * sizeof(float));
                result.Add(value);
            }
        }

        return result;
    }
    
    private static List<int> _LoadIndicesData(JObject gltfJson, int accessorIndex, byte[] binaryBytes)
    {
        var accessors = gltfJson["accessors"] as JArray;
        var bufferViews = gltfJson["bufferViews"] as JArray;

        if (accessors == null || accessorIndex >= accessors.Count)
            throw new Exception("Invalid GLTF format: accessors section not found or accessor index out of range.");
        if (bufferViews == null)
            throw new Exception("Invalid GLTF format: bufferViews section not found.");

        var accessor = accessors[accessorIndex];
        int bufferViewIndex = (int)(accessor["bufferView"] ?? throw new InvalidOperationException());
        int count = (int)(accessor["count"] ?? throw new InvalidOperationException());
        int componentType = (int)(accessor["componentType"] ?? throw new InvalidOperationException());
        var bufferView = bufferViews[bufferViewIndex];
        int byteOffset = bufferView["byteOffset"]?.Value<int>() ?? 0;
        int byteStride = bufferView["byteStride"]?.Value<int>() ?? 0;

        var result = new List<int>(count);
        int componentSize = componentType switch
        {
            5123 => sizeof(ushort), // GL_UNSIGNED_SHORT
            5125 => sizeof(uint),   // GL_UNSIGNED_INT
            _ => throw new Exception($"Unsupported component type for indices: {componentType}")
        };

        for (int i = 0; i < count; i++)
        {
            int offset = byteOffset + i * (byteStride > 0 ? byteStride : componentSize);
            int index = componentType == 5123
                ? BitConverter.ToUInt16(binaryBytes, offset)
                : (int)BitConverter.ToUInt32(binaryBytes, offset);
            result.Add(index);
        }

        return result;
    }
    
    private static (int Vao, int Vbo, int Ebo) _CreateBufferObjects(
        float[] vertices, 
        float[] normals,
        int[] indices)
    {
        int vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);
        
        int ebo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(int), indices, BufferUsage.StaticDraw);
        
        int vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsage.StaticDraw);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);

        int nbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, nbo);
        GL.BufferData(BufferTarget.ArrayBuffer, normals.Length * sizeof(float), normals, BufferUsage.StaticDraw);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        GL.EnableVertexAttribArray(1);

        GL.BindVertexArray(0);
        return (vao, vbo, ebo);
    }
    
    public void Render(float time)
    {
        GL.UseProgram(_shaderProgram);

        foreach (var node in _nodes)
        {
            GL.BindVertexArray(node.Vao);

            var modelMatrix = _GetNodeModelMatrix(node);
            _SetModelMatrixUniform(_shaderProgram, modelMatrix, time);

            GL.DrawElements(PrimitiveType.Triangles, node.VertexCount, DrawElementsType.UnsignedInt, 0);

            GL.BindVertexArray(0);
        }
    }

    private Matrix4 _CalculateModelMatrix(NodeInfo node)
    {
        // 1. 建立平移矩陣
        var translationMatrix = Matrix4.CreateTranslation(node.Translation);

        // 2. 建立旋轉矩陣（將 Quaternion 轉換成旋轉矩陣）
        var rotationMatrix = Matrix4.CreateFromQuaternion(node.Rotation);

        // 3. 建立縮放矩陣
        var scaleMatrix = Matrix4.CreateScale(node.Scale);

        // 4. 將縮放、旋轉和平移矩陣相乘
        // 注意：矩陣乘法順序為 scale -> rotate -> translate
        return scaleMatrix * rotationMatrix * translationMatrix;
    }

    private Matrix4 _GetNodeModelMatrix(NodeInfo node)
    {
        var modelMatrix = _CalculateModelMatrix(node);
        if (node.ParentIndex != -1)
            modelMatrix *= _GetNodeModelMatrix(_nodes[node.ParentIndex]);

        return modelMatrix;
    }

    private void _SetModelMatrixUniform(int shaderProgram, Matrix4 modelMatrix, float time)
    {
        // 取得 uniform 變量的位置
        var modelLoc = GL.GetUniformLocation(shaderProgram, "model");

        // 時間為基礎的旋轉（例如簡單動畫效果）
        var rotationMatrix = Matrix4.CreateRotationY(time);

        // 最終模型矩陣：將基礎模型矩陣與旋轉相乘
        var finalModelMatrix = modelMatrix * rotationMatrix;

        // 將矩陣數據傳遞給著色器
        var matrixData = new[]
        {
            finalModelMatrix.M11, finalModelMatrix.M12, finalModelMatrix.M13, finalModelMatrix.M14,
            finalModelMatrix.M21, finalModelMatrix.M22, finalModelMatrix.M23, finalModelMatrix.M24,
            finalModelMatrix.M31, finalModelMatrix.M32, finalModelMatrix.M33, finalModelMatrix.M34,
            finalModelMatrix.M41, finalModelMatrix.M42, finalModelMatrix.M43, finalModelMatrix.M44
        };

        unsafe
        {
            fixed (float* matrixPtr = matrixData)
            {
                GL.UniformMatrix4fv(modelLoc, 1, false, matrixPtr);
            }
        }
    }
    
    private VrmModel(int shaderProgram, NodeInfo[] nodes)
    {
        _shaderProgram = shaderProgram;
        _nodes = nodes;
    }
}