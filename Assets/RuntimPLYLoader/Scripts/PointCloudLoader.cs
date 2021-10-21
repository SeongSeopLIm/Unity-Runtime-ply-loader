// Point Cloud Binary Viewer DX11 for runtime parsing
// http://unitycoder.com

using UnityEngine;
using System.Collections;
using Debug = UnityEngine.Debug;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniRx;
using Thread = System.Threading.Thread;
#if !UNITY_SAMSUNGTV && !UNITY_WEBGL
using System.Threading;
using System.IO;
#endif

enum DataProperty
{
    Invalid,
    R8, G8, B8, A8,
    R16, G16, B16, A16,
    SingleX, SingleY, SingleZ,
    SingleNX, SingleNY, SingleNZ,
    DoubleX, DoubleY, DoubleZ,
    Data8, Data16, Data32, Data64
}
class DataHeader
{
    public List<DataProperty> properties = new List<DataProperty>();
    public long vertexCount = -1;
}

public class PointCloudInfo
{
    public Vector3 Min { get; private set; }
    public Vector3 Max { get; private set; }
    public Vector3 Center => (Min + Max) / 2;
    public Vector3 BotttomCenter => new Vector3(Center.x, Min.y, Center.z);
    public Vector3 YSideCenter => new Vector3(Center.x, 0, Center.z);
    public Vector3 Size => Max - Min;
    public Bounds bound => new Bounds(Center, Size);
    public bool IsSetted => Min != Vector3.zero && Max != Vector3.zero;

    public PointCloudInfo()
    {
        Max = Vector3.zero;
        Min = Vector3.zero;
    }
    public PointCloudInfo(Vector3 min, Vector3 max)
    {
        Max = max;
        Min = min;
    }
}

[Serializable]
public class PointCloudSetting
{
    public ReactiveProperty<bool> isLoaded = new ReactiveProperty<bool>(false);

    public ReactiveProperty<float> pointSize = new ReactiveProperty<float>(0.01f);
    public ReactiveProperty<float> minVisibleHeight = new ReactiveProperty<float>(-10000);
    public ReactiveProperty<float> maxVisibleHeight = new ReactiveProperty<float>(10000);

    public ReactiveProperty<Vector3> position = new ReactiveProperty<Vector3>(Vector3.zero);
    public ReactiveProperty<Vector3> rotation = new ReactiveProperty<Vector3>(Vector3.zero);
    public ReactiveProperty<Vector3> scale = new ReactiveProperty<Vector3>(Vector3.one);
}

/// <summary>
/// 포인트 클라우드 에셋 RuntimeViewerDX11의 수정버전입니다.
/// </summary>
public class PointCloudLoader : MonoBehaviour
{
    public Action<float, string> onProgressChange;
    public Action onLoadComplete;

    [SerializeField] private Material targetMaterial;
    [Header("Load ply when it start if you wrrite the path that streaming asset")]
    [SerializeField] private string streamingPath = "";
    private PointCloudInfo pointCloudInfo = new PointCloudInfo();
    private PointCloudSetting mapSetting = new PointCloudSetting();
    private PointOctree<Vector3> pointOctree;
    private ReactiveProperty<float> loadProgress = new ReactiveProperty<float>(0.0f);
    private ComputeBuffer bufferPoints;
    private ComputeBuffer bufferColors;
    private CommandBuffer depthBuffer;
    private Material material;
    private Vector3[] originPoints;
    private Vector3[] originColors;
    private string[] progressTitles = { "reading", "generating", "calculating octree" };
    private int progressTitleIndex = 0;
    private int pointCount = 0; 
    [HideInInspector] public ReactiveProperty<bool> isLoading = new ReactiveProperty<bool>(false);
    [HideInInspector] public ReactiveProperty<bool> isVisible = new ReactiveProperty<bool>(true);
    [HideInInspector] public ReactiveProperty<bool> isRenderMainCameraOnly = new ReactiveProperty<bool>(true);
    public CameraEvent depthCameraEvent = CameraEvent.AfterDepthTexture;

    private Camera Camera => Camera.main;

