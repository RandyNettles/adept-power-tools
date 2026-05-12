using AdeptTools.Core.Models;

namespace AdeptTools.Import.Models;

public class CreateDocResultDto : ApiResult
{
    public int TableNumber { get; set; }
    public string FileId { get; set; } = string.Empty;
    public int MajRev { get; set; }
    public int MinRev { get; set; }
}
