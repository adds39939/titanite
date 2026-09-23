namespace Titanite.Abstractions.Hosting;

public interface IInterfaceScaler
{
    Task ApplyAsync(int percent);
}
