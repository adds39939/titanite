using PhotinoX.App;
using Titanite.Abstractions.Hosting;

namespace Titanite.App;

internal sealed class PhotinoAppEnvironment(PhotinoEnvironment environment) : IAppEnvironment
{
    public bool IsDevelopment => environment.IsDevelopment;
}
