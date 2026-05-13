using System.Runtime.InteropServices;

namespace AdeptTools.Backend.Com.Interop;

/// <summary>
/// COM interop interface for the Adept NxDb (database) object.
/// Provides field definitions, search, and data card operations.
/// </summary>
[ComImport]
[Guid("00000000-0000-0000-0000-000000000001")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface INxDb
{
    int GetFieldCount();
    INxFieldDef GetFieldDef(int index);
    INxSearchResult Search(INxSearchCriteria criteria);
    INxDataCard GetDataCard(int tableNumber, string fileId, int majRev, int minRev);
    int CreateDocument(string workAreaId, string fileName, out string fileId, out int majRev, out int minRev);
    int CheckInToLibrary(string fileId, int majRev, int minRev, string libraryId);
    int GetMaxFilenameLength();
    INxLibraryTree GetLibraryTree();
}
