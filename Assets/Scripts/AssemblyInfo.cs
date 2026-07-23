using System.Runtime.CompilerServices;

// Grant the PlayMode test assembly access to Make10.Runtime internals so
// integration tests can drive gameplay through the internal test hooks
// (e.g. GridManager.BeginSwap / GetTileAt) instead of reflection.
[assembly: InternalsVisibleTo("Make10.Tests.PlayMode")]
