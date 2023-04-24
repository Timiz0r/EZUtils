namespace EZUtils
{
    using System.IO;

    public static class PathUtil
    {
        public static string GetRelative(string root, string path, string newRoot = "")
            => Path
                .Combine(newRoot ?? string.Empty, Path.GetFullPath(path).Substring(Path.GetFullPath(root).Length + 1))
                .Replace("\\", "/");
    }
}
