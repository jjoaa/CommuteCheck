using Kiosk.Services.Interface;
using Microsoft.Extensions.Logging;

namespace Kiosk.Commands;

public sealed class VmDeps<T>
{
    public VmDeps(ILogger<T> logger, INavigationService nav, ISessionService session)
    { Logger = logger; Nav = nav; Session = session; }

    public ILogger<T> Logger { get; }
    public INavigationService Nav { get; }
    public ISessionService Session { get; }
}