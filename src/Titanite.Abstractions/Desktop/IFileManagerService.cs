namespace Titanite.Abstractions.Desktop;

public enum DirectoryOpenStatus
{
    Opened,

    NotFound,

    Failed
}

public interface IFileManagerService
{
    DirectoryOpenStatus OpenDirectory(string path);
}
