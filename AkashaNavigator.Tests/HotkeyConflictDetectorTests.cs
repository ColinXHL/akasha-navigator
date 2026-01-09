using System.Collections.Generic;
using System.Linq;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Services;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace AkashaNavigator.Tests
{
/// <summary>
/// HotkeyConflictDetector 测试
/// </summary>
public class HotkeyConflictDetectorTests
{
    private readonly HotkeyConflictDetector _detector = new();

#region Property 16 : Conflict detection accuracy

    /// <summary>
    /// **Feature: hotkey-expansion, Property 16: Conflict detection accuracy**
    /// **Validates: Requirements 9.1, 9.3**
    ///
    /// *For any* set of hotkey bindings, if two or more bindings have the same
    /// (InputType, Key, Modifiers) combination, they SHALL be marked as conflicting.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SameKeySignature_DetectedAsConflict(byte keyByte, ModifierKeys modifiers, InputType inputType)
    {
        // Skip empty bindings (Key=0) as they don't participate in conflict detection
        uint key = keyByte;
        if (key == 0)
            return true.ToProperty();

        // Arrange - Create two bindings with the same key signature
        var binding1 =
            new HotkeyBinding { InputType = inputType, Key = key, Modifiers = modifiers, Action = "Action1" };

        var binding2 =
            new HotkeyBinding { InputType = inputType, Key = key, Modifiers = modifiers, Action = "Action2" };

        var bindings = new List<HotkeyBinding> { binding1, binding2 };

        // Act
        var conflicts = _detector.DetectConflicts(bindings);
        var hasConflict1 = _detector.HasConflict(binding1, bindings);
        var hasConflict2 = _detector.HasConflict(binding2, bindings);

        // Assert - Both bindings should be detected as conflicting
        return (conflicts.Count == 1 && conflicts.Values.First().Count == 2 && hasConflict1 && hasConflict2)
            .ToProperty();
    }

    /// <summary>
    /// **Feature: hotkey-expansion, Property 16: Conflict detection accuracy**
    /// **Validates: Requirements 9.1, 9.3**
    ///
    /// *For any* set of hotkey bindings with different key signatures,
    /// they SHALL NOT be marked as conflicting.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DifferentKeySignatures_NoConflict(byte key1Byte, byte key2Byte, ModifierKeys modifiers1,
                                                      ModifierKeys modifiers2)
    {
        uint key1 = key1Byte;
        uint key2 = key2Byte;

        // Skip if both keys are 0 (empty bindings)
        if (key1 == 0 && key2 == 0)
            return true.ToProperty();

        // Skip if signatures are the same
        if (key1 == key2 && modifiers1 == modifiers2)
            return true.ToProperty();

        // Arrange - Create two bindings with different key signatures
        var binding1 = new HotkeyBinding { InputType = InputType.Keyboard, Key = key1, Modifiers = modifiers1,
                                           Action = "Action1" };

        var binding2 = new HotkeyBinding { InputType = InputType.Keyboard, Key = key2, Modifiers = modifiers2,
                                           Action = "Action2" };

        var bindings = new List<HotkeyBinding> { binding1, binding2 };

        // Act
        var conflicts = _detector.DetectConflicts(bindings);
        var hasConflict1 = _detector.HasConflict(binding1, bindings);
        var hasConflict2 = _detector.HasConflict(binding2, bindings);

        // Assert - Neither binding should be detected as conflicting
        return (conflicts.Count == 0 && !hasConflict1 && !hasConflict2).ToProperty();
    }

    /// <summary>
    /// **Feature: hotkey-expansion, Property 16: Conflict detection accuracy**
    /// **Validates: Requirements 9.1, 9.3**
    ///
    /// *For any* empty binding (Key=0), it SHALL NOT be detected as conflicting,
    /// even if multiple empty bindings exist.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EmptyBindings_NeverConflict(ModifierKeys modifiers1, ModifierKeys modifiers2)
    {
        // Arrange - Create two empty bindings
        var binding1 =
            new HotkeyBinding { InputType = InputType.Keyboard, Key = 0, Modifiers = modifiers1, Action = "Action1" };

        var binding2 =
            new HotkeyBinding { InputType = InputType.Keyboard, Key = 0, Modifiers = modifiers2, Action = "Action2" };

        var bindings = new List<HotkeyBinding> { binding1, binding2 };

        // Act
        var conflicts = _detector.DetectConflicts(bindings);
        var hasConflict1 = _detector.HasConflict(binding1, bindings);
        var hasConflict2 = _detector.HasConflict(binding2, bindings);

        // Assert - Empty bindings should never be detected as conflicting
        return (conflicts.Count == 0 && !hasConflict1 && !hasConflict2).ToProperty();
    }

#endregion

#region Property 17 : Conflict resolution removes warnings

    /// <summary>
    /// **Feature: hotkey-expansion, Property 17: Conflict resolution removes warnings**
    /// **Validates: Requirements 9.4**
    ///
    /// *For any* previously conflicting binding, when the conflict is resolved by
    /// changing one binding, the HasConflict flag SHALL be set to false.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConflictResolution_ByChangingKey_RemovesConflict(byte originalKeyByte, byte newKeyByte,
                                                                     ModifierKeys modifiers)
    {
        uint originalKey = originalKeyByte;
        uint newKey = newKeyByte;

        // Skip empty bindings
        if (originalKey == 0 || newKey == 0)
            return true.ToProperty();

        // Skip if keys are the same (no resolution)
        if (originalKey == newKey)
            return true.ToProperty();

        // Arrange - Create two conflicting bindings
        var binding1 = new HotkeyBinding { InputType = InputType.Keyboard, Key = originalKey, Modifiers = modifiers,
                                           Action = "Action1" };

        var binding2 = new HotkeyBinding { InputType = InputType.Keyboard, Key = originalKey, Modifiers = modifiers,
                                           Action = "Action2" };

        var bindings = new List<HotkeyBinding> { binding1, binding2 };

        // Verify initial conflict exists
        var initialConflicts = _detector.DetectConflicts(bindings);
        if (initialConflicts.Count != 1)
            return false.ToProperty();

        // Act - Resolve conflict by changing binding2's key
        binding2.Key = newKey;

        // Assert - Conflict should be resolved
        var finalConflicts = _detector.DetectConflicts(bindings);
        var hasConflict1 = _detector.HasConflict(binding1, bindings);
        var hasConflict2 = _detector.HasConflict(binding2, bindings);

        return (finalConflicts.Count == 0 && !hasConflict1 && !hasConflict2).ToProperty();
    }

    /// <summary>
    /// **Feature: hotkey-expansion, Property 17: Conflict resolution removes warnings**
    /// **Validates: Requirements 9.4**
    ///
    /// *For any* previously conflicting binding, when the conflict is resolved by
    /// clearing one binding (Key=0), the HasConflict flag SHALL be set to false.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConflictResolution_ByClearingKey_RemovesConflict(byte keyByte, ModifierKeys modifiers)
    {
        uint key = keyByte;

        // Skip empty bindings
        if (key == 0)
            return true.ToProperty();

        // Arrange - Create two conflicting bindings
        var binding1 =
            new HotkeyBinding { InputType = InputType.Keyboard, Key = key, Modifiers = modifiers, Action = "Action1" };

        var binding2 =
            new HotkeyBinding { InputType = InputType.Keyboard, Key = key, Modifiers = modifiers, Action = "Action2" };

        var bindings = new List<HotkeyBinding> { binding1, binding2 };

        // Verify initial conflict exists
        var initialConflicts = _detector.DetectConflicts(bindings);
        if (initialConflicts.Count != 1)
            return false.ToProperty();

        // Act - Resolve conflict by clearing binding2
        binding2.Key = 0;

        // Assert - Conflict should be resolved
        var finalConflicts = _detector.DetectConflicts(bindings);
        var hasConflict1 = _detector.HasConflict(binding1, bindings);
        var hasConflict2 = _detector.HasConflict(binding2, bindings);

        return (finalConflicts.Count == 0 && !hasConflict1 && !hasConflict2).ToProperty();
    }

    /// <summary>
    /// **Feature: hotkey-expansion, Property 17: Conflict resolution removes warnings**
    /// **Validates: Requirements 9.4**
    ///
    /// *For any* previously conflicting binding, when the conflict is resolved by
    /// changing modifiers, the HasConflict flag SHALL be set to false.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConflictResolution_ByChangingModifiers_RemovesConflict(byte keyByte, ModifierKeys modifiers1,
                                                                           ModifierKeys modifiers2)
    {
        uint key = keyByte;

        // Skip empty bindings
        if (key == 0)
            return true.ToProperty();

        // Skip if modifiers are the same (no resolution)
        if (modifiers1 == modifiers2)
            return true.ToProperty();

        // Arrange - Create two conflicting bindings
        var binding1 =
            new HotkeyBinding { InputType = InputType.Keyboard, Key = key, Modifiers = modifiers1, Action = "Action1" };

        var binding2 =
            new HotkeyBinding { InputType = InputType.Keyboard, Key = key, Modifiers = modifiers1, Action = "Action2" };

        var bindings = new List<HotkeyBinding> { binding1, binding2 };

        // Verify initial conflict exists
        var initialConflicts = _detector.DetectConflicts(bindings);
        if (initialConflicts.Count != 1)
            return false.ToProperty();

        // Act - Resolve conflict by changing binding2's modifiers
        binding2.Modifiers = modifiers2;

        // Assert - Conflict should be resolved
        var finalConflicts = _detector.DetectConflicts(bindings);
        var hasConflict1 = _detector.HasConflict(binding1, bindings);
        var hasConflict2 = _detector.HasConflict(binding2, bindings);

        return (finalConflicts.Count == 0 && !hasConflict1 && !hasConflict2).ToProperty();
    }

