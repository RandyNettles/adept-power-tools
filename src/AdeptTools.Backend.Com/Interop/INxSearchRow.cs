using System.Runtime.InteropServices;

namespace AdeptTools.Backend.Com.Interop;

/// <summary>
/// COM interop interface for a single row in search results.
/// </summary>
[ComImport]
[Guid("00000000-0000-0000-0000-000000000005")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface INxSearchRow
{
    int TableNumber { get; }
    string FileId { get; }
    int MajRev { get; }
    int MinRev { get; }
    string GetFieldValue(string fieldName);
}
