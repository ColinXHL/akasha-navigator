using System.Collections.Generic;
using AkashaNavigator.Controls;
using AkashaNavigator.Core;
using AkashaNavigator.Models.Config;
using Xunit;

namespace AkashaNavigator.Tests;

public sealed class HotkeyRegressionTests
{
    [Theory]
    [InlineData(0x10)]
    [InlineData(0x11)]
    [InlineData(0x12)]
    [InlineData(0x5B)]
    [InlineData(0x5C)]
    [InlineData(0xA0)]
    [InlineData(0xA1)]
    [InlineData(0xA2)]
    [InlineData(0xA3)]
    [InlineData(0xA4)]
    [InlineData(0xA5)]
    public void AltRecorder_IgnoresModifierKeys(uint vk)
    {
        Assert.False(HotkeyTextBox.IsRecordableCombo(vk));
    }

    [Theory]
    [InlineData(0x09)]
    [InlineData(0x1B)]
    [InlineData(0x73)]
    [InlineData(0x79)]
    public void AltRecorder_IgnoresReservedSystemKeys(uint vk)
    {
        Assert.False(HotkeyTextBox.IsRecordableCombo(vk));
    }

    [Fact]
    public void AltRecorder_AllowsOrdinaryMainKey()
    {
        Assert.True(HotkeyTextBox.IsRecordableCombo(0x35));
    }

    [Fact]
    public void MergeUserBindings_ReplacesOldCoreBindingAndPreservesPluginBinding()
    {
        var active = new HotkeyProfile
        {
            Name = "Default",
            Bindings = new List<HotkeyBinding>
            {
                new() { Key = 0x35, Modifiers = ModifierKeys.None, Action = "SeekBackward" },
                new() { Key = 0x50, Modifiers = ModifierKeys.Alt, Action = "Plugin:test:Hotkey:1" }
            }
        };
        var incoming = new HotkeyProfile
        {
            Name = "Default",
            Bindings = new List<HotkeyBinding>
            {
                new() { Key = 0x41, Modifiers = ModifierKeys.Ctrl, Action = "SeekBackward" }
            }
        };

        HotkeyManager.MergeUserBindings(active, incoming);

        Assert.DoesNotContain(active.Bindings,
            binding => binding.Action == "SeekBackward" && binding.Key == 0x35);
        Assert.Contains(active.Bindings,
            binding => binding.Action == "SeekBackward" && binding.Key == 0x41 &&
                       binding.Modifiers == ModifierKeys.Ctrl);
        Assert.Contains(active.Bindings,
            binding => binding.Action == "Plugin:test:Hotkey:1" && binding.Key == 0x50);
    }

    [Fact]
    public void MergeUserBindings_PreservesMultipleBindingsForSameIncomingAction()
    {
        var active = new HotkeyProfile
        {
            Name = "Default",
            Bindings = new List<HotkeyBinding>
            {
                new() { Key = 0x35, Modifiers = ModifierKeys.None, Action = "SeekBackward" }
            }
        };
        var incoming = new HotkeyProfile
        {
            Name = "Default",
            Bindings = new List<HotkeyBinding>
            {
                new() { Key = 0x41, Modifiers = ModifierKeys.Ctrl, Action = "SeekBackward" },
                new() { Key = 0x42, Modifiers = ModifierKeys.Alt, Action = "SeekBackward" }
            }
        };

        HotkeyManager.MergeUserBindings(active, incoming);

        Assert.Equal(2, active.Bindings.FindAll(binding => binding.Action == "SeekBackward").Count);
    }
}
