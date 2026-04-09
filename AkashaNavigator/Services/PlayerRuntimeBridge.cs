using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Views.Windows;

namespace AkashaNavigator.Services;

public class PlayerRuntimeBridge : IPlayerRuntimeBridge
{
    private PlayerWindow? _playerWindow;

    public PlayerWindow? GetPlayerWindow()
    {
        return _playerWindow;
    }

    public void SetPlayerWindow(PlayerWindow playerWindow)
    {
        _playerWindow = playerWindow;
    }
}
