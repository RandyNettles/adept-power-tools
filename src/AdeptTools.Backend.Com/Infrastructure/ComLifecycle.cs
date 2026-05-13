using System.Runtime.InteropServices;

namespace AdeptTools.Backend.Com.Infrastructure;

/// <summary>
/// Provides helpers for proper COM object lifecycle management.
/// Ensures COM references are released deterministically to avoid leaks
/// and prevents dialog popups from the COM SDK.
/// </summary>
public static class ComLifecycle
{
    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(
        ref Guid rclsid,
        IntPtr pvReserved,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

    /// <summary>
    /// Releases a COM object and suppresses finalization. Safe to call with null.
    /// </summary>
    public static void Release(ref object? comObject)
    {
        if (comObject == null) return;

        try
        {
            if (Marshal.IsComObject(comObject))
            {
                Marshal.FinalReleaseComObject(comObject);
            }
        }
        finally
        {
            comObject = null;
        }
    }

    /// <summary>
    /// Releases a COM object cast to a specific interface type.
    /// </summary>
    public static void Release<T>(ref T? comObject) where T : class
    {
        if (comObject == null) return;

        object? obj = comObject;
        Release(ref obj);
        comObject = null;
    }

    /// <summary>
    /// Executes an action with a COM object and ensures the object is released afterward.
    /// </summary>
    public static TResult UseAndRelease<TCom, TResult>(TCom comObject, Func<TCom, TResult> action)
        where TCom : class
    {
        try
        {
            return action(comObject);
        }
        finally
        {
            var obj = comObject as object;
            Release(ref obj);
        }
    }

    /// <summary>
    /// Creates a COM object from a ProgID. Throws if the COM class is not registered.
    /// </summary>
    public static T CreateInstance<T>(string progId) where T : class
    {
        var type = Type.GetTypeFromProgID(progId, throwOnError: true)!;
        var instance = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Failed to create COM instance for ProgID '{progId}'.");
        return (T)instance;
    }

    /// <summary>
    /// Attempts to get a running COM object from the ROT (Running Object Table).
    /// Returns null if not found.
    /// </summary>
    public static T? GetActiveInstance<T>(string progId) where T : class
    {
        try
        {
            var type = Type.GetTypeFromProgID(progId, throwOnError: true)!;
            var clsid = type.GUID;
            GetActiveObject(ref clsid, IntPtr.Zero, out var obj);
            return obj as T;
        }
        catch (COMException)
        {
            return null;
        }
    }
}
