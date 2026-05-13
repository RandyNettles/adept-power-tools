using System.Runtime.InteropServices;

namespace AdeptTools.Backend.Com.Interop;

/// <summary>
/// COM interop interface for data card read/write operations.
/// </summary>
[ComImport]
[Guid("00000000-0000-0000-0000-000000000006")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface INxDataCard
{
    int FieldCount { get; }
    string GetFieldName(int index);
    string GetFieldValue(string fieldName);
    void SetFieldValue(string fieldName, string value);
    int Save();
}
