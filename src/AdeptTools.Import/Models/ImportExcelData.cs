namespace AdeptTools.Import.Models;

public class ImportExcelData
{
    public ImportConfig Config { get; set; } = new();
    public List<ColumnMapping> Mappings { get; set; } = new();
    public List<ImportRow> DataRows { get; set; } = new();
}