#endregion

#region Unit Tests

    /// <summary>
    /// 验证相同按键组合检测为冲突
    /// </summary>
    [Fact]
    public void SameKeyCombo_DetectedAsConflict()
    {
        // Arrange
        var binding1 = new HotkeyBinding { Key = 0x35, Modifiers = ModifierKeys.Ctrl, Action = "Action1" };
        var binding2 = new HotkeyBinding { Key = 0x35, Modifiers = ModifierKeys.Ctrl, Action = "Action2" };
        var bindings = new List<HotkeyBinding> { binding1, binding2 };

        // Act
        var conflicts = _detector.DetectConflicts(bindings);

        // Assert
        Assert.Single(conflicts);
        Assert.Equal(2, conflicts.Values.First().Count);
    }

    /// <summary>
    /// 验证不同修饰键不检测为冲突
    /// </summary>
    [Fact]
    public void DifferentModifiers_NoConflict()
    {
        // Arrange
        var binding1 = new HotkeyBinding { Key = 0x35, Modifiers = ModifierKeys.Ctrl, Action = "Action1" };
        var binding2 = new HotkeyBinding { Key = 0x35, Modifiers = ModifierKeys.Alt, Action = "Action2" };
        var bindings = new List<HotkeyBinding> { binding1, binding2 };

        // Act
        var conflicts = _detector.DetectConflicts(bindings);

        // Assert
        Assert.Empty(conflicts);
    }

    /// <summary>
    /// 验证空绑定不参与冲突检测
    /// </summary>
    [Fact]
    public void EmptyBinding_IgnoredInConflictDetection()
    {
        // Arrange
        var binding1 = new HotkeyBinding { Key = 0, Modifiers = ModifierKeys.None, Action = "Action1" };
        var binding2 = new HotkeyBinding { Key = 0, Modifiers = ModifierKeys.None, Action = "Action2" };
        var bindings = new List<HotkeyBinding> { binding1, binding2 };

        // Act
        var conflicts = _detector.DetectConflicts(bindings);
        var hasConflict = _detector.HasConflict(binding1, bindings);

        // Assert
        Assert.Empty(conflicts);
        Assert.False(hasConflict);
    }

    /// <summary>
    /// 验证不同输入类型不检测为冲突
    /// </summary>
    [Fact]
    public void DifferentInputTypes_NoConflict()
    {
        // Arrange - Same key but different input types
        var binding1 = new HotkeyBinding { InputType = InputType.Keyboard, Key = 0x05, Modifiers = ModifierKeys.None,
                                           Action = "Action1" };
        var binding2 = new HotkeyBinding { InputType = InputType.Mouse, Key = 0x05, Modifiers = ModifierKeys.None,
                                           Action = "Action2" };
        var bindings = new List<HotkeyBinding> { binding1, binding2 };

        // Act
        var conflicts = _detector.DetectConflicts(bindings);

        // Assert
        Assert.Empty(conflicts);
    }

    /// <summary>
    /// 验证获取冲突绑定列表
    /// </summary>
    [Fact]
    public void GetConflictingBindings_ReturnsCorrectList()
    {
        // Arrange
        var binding1 = new HotkeyBinding { Key = 0x35, Modifiers = ModifierKeys.None, Action = "Action1" };
        var binding2 = new HotkeyBinding { Key = 0x35, Modifiers = ModifierKeys.None, Action = "Action2" };
        var binding3 = new HotkeyBinding { Key = 0x36, Modifiers = ModifierKeys.None, Action = "Action3" };
        var bindings = new List<HotkeyBinding> { binding1, binding2, binding3 };

        // Act
        var conflicting = _detector.GetConflictingBindings(binding1, bindings);

        // Assert
        Assert.Single(conflicting);
        Assert.Equal("Action2", conflicting[0].Action);
    }

    /// <summary>
    /// 验证通过更改按键解决冲突
    /// </summary>
    [Fact]
    public void ConflictResolution_ChangeKey_RemovesWarning()
    {
        // Arrange - Create conflicting bindings
        var binding1 = new HotkeyBinding { Key = 0x35, Modifiers = ModifierKeys.None, Action = "Action1" };
        var binding2 = new HotkeyBinding { Key = 0x35, Modifiers = ModifierKeys.None, Action = "Action2" };
        var bindings = new List<HotkeyBinding> { binding1, binding2 };

        // Verify initial conflict
        Assert.True(_detector.HasConflict(binding1, bindings));
        Assert.True(_detector.HasConflict(binding2, bindings));

        // Act - Resolve by changing key
        binding2.Key = 0x36;

        // Assert - No more conflict
        Assert.False(_detector.HasConflict(binding1, bindings));
        Assert.False(_detector.HasConflict(binding2, bindings));
    }

    /// <summary>
    /// 验证通过清空按键解决冲突
    /// </summary>
    [Fact]
    public void ConflictResolution_ClearKey_RemovesWarning()
    {
        // Arrange - Create conflicting bindings
        var binding1 = new HotkeyBinding { Key = 0x35, Modifiers = ModifierKeys.None, Action = "Action1" };
        var binding2 = new HotkeyBinding { Key = 0x35, Modifiers = ModifierKeys.None, Action = "Action2" };
        var bindings = new List<HotkeyBinding> { binding1, binding2 };

        // Verify initial conflict
        Assert.True(_detector.HasConflict(binding1, bindings));

        // Act - Resolve by clearing key
        binding2.Key = 0;

        // Assert - No more conflict
        Assert.False(_detector.HasConflict(binding1, bindings));
        Assert.False(_detector.HasConflict(binding2, bindings));
    }

    /// <summary>
    /// 验证通过更改修饰键解决冲突
    /// </summary>
    [Fact]
    public void ConflictResolution_ChangeModifiers_RemovesWarning()
    {
        // Arrange - Create conflicting bindings
        var binding1 = new HotkeyBinding { Key = 0x35, Modifiers = ModifierKeys.Ctrl, Action = "Action1" };
        var binding2 = new HotkeyBinding { Key = 0x35, Modifiers = ModifierKeys.Ctrl, Action = "Action2" };
        var bindings = new List<HotkeyBinding> { binding1, binding2 };

        // Verify initial conflict
        Assert.True(_detector.HasConflict(binding1, bindings));

        // Act - Resolve by changing modifiers
        binding2.Modifiers = ModifierKeys.Alt;

        // Assert - No more conflict
        Assert.False(_detector.HasConflict(binding1, bindings));
        Assert.False(_detector.HasConflict(binding2, bindings));
    }

#endregion
}
}
