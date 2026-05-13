using System.Runtime.InteropServices;

namespace AdeptTools.Backend.Com.Interop;

/// <summary>
/// COM interop interface for the Adept NxProject object.
/// ProgID: "AdeptSDK.NxProject"
/// </summary>
[ComImport]
[Guid("00000000-0000-0000-0000-000000000000")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface INxProject
{
    string UserId { get; }
    string UserName { get; }
    string DisplayName { get; }
    string EmailAddress { get; }
    string AppVersion { get; }
    string WorkAreaId { get; }
    string ServerUrl { get; }
    bool IsConnected { get; }

    int Connect(string serverUrl, string userId, string password);
    void Disconnect();
    INxDb GetDatabase();
    INxWorkflowAdmin GetWorkflowAdmin();
}
