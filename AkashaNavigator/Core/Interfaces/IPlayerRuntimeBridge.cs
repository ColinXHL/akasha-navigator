using AkashaNavigator.Views.Windows;

namespace AkashaNavigator.Core.Interfaces;

public interface IPlayerRuntimeBridge
{
    PlayerWindow? GetPlayerWindow();
    void SetPlayerWindow(PlayerWindow playerWindow);
}