    public string ProgressTitle => progressTitles[progressTitleIndex];
    public ReactiveProperty<float> LoadProgress => loadProgress; 
    public float PointSize
    {
        get
        {
            return PointCloudSetting.pointSize.Value;
        }
        set
        {
            PointCloudSetting.pointSize.SetValueAndForceNotify(value);
            material.SetFloat("_Size", PointCloudSetting.pointSize.Value);
        }
    }
    public float MinYRange
    {
        get => PointCloudSetting.minVisibleHeight.Value;
        set
        {
            PointCloudSetting.minVisibleHeight.SetValueAndForceNotify(value);
            material.SetFloat("_MinHeight", value);
        }
    }
    public float MaxYRange
    {
        get => PointCloudSetting.maxVisibleHeight.Value;
        set
        {
            PointCloudSetting.maxVisibleHeight.SetValueAndForceNotify(value);
            material.SetFloat("_MaxHeight", value);
        }
    }

    public Quaternion Rotation
    {
        get => Quaternion.Euler(PointCloudSetting.rotation.Value);
        private set
        {
            var rot = Matrix4x4.Rotate(value);
            material.SetMatrix("_RotMatrix", rot.inverse);
        }
    }
    public Vector3 Position
    {
        get => PointCloudSetting.position.Value;
        private set
        {
            material.SetVector("_Offset", value);
        }
    }
    public Vector3 Scale
    {
        get => PointCloudSetting.scale.Value;
        private set
        {
            material.SetMatrix("_ScaleMatrix", Matrix4x4.Scale(value));
        }
    }

    public PointOctree<Vector3> PointOctree { get => pointOctree; private set => pointOctree = value; }
    public PointCloudInfo PointCloudInfo { get => pointCloudInfo; private set => pointCloudInfo = value; }
    public PointCloudSetting PointCloudSetting { get => mapSetting; set => mapSetting = value; }

     

    private void Start()
    {
        Initalize();
    }

    private void Initalize()
    {
        //Instantiate Utility object
        var utility = Utility.Instance;
        LoadProgress.Value = 0;
        material = new Material(targetMaterial);

        depthBuffer = new CommandBuffer();
        Camera.AddCommandBuffer(depthCameraEvent, depthBuffer);

        if(streamingPath != "")
        {
            var fullPath = System.IO.Path.Combine(Application.streamingAssetsPath, streamingPath);
            LoadPlyAsync(fullPath);
        }

        Subscrible();
    }


    void Subscrible()
    {
        PointCloudSetting.pointSize
            .DistinctUntilChanged()
            .Subscribe(x => PointSize = x);
        PointCloudSetting.minVisibleHeight
            .DistinctUntilChanged()
            .Subscribe(x => MinYRange = x);
        PointCloudSetting.maxVisibleHeight
            .DistinctUntilChanged()
            .Subscribe(x => MaxYRange = x);
        PointCloudSetting.position
            .DistinctUntilChanged()
            .Subscribe(x => Position = x);
        PointCloudSetting.rotation
            .DistinctUntilChanged()
            .Subscribe(x => Rotation = Quaternion.Euler(x));
        PointCloudSetting.scale
            .DistinctUntilChanged()
            .Subscribe(x => Scale = x);
    }

     

