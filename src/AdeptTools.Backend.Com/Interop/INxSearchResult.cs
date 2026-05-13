using System.Runtime.InteropServices;

namespace AdeptTools.Backend.Com.Interop;

/// <summary>
/// COM interop interface for search results returned by NxDb.Search().
/// </summary>
[ComImport]
[Guid("00000000-0000-0000-0000-000000000004")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface INxSearchResult
{
    int RowCount { get; }
    INxSearchRow GetRow(int index);
}
