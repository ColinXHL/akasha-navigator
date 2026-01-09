using System.Text.Json;
using AkashaNavigator.Helpers;
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
    /// **Feature: hotkey-expansion, Property 14: Empty binding displays empty text**
    /// **Validates: Requirements 8.3**
    ///
    /// *For any* HotkeyBinding with Key=0, the display text SHALL be an empty string,
    /// regardless of the modifier keys.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EmptyBinding_DisplaysEmptyText(ModifierKeys modifiers)
    {
        // Arrange - Key=0 represents an empty binding
        uint emptyKey = 0;

        // Act
        var displayText = Win32Helper.GetHotkeyDisplayName(emptyKey, modifiers);

        // Assert - Empty binding should always display empty string
        return (displayText == string.Empty).ToProperty();
    }

    /// <summary>
    /// **Feature: hotkey-expansion, Property 14: Empty binding displays empty text**
    /// **Validates: Requirements 8.3**
    ///
    /// Additional test: Non-empty bindings should NOT display empty text
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonEmptyBinding_DisplaysNonEmptyText(byte keyByte, ModifierKeys modifiers)
    {
        // Skip key=0 as that's the empty binding case
        uint key = keyByte;
        if (key == 0)
            return true.ToProperty(); // Skip this case

        // Act
        var displayText = Win32Helper.GetHotkeyDisplayName(key, modifiers);

        // Assert - Non-empty binding should display non-empty text
        return (!string.IsNullOrEmpty(displayText)).ToProperty();
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

    /// <summary>
    /// 验证空绑定显示空字符串
    /// </summary>
    [Fact]
    public void EmptyBinding_WithNoModifiers_DisplaysEmptyString()
    {
        // Arrange
        uint emptyKey = 0;
        var modifiers = ModifierKeys.None;

        // Act
        var displayText = Win32Helper.GetHotkeyDisplayName(emptyKey, modifiers);

        // Assert
        Assert.Equal(string.Empty, displayText);
    }

    /// <summary>
    /// 验证空绑定即使有修饰键也显示空字符串
    /// </summary>
    [Fact]
    public void EmptyBinding_WithModifiers_DisplaysEmptyString()
    {
        // Arrange
        uint emptyKey = 0;
        var modifiers = ModifierKeys.Ctrl | ModifierKeys.Alt | ModifierKeys.Shift;

        // Act
        var displayText = Win32Helper.GetHotkeyDisplayName(emptyKey, modifiers);

        // Assert
        Assert.Equal(string.Empty, displayText);
    }
}
}
