using Xunit;

namespace TrafficSense.Tests;

public class StartupServiceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ChangeReturnsVerifiedStartupState(bool enabled)
    {
        var registration = new FakeRegistration { Enabled = !enabled };
        var status = new StartupService(registration).SetEnabled(enabled);
        Assert.Equal(enabled, status.Enabled);
        Assert.Null(status.Error);
    }

    [Fact]
    public void FailedWriteReturnsRealStateAndError()
    {
        var registration = new FakeRegistration { WriteError = new UnauthorizedAccessException() };
        var status = new StartupService(registration).SetEnabled(true);
        Assert.False(status.Enabled);
        Assert.NotNull(status.Error);
    }

    [Fact]
    public void FailedReadReportsUnknownRatherThanDisabled()
    {
        var status = new StartupService(new FakeRegistration { ReadError = new IOException() }).Read();
        Assert.Null(status.Enabled);
        Assert.NotNull(status.Error);
    }

    [Fact]
    public void IgnoredWriteIsReportedAsFailure()
    {
        var status = new StartupService(new FakeRegistration { IgnoreWrites = true }).SetEnabled(true);
        Assert.False(status.Enabled);
        Assert.NotNull(status.Error);
    }

    [Fact]
    public void UnverifiableWriteDoesNotReportSuccess()
    {
        var status = new StartupService(new FakeRegistration { ReadError = new IOException() }).SetEnabled(true);
        Assert.Null(status.Enabled);
        Assert.NotNull(status.Error);
    }

    [Fact]
    public void ExecutablePathWithSpacesIsQuoted()
    {
        var path = Path.Combine(Path.GetTempPath(), "Traffic Sense", "TrafficSense.exe");
        Assert.Equal($"\"{path}\"", StartupCommand.Build(path, null));
    }

    [Fact]
    public void DotnetHostedApplicationIncludesQuotedDllPath()
    {
        var host = Path.Combine(Path.GetTempPath(), "Program Files", "DOTNET.EXE");
        var dll = Path.Combine(Path.GetTempPath(), "Traffic Sense", "TrafficSense.dll");
        Assert.Equal($"\"{host}\" \"{dll}\"", StartupCommand.Build(host, dll));
    }

    [Fact]
    public void MissingExecutableCannotEnableStartup() =>
        Assert.Throws<InvalidOperationException>(() => StartupCommand.Build(null, null));

    [Fact]
    public void DotnetWithoutApplicationDllCannotEnableStartup() =>
        Assert.Throws<InvalidOperationException>(() => StartupCommand.Build("dotnet.exe", null));

    private sealed class FakeRegistration : IStartupRegistration
    {
        public bool Enabled { get; set; }
        public bool IgnoreWrites { get; init; }
        public Exception? ReadError { get; init; }
        public Exception? WriteError { get; init; }
        public bool IsEnabled => ReadError is null ? Enabled : throw ReadError;
        public void SetEnabled(bool enabled)
        {
            if (WriteError != null) throw WriteError;
            if (!IgnoreWrites) Enabled = enabled;
        }
    }
}
