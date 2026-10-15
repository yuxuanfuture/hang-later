namespace HangLater.Windows.Services;

public sealed class Localizer
{
    public bool Chinese { get; private set; }
    public Localizer(bool chinese) => Chinese = chinese;
    public void Set(bool chinese) => Chinese = chinese;
    public string T(string zh, string en) => Chinese ? zh : en;
}
