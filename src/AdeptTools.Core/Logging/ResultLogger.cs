namespace AdeptTools.Core.Logging;

public enum ResultStatus
{
    OK,
    Fail,
    Skip,
    Add
}

public class ResultLogger
{
    private readonly bool _verbose;
    private readonly StreamWriter? _fileWriter;

    private int _okCount;
    private int _failCount;
    private int _skipCount;
    private int _addCount;

    public ResultLogger(bool verbose = false, string? logPath = null)
    {
        _verbose = verbose;
        if (logPath is not null)
        {
            var directory = Path.GetDirectoryName(logPath);
            if (directory is not null)
                Directory.CreateDirectory(directory);
            _fileWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };
        }
    }

    public void Log(ResultStatus status, string message)
    {
        switch (status)
        {
            case ResultStatus.OK:   _okCount++;   break;
            case ResultStatus.Fail: _failCount++; break;
            case ResultStatus.Skip: _skipCount++; break;
            case ResultStatus.Add:  _addCount++;  break;
        }

        var prefix = status switch
        {
            ResultStatus.OK   => "\u001b[32m[OK]\u001b[0m  ",
            ResultStatus.Fail => "\u001b[31m[FAIL]\u001b[0m",
            ResultStatus.Skip => "\u001b[33m[SKIP]\u001b[0m",
            ResultStatus.Add  => "\u001b[36m[ADD]\u001b[0m ",
            _ => "[????]"
        };

        var line = $"  {prefix}  {message}";
        Console.WriteLine(line);
        _fileWriter?.WriteLine($"  [{status}]  {message}");
    }

    public void LogDetail(string message)
    {
        if (!_verbose) return;
        Console.WriteLine($"         {message}");
        _fileWriter?.WriteLine($"         {message}");
    }

    public void LogInfo(string message)
    {
        Console.WriteLine(message);
        _fileWriter?.WriteLine(message);
    }

    public void LogSummary()
    {
        var total = _okCount + _failCount + _skipCount + _addCount;
        Console.WriteLine();
        Console.WriteLine($"  Summary: {total} total — {_okCount} succeeded, {_failCount} failed, {_skipCount} skipped, {_addCount} added");
        _fileWriter?.WriteLine();
        _fileWriter?.WriteLine($"  Summary: {total} total — {_okCount} succeeded, {_failCount} failed, {_skipCount} skipped, {_addCount} added");
    }

    public void Dispose()
    {
        _fileWriter?.Dispose();
    }
}
