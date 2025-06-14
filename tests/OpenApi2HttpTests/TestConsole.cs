using System.CommandLine;
using System.CommandLine.IO;
using System.Text;

namespace OpenApi2HttpTests;

public class TestConsole : IConsole
{
    private readonly StringBuilder _out = new();
    private readonly StringBuilder _error = new();

    public IStandardStreamWriter Out { get; }
    public bool IsOutputRedirected => false;
    public IStandardStreamWriter Error { get; }
    public bool IsErrorRedirected => false;
    public bool IsInputRedirected => false;

    public TestConsole()
    {
        Out = new TestStandardStreamWriter(_out);
        Error = new TestStandardStreamWriter(_error);
    }

    public string GetOutput() => _out.ToString();
    public string GetError() => _error.ToString();

    private class TestStandardStreamWriter : IStandardStreamWriter
    {
        private readonly StringBuilder _stringBuilder;

        public TestStandardStreamWriter(StringBuilder stringBuilder)
        {
            _stringBuilder = stringBuilder;
        }

        public void Write(string? value)
        {
            if (value != null)
                _stringBuilder.Append(value);
        }

        public void WriteLine(string? value)
        {
            if (value != null)
                _stringBuilder.AppendLine(value);
            else
                _stringBuilder.AppendLine();
        }

        public void WriteLine()
        {
            _stringBuilder.AppendLine();
        }
    }
}