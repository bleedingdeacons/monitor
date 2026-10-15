using TheBleedingDeacons.Intergroup.Monitor.Services.Interfaces;

namespace TheBleedingDeacons.Intergroup.Monitor.Services;

/// <summary>
/// The real <see cref="IUiDispatcher"/>: MAUI's main thread.
///
/// <para>The whole implementation is the one call it forwards. It lives
/// in the app rather than in Monitor.Core because <c>MainThread</c> comes
/// from the MAUI workload, which is precisely why the interface exists —
/// see <see cref="IUiDispatcher"/>.</para>
/// </summary>
public sealed class MainThreadDispatcher : IUiDispatcher
{
	public Task InvokeAsync(Action action) => MainThread.InvokeOnMainThreadAsync(action);
}
