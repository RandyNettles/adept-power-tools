namespace AdeptTools.Import.Models;

public class LibraryDto
{
    public string LibraryId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public List<LibraryDto> Children { get; set; } = new();
}
