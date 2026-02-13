namespace BombasticIFC.Shared.Constants;

/// <summary>
/// Application-wide constants
/// </summary>
public static class AppConstants
{
    public const string ApplicationName = "BombasticIFC Cluster";
    public const string ApiVersion = "v1";
    
    public static class FileExtensions
    {
        public const string Ifc = ".ifc";
        public const string Xkt = ".xkt";
        public const string Gltf = ".gltf";
        public const string Glb = ".glb";
        public const string Json = ".json";
    }

    public static class Limits
    {
        public const long MaxFileSize = 500 * 1024 * 1024; // 500 MB
        public const int MaxConcurrentConversions = 5;
    }
}
