using System.Runtime.InteropServices;

namespace AdeptTools.Backend.Com.Interop;

/// <summary>
/// COM interop interface for search criteria construction.
/// </summary>
[ComImport]
[Guid("00000000-0000-0000-0000-000000000003")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface INxSearchCriteria
{
    void AddCondition(string fieldName, string operatorType, string value);
    void SetMaxResults(int maxResults);
    void Clear();
}
