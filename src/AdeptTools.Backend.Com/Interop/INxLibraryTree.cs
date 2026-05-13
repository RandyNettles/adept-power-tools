using System.Runtime.InteropServices;

namespace AdeptTools.Backend.Com.Interop;

/// <summary>
/// COM interop interface for library tree navigation.
/// </summary>
[ComImport]
[Guid("00000000-0000-0000-0000-000000000007")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface INxLibraryTree
{
    int LibraryCount { get; }
    INxLibrary GetLibrary(int index);
}

/// <summary>
/// COM interop interface for a single library node.
/// </summary>
[ComImport]
[Guid("00000000-0000-0000-0000-000000000008")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface INxLibrary
{
    string LibraryId { get; }
    string Name { get; }
    string Path { get; }
    int ChildCount { get; }
    INxLibrary GetChild(int index);
}