    public Task LoadPlyAsync(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"Faild Load. Path : {path}");
            return Task.CompletedTask;
        }
        LoadPly(path);
        return Task.CompletedTask;// Task.Run(() => LoadPly(path)); 
    }

    public void LoadPly(System.Object path)
    {
        var fullPath = (string)path;
        LoadProgress.Value = 0;
        progressTitleIndex = 0;

        Utility.Instance.AddMainTask(new Utility.MainTask(0.0f, () =>
        {
            PointCloudSetting.isLoaded.Value = false;
            isLoading.Value = true;
        }));
        

        try
        {
            var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var header = ReadDataHeader(new StreamReader(stream));
              
            pointCount = (int)header.vertexCount;
            originPoints = new Vector3[pointCount];
            originColors = new Vector3[pointCount];

            ReadDataBody(header, new BinaryReader(stream));
            GenerateOctree();

            Utility.Instance.AddMainTask(new Utility.MainTask(0.0f, () =>
            {
                isLoading.Value = true;
            }));

            Utility.Instance.AddMainTask(new Utility.MainTask(0.0f, ApplyBuffer));

            return;
        }
        catch (Exception error)
        {
            Debug.LogError($"Failed load. path : {path} msg : {error.Message}");
        }
    }


    DataHeader ReadDataHeader(StreamReader reader)
    {
        var data = new DataHeader();
        var readCount = 0;

        var line = reader.ReadLine();

        int newLineBytes = System.Environment.NewLine.Length;

        readCount += line.Length + newLineBytes;
        if (line != "ply")
            throw new ArgumentException("Magic number ('ply') mismatch.");

        line = reader.ReadLine();
        readCount += line.Length + newLineBytes;
        if (line != "format binary_little_endian 1.0")
            throw new ArgumentException(
                "Invalid data format ('" + line + "'). " +
                "Should be binary/little endian.");

        for (var skip = false; ;)
        {
            // Read a line and split it with white space.
            line = reader.ReadLine();
            readCount += line.Length + newLineBytes;
            if (line == "end_header") break;
            var col = line.Split();

            // Element declaration (unskippable)
            if (col[0] == "element")
            {
                if (col[1] == "vertex")
                {
                    data.vertexCount = Convert.ToInt32(col[2]);
                    skip = false;
                }
                else
                {
                    // Don't read elements other than vertices.
                    skip = true;
                }
            }

            if (skip) continue;

            // Property declaration line
            if (col[0] == "property")
            {
                var prop = DataProperty.Invalid;

                // Parse the property name entry.
                switch (col[2])
                {
                    case "red": prop = DataProperty.R8; break;
                    case "green": prop = DataProperty.G8; break;
                    case "blue": prop = DataProperty.B8; break;
                    case "alpha": prop = DataProperty.A8; break;
                    case "x": prop = DataProperty.SingleX; break;
                    case "y": prop = DataProperty.SingleY; break;
                    case "z": prop = DataProperty.SingleZ; break;
                    case "nx": prop = DataProperty.SingleNX; break;
                    case "ny": prop = DataProperty.SingleNY; break;
                    case "nz": prop = DataProperty.SingleNZ; break;
                }

                // Check the property type.
                if (col[1] == "char" || col[1] == "uchar" ||
                    col[1] == "int8" || col[1] == "uint8")
                {
                    if (prop == DataProperty.Invalid)
                        prop = DataProperty.Data8;
                    else if (GetPropertySize(prop) != 1)
                        throw new ArgumentException("Invalid property type ('" + line + "').");
                }
                else if (col[1] == "short" || col[1] == "ushort" ||
                         col[1] == "int16" || col[1] == "uint16")
                {
                    switch (prop)
                    {
                        case DataProperty.Invalid: prop = DataProperty.Data16; break;
                        case DataProperty.R8: prop = DataProperty.R16; break;
                        case DataProperty.G8: prop = DataProperty.G16; break;
                        case DataProperty.B8: prop = DataProperty.B16; break;
                        case DataProperty.A8: prop = DataProperty.A16; break;
                    }
                    if (GetPropertySize(prop) != 2)
                        throw new ArgumentException("Invalid property type ('" + line + "').");
                }
                else if (col[1] == "int" || col[1] == "uint" || col[1] == "float" ||
                         col[1] == "int32" || col[1] == "uint32" || col[1] == "float32")
                {
                    if (prop == DataProperty.Invalid)
                        prop = DataProperty.Data32;
                    else if (GetPropertySize(prop) != 4)
                        throw new ArgumentException("Invalid property type ('" + line + "').");
                }
                else if (col[1] == "int64" || col[1] == "uint64" ||
                         col[1] == "double" || col[1] == "float64")
                {
                    switch (prop)
                    {
                        case DataProperty.Invalid: prop = DataProperty.Data64; break;
                        case DataProperty.SingleX: prop = DataProperty.DoubleX; break;
                        case DataProperty.SingleY: prop = DataProperty.DoubleY; break;
                        case DataProperty.SingleZ: prop = DataProperty.DoubleZ; break;
                    }
                    if (GetPropertySize(prop) != 8)
                        throw new ArgumentException("Invalid property type ('" + line + "').");
                }
                else
                {
                    throw new ArgumentException("Unsupported property type ('" + line + "').");
                }

                data.properties.Add(prop);
            }
        }

        System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.GetField;
        int charPos = (int)reader.GetType().InvokeMember("charPos", flags, null, reader, null);


        reader.BaseStream.Position = charPos;

        return data;
    }


    void ReadDataBody(DataHeader header, BinaryReader reader)
    {
        float x = 0, y = 0, z = 0;
        float nx = 0, ny = 0, nz = 0;
        float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
        UInt16 r = 255, g = 255, b = 255, a = 255;


        progressTitleIndex = 1; 
        pointCount = (int)header.vertexCount;
        originPoints = new Vector3[pointCount];
        originColors = new Vector3[pointCount];

        for (int i = 0; i < header.vertexCount; i++)
        {
            LoadProgress.Value = (float)((float)i / (float)header.vertexCount);
            foreach (var prop in header.properties)
            {
                switch (prop)
                {
                    case DataProperty.R8: r = reader.ReadByte(); break;
                    case DataProperty.G8: g = reader.ReadByte(); break;
                    case DataProperty.B8: b = reader.ReadByte(); break;
                    case DataProperty.A8: a = reader.ReadByte(); break;

                    case DataProperty.R16: r = ((ushort)(reader.ReadUInt16() >> 8)); break;
                    case DataProperty.G16: g = ((ushort)(reader.ReadUInt16() >> 8)); break;
                    case DataProperty.B16: b = ((ushort)(reader.ReadUInt16() >> 8)); break;
                    case DataProperty.A16: a = ((ushort)(reader.ReadUInt16() >> 8)); break;

                    case DataProperty.SingleX: x = reader.ReadSingle(); break;
                    case DataProperty.SingleY: y = reader.ReadSingle(); break;
                    case DataProperty.SingleZ: z = reader.ReadSingle(); break;

                    case DataProperty.SingleNX: nx = reader.ReadSingle(); break;
                    case DataProperty.SingleNY: ny = reader.ReadSingle(); break;
                    case DataProperty.SingleNZ: nz = reader.ReadSingle(); break;

                    case DataProperty.DoubleX: x = (float)reader.ReadDouble(); break;
                    case DataProperty.DoubleY: y = (float)reader.ReadDouble(); break;
                    case DataProperty.DoubleZ: z = (float)reader.ReadDouble(); break;

                    case DataProperty.Data8: reader.ReadByte(); break;
                    case DataProperty.Data16: reader.BaseStream.Position += 2; break;
                    case DataProperty.Data32: reader.BaseStream.Position += 4; break;
                    case DataProperty.Data64: reader.BaseStream.Position += 8; break;
                }

            }


            minX = x < minX ? x : minX;
            minY = y < minY ? y : minY;
            minZ = z < minZ ? z : minZ;
            maxX = x > maxX ? x : maxX;
            maxY = y > maxY ? y : maxY;
            maxZ = z > maxZ ? z : maxZ;

            originPoints[i].Set(x, y, z);
            originColors[i].Set((float)r / 255.0f, (float)g / 255.0f, (float)b / 255.0f);
        }

        PointCloudInfo = new PointCloudInfo(new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, maxZ));
        Utility.Instance.AddMainTask(new Utility.MainTask(0.0f, () =>
        {

            //PointCloudSetting.minVisibleHeight.Value = PointCloudInfo.Min.y - 0.1f;
            //PointCloudSetting.maxVisibleHeight.Value = PointCloudInfo.Max.y + 0.1f;
        }));
    }
     
    void GenerateOctree()
    {
        progressTitleIndex = 2;
        var size = Mathf.Abs(PointCloudInfo.Max.x - PointCloudInfo.Min.x);
        PointOctree = new PointOctree<Vector3>(size, Vector3.zero, 1);

        for (int i = 0; i < originPoints.Length; i++)
        {
            LoadProgress.Value = (float)((float)i / (float)originPoints.Length);
            PointOctree.Add(originPoints[i], originPoints[i]);
        }

    }
 
    static int GetPropertySize(DataProperty p)
    {
        switch (p)
        {
            case DataProperty.R8: return 1;
            case DataProperty.G8: return 1;
            case DataProperty.B8: return 1;
            case DataProperty.A8: return 1;
            case DataProperty.R16: return 2;
            case DataProperty.G16: return 2;
            case DataProperty.B16: return 2;
            case DataProperty.A16: return 2;
            case DataProperty.SingleX: return 4;
            case DataProperty.SingleY: return 4;
            case DataProperty.SingleZ: return 4;
            case DataProperty.SingleNX: return 4;
            case DataProperty.SingleNY: return 4;
            case DataProperty.SingleNZ: return 4;
            case DataProperty.DoubleX: return 8;
            case DataProperty.DoubleY: return 8;
            case DataProperty.DoubleZ: return 8;
            case DataProperty.Data8: return 1;
            case DataProperty.Data16: return 2;
            case DataProperty.Data32: return 4;
            case DataProperty.Data64: return 8;
        }
        return 0;
    }

    public bool SearchNearestPoint(Ray worldRay, out Vector3 worldPos, out Vector3 localPos, float octreeDistance = 0.05f)
    {
        localPos = Vector3.zero;
        worldPos = Vector3.zero;


        if (PointOctree == null)
            return false;


        var root = new GameObject();
        var pivot = new GameObject();
        var dir = new GameObject();

        root.transform.position = PointCloudSetting.position.Value;
        root.transform.rotation = Quaternion.Euler(PointCloudSetting.rotation.Value);
        root.transform.localScale = PointCloudSetting.scale.Value;
        pivot.transform.position = worldRay.origin;
        dir.transform.position = worldRay.origin + worldRay.direction;

        root.transform.parent = transform;
        pivot.transform.parent = root.transform;
        dir.transform.parent = pivot.transform;

        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        var direction = (dir.transform.position - pivot.transform.position).normalized;
        var localRay = new Ray(pivot.transform.position, direction);

        Destroy(dir);
        Destroy(pivot);
        Destroy(root);

        var points = PointOctree.GetNearby(localRay, octreeDistance);

        if (points.Length <= 0)
            return false;

        float distance = float.MaxValue;
        foreach (var point in points)
        {
            var calcDistance = Vector3.SqrMagnitude(localRay.origin - point);
            if (distance < calcDistance)
                continue;
            distance = calcDistance;
            localPos = point;
        }


        root = new GameObject();
        pivot = new GameObject();

        pivot.transform.position = localPos;

        root.transform.parent = transform;
        pivot.transform.parent = root.transform;

        root.transform.position = PointCloudSetting.position.Value;
        root.transform.rotation = Quaternion.Euler(PointCloudSetting.rotation.Value);
        root.transform.localScale = PointCloudSetting.scale.Value;

        worldPos = pivot.transform.position;
        localPos = pivot.transform.localPosition;

        Destroy(pivot);
        Destroy(root);
        return true;
    }

    void OnDrawGizmos()
    {
         
        /* 
        if (PointOctree == null)
            return;
        PointOctree.DrawAllBounds(); // Draw node boundaries 
        */
    } 

    public void ApplyBuffer()
    {
        if (pointCount == 0) 
            return;
         
        ReleaseBuffers();  
         
        int resultPointCount = originPoints.Length;
        Vector3[] resultPoints = originPoints;
        Vector3[] resultColors = originColors; 

        bufferPoints = new ComputeBuffer(resultPointCount, 12);
        bufferPoints.SetData(resultPoints);
        material.SetBuffer("buf_Points", bufferPoints);

        if (bufferColors != null) bufferColors.Dispose();
        bufferColors = new ComputeBuffer(resultPointCount, 12);
        bufferColors.SetData(resultColors);
        material.SetBuffer("buf_Colors", bufferColors);


        depthBuffer.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Points, pointCount, 1);

        //apply material transform
        PointCloudSetting.position.Value = Position;
        PointCloudSetting.rotation.Value = Rotation.eulerAngles;
        PointCloudSetting.scale.Value = Scale;
        PointCloudSetting.isLoaded.Value = true; 
    }


    void ReleaseBuffers()
    {
        if (bufferPoints != null) 
            bufferPoints.Release();
        if (bufferColors != null) 
            bufferColors.Release();

        bufferPoints = null;
        bufferColors = null;
    }
    void OnDestroy()
    {
        ReleaseBuffers();

        originPoints = new Vector3[0];
        originColors = new Vector3[0]; 
    }

    // mainloop, for displaying the points
    //	void OnPostRender () // < works also if attached to camera
    void OnRenderObject()
    { 
        material.SetPass(0);
        Graphics.DrawProceduralNow(MeshTopology.Points, pointCount); 
    }

} 