using System.Runtime.InteropServices;

namespace AdeptTools.Backend.Com.Interop;

/// <summary>
/// COM interop interface for field definition metadata.
/// </summary>
[ComImport]
[Guid("00000000-0000-0000-0000-000000000002")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface INxFieldDef
{
    string FieldName { get; }
    string DisplayName { get; }
    string SchemaId { get; }
    string FieldType { get; }
    int Width { get; }
    bool IsSystem { get; }
    bool IsRestricted { get; }
    int RestrictedValueCount { get; }
    string GetRestrictedValue(int index);
}
