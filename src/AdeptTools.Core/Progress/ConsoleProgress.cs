namespace AdeptTools.Core.Progress;

public class ConsoleProgress<T> : IProgress<T>
{
    private readonly bool _verbose;
    private readonly Func<T, string> _formatter;
    private int _count;

    public ConsoleProgress(Func<T, string>? formatter = null, bool verbose = false)
    {
        _verbose = verbose;
        _formatter = formatter ?? (v => v?.ToString() ?? string.Empty);
    }

    public void Report(T value)
    {
        _count++;
        var message = _formatter(value);

        if (_verbose)
        {
            Console.WriteLine($"  [{_count}] {message}");
        }
        else
        {
            Console.Write($"\r  Processing... {_count}");
        }
    }

    public void Complete()
    {
        if (!_verbose)
        {
            Console.WriteLine($"\r  Processing... {_count} done.");
        }
    }
}
