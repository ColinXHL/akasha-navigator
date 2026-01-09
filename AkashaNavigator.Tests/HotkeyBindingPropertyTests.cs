using System.Text.Json;
using AkashaNavigator.Models.Config;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace AkashaNavigator.Tests
{
/// <summary>
/// HotkeyBinding 属性测试
/// </summary>
public class HotkeyBindingPropertyTests
{
    /// <summary>
    /// **Feature: hotkey-expansion, Property 2: Input type preservation in serialization**
    /// **Validates: Requirements 1.4, 7.4**
    ///
    /// *For any* HotkeyBinding with InputType set to Mouse, serializing and deserializing
    /// SHALL preserve the InputType value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InputType_SerializationRoundTrip_PreservesValue(InputType inputType, byte keyByte, bool isEnabled)
    {
        uint key = keyByte; // Convert byte to uint for valid key codes (0-255)
        var action = "TestAction";
        var modifiers = ModifierKeys.None;

        // Arrange
        var original = new HotkeyBinding { InputType = inputType, Key = key, Modifiers = modifiers, Action = action,
                                           IsEnabled = isEnabled };

        // Act - Serialize then deserialize
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<HotkeyBinding>(json);

        // Assert
        return (deserialized != null && deserialized.InputType == original.InputType &&
                deserialized.Key == original.Key && deserialized.Modifiers == original.Modifiers &&
                deserialized.Action == original.Action && deserialized.IsEnabled == original.IsEnabled)
            .ToProperty();
    }

    /// <summary>
    /// **Feature: hotkey-expansion, Property 2: Input type preservation in serialization**
    /// **Validates: Requirements 1.4, 7.4**
    ///
    /// Additional test with modifiers variation
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InputType_WithModifiers_SerializationRoundTrip(InputType inputType, ModifierKeys modifiers,
                                                                   byte keyByte)
    {
        uint key = keyByte;

        var original = new HotkeyBinding { InputType = inputType, Key = key, Modifiers = modifiers,
                                           Action = "SomeAction", IsEnabled = true };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<HotkeyBinding>(json);

        return (deserialized != null && deserialized.InputType == original.InputType &&
                deserialized.Key == original.Key && deserialized.Modifiers == original.Modifiers)
            .ToProperty();
    }

    /// <summary>
    /// 验证 Mouse InputType 特别能正确序列化
    /// </summary>
    [Fact]
    public void MouseInputType_SerializationRoundTrip_PreservesValue()
    {
        // Arrange
        var original = new HotkeyBinding { InputType = InputType.Mouse, Key = MouseButtonCodes.XButton1,
                                           Modifiers = ModifierKeys.None, Action = "TestAction", IsEnabled = true };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<HotkeyBinding>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(InputType.Mouse, deserialized.InputType);
        Assert.Equal(MouseButtonCodes.XButton1, deserialized.Key);
    }

    /// <summary>
    /// 验证 XButton2 也能正确序列化
    /// </summary>
    [Fact]
    public void XButton2_SerializationRoundTrip_PreservesValue()
    {
        // Arrange
        var original = new HotkeyBinding { InputType = InputType.Mouse, Key = MouseButtonCodes.XButton2,
                                           Modifiers = ModifierKeys.Ctrl, Action = "AnotherAction", IsEnabled = false };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<HotkeyBinding>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(InputType.Mouse, deserialized.InputType);
        Assert.Equal(MouseButtonCodes.XButton2, deserialized.Key);
        Assert.Equal(ModifierKeys.Ctrl, deserialized.Modifiers);
        Assert.False(deserialized.IsEnabled);
    }
}
}
