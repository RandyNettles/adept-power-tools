using CommunityToolkit.Mvvm.ComponentModel;

namespace AdeptTools.Launcher.ViewModels;

public enum ResultStatus
{
    Ok,
    Fail,
    Skip,
    Add
}

public partial class ResultItem : ObservableObject
{
    public ResultStatus Status { get; }
    public string Message { get; }

    public string StatusPrefix => Status switch
    {
        ResultStatus.Ok => "[OK]",
        ResultStatus.Fail => "[FAIL]",
        ResultStatus.Skip => "[SKIP]",
        ResultStatus.Add => "[ADD]",
        _ => "[?]"
    };

    public ResultItem(ResultStatus status, string message)
    {
        Status = status;
        Message = message;
    }
}
