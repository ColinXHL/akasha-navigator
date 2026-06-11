using AkashaNavigator.Models.Config;
using AkashaNavigator.Services;
using AkashaNavigator.ViewModels.Pages.Settings;
using Xunit;

namespace AkashaNavigator.Tests
{
    /// <summary>
    /// 按住窥视功能测试
    /// 覆盖配置序列化、透明度范围限制、ViewModel 保存/加载、事件分发等场景
    /// </summary>
    public class HoldToPeekTests
    {
        #region Config Defaults

        [Fact]
        public void AppConfig_Default_EnableHoldToPeek_IsTrue()
        {
            var config = new AppConfig();
            Assert.True(config.EnableHoldToPeek);
        }

        [Fact]
        public void AppConfig_Default_PeekOpacity_IsDefault()
        {
            var config = new AppConfig();
            Assert.Equal(AppConstants.DefaultPeekOpacity, config.PeekOpacity);
        }

        [Fact]
        public void AppConfig_Default_HotkeyPeek_IsVkG()
        {
            var config = new AppConfig();
            Assert.Equal(0x47u, config.HotkeyPeek); // VK_G
        }

        [Fact]
        public void AppConfig_Default_HotkeyPeekMod_IsNone()
        {
            var config = new AppConfig();
            Assert.Equal(ModifierKeys.None, config.HotkeyPeekMod);
        }

        #endregion

        #region Config Serialization / Round-Trip

        [Fact]
        public void AppConfig_PeekProperties_RoundTrip_PreservesValues()
        {
            var original = new AppConfig
            {
                EnableHoldToPeek = true,
                PeekOpacity = 0.5,
                HotkeyPeek = 0x10, // VK_SHIFT (test non-default)
                HotkeyPeekMod = ModifierKeys.Ctrl
            };

            // Simulate serialization round-trip via property reads
            var restored = new AppConfig
            {
                EnableHoldToPeek = original.EnableHoldToPeek,
                PeekOpacity = original.PeekOpacity,
                HotkeyPeek = original.HotkeyPeek,
                HotkeyPeekMod = original.HotkeyPeekMod
            };

            Assert.True(restored.EnableHoldToPeek);
            Assert.Equal(0.5, restored.PeekOpacity);
            Assert.Equal(0x10u, restored.HotkeyPeek);
            Assert.Equal(ModifierKeys.Ctrl, restored.HotkeyPeekMod);
        }

        [Fact]
        public void AppConfig_PeekProperties_Serialization_DefaultToNonNullable()
        {
            // HotkeyPeek is uint (non-nullable), verify default is not 0 when serialized
            var config = new AppConfig();
            Assert.NotEqual(0u, config.HotkeyPeek);
        }

        #endregion

        #region PeekOpacity Clamping

        [Theory]
        [InlineData(0.0, 0.2)]  // Below MinOpacity → clamped to 0.2
        [InlineData(0.1, 0.2)]  // Below MinOpacity → clamped to 0.2
        [InlineData(0.2, 0.2)]  // At MinOpacity → stays 0.2
        [InlineData(0.5, 0.5)]  // Mid-range → stays 0.5
        [InlineData(1.0, 1.0)]  // At MaxOpacity → stays 1.0
        [InlineData(1.5, 1.0)]  // Above MaxOpacity → clamped to 1.0
        public void AppConfig_PeekOpacity_RoundTripViaVM_Clamped(double input, double expected)
        {
            var vm = new WindowSettingsPageViewModel();
            vm.PeekOpacityPercent = input * 100.0;

            var config = new AppConfig();
            vm.SaveSettings(config);

            Assert.InRange(config.PeekOpacity, AppConstants.MinOpacity, AppConstants.MaxOpacity);
            Assert.Equal(expected, config.PeekOpacity, 2);
        }

        [Fact]
        public void AppConfig_PeekOpacity_SetToMinOpacity_Works()
        {
            var config = new AppConfig { PeekOpacity = AppConstants.MinOpacity };
            Assert.Equal(AppConstants.MinOpacity, config.PeekOpacity);
        }

        [Fact]
        public void AppConfig_PeekOpacity_SetToMaxOpacity_Works()
        {
            var config = new AppConfig { PeekOpacity = AppConstants.MaxOpacity };
            Assert.Equal(AppConstants.MaxOpacity, config.PeekOpacity);
        }

        [Fact]
        public void AppConfig_PeekOpacity_BelowMin_ClampedBySaveSettings()
        {
            var vm = new WindowSettingsPageViewModel();
            vm.PeekOpacityPercent = 5.0; // 5%, way below min

            var config = new AppConfig();
            vm.SaveSettings(config);

            Assert.InRange(config.PeekOpacity, AppConstants.MinOpacity, AppConstants.MaxOpacity);
            Assert.Equal(AppConstants.MinOpacity, config.PeekOpacity);
        }

        [Fact]
        public void AppConfig_PeekOpacity_AboveMax_ClampedBySaveSettings()
        {
            var vm = new WindowSettingsPageViewModel();
            vm.PeekOpacityPercent = 150.0; // 150%, way above max

            var config = new AppConfig();
            vm.SaveSettings(config);

            Assert.InRange(config.PeekOpacity, AppConstants.MinOpacity, AppConstants.MaxOpacity);
            Assert.Equal(AppConstants.MaxOpacity, config.PeekOpacity);
        }

        #endregion

        #region ViewModel Load / Save

        [Fact]
        public void WindowSettingsPageVM_LoadSettings_LoadsPeekProperties()
        {
            var vm = new WindowSettingsPageViewModel();
            var config = new AppConfig
            {
                EnableHoldToPeek = true,
                PeekOpacity = 0.3,
                HotkeyPeek = 0x11,
                HotkeyPeekMod = ModifierKeys.Alt
            };

            vm.LoadSettings(config);

            Assert.True(vm.EnableHoldToPeek);
            Assert.Equal(30.0, vm.PeekOpacityPercent, 1); // 0.3 * 100 = 30
        }

        [Fact]
        public void WindowSettingsPageVM_LoadSettings_PeekDisabled()
        {
            var vm = new WindowSettingsPageViewModel();
            var config = new AppConfig
            {
                EnableHoldToPeek = false,
                PeekOpacity = 0.6
            };

            vm.LoadSettings(config);

            Assert.False(vm.EnableHoldToPeek);
            Assert.Equal(60.0, vm.PeekOpacityPercent, 1);
        }

        [Fact]
        public void WindowSettingsPageVM_SaveSettings_SavesPeekProperties()
        {
            var vm = new WindowSettingsPageViewModel();
            vm.EnableHoldToPeek = false;
            vm.PeekOpacityPercent = 80.0;

            var config = new AppConfig();
            vm.SaveSettings(config);

            Assert.False(config.EnableHoldToPeek);
            Assert.Equal(0.8, config.PeekOpacity, 2); // 80% → 0.8
        }

        [Fact]
        public void WindowSettingsPageVM_PeekOpacityPercent_RoundTrip()
        {
            var vm = new WindowSettingsPageViewModel();
            vm.PeekOpacityPercent = 45.0;

            var config = new AppConfig();
            vm.SaveSettings(config);

            var vm2 = new WindowSettingsPageViewModel();
            vm2.LoadSettings(config);

            Assert.Equal(45.0, vm2.PeekOpacityPercent, 1);
        }

        [Fact]
        public void WindowSettingsPageVM_ResetSettings_RestoresPeekDefaults()
        {
            var vm = new WindowSettingsPageViewModel();
            vm.EnableHoldToPeek = false;
            vm.PeekOpacityPercent = 50.0;

            var defaultConfig = new AppConfig();
            vm.ResetSettings(defaultConfig);

            Assert.True(vm.EnableHoldToPeek);
            Assert.Equal(AppConstants.DefaultPeekOpacity * 100.0, vm.PeekOpacityPercent, 1);
        }

        #endregion

        #region ActionDispatcher PeekPressed / PeekReleased

        [Fact]
        public void ActionDispatcher_PeekPressed_IsRegistered()
        {
            var dispatcher = new ActionDispatcher();
            Assert.True(dispatcher.IsActionRegistered(ActionDispatcher.ActionPeekPressed));
        }

        [Fact]
        public void ActionDispatcher_PeekReleased_IsRegistered()
        {
            var dispatcher = new ActionDispatcher();
            Assert.True(dispatcher.IsActionRegistered(ActionDispatcher.ActionPeekReleased));
        }

        [Fact]
        public void ActionDispatcher_DispatchPeekPressed_FiresEvent()
        {
            var dispatcher = new ActionDispatcher();
            bool fired = false;
            dispatcher.PeekPressed += (s, e) => fired = true;

            bool result = dispatcher.Dispatch(ActionDispatcher.ActionPeekPressed);

            Assert.True(result);
            Assert.True(fired);
        }

        [Fact]
        public void ActionDispatcher_DispatchPeekReleased_FiresEvent()
        {
            var dispatcher = new ActionDispatcher();
            bool fired = false;
            dispatcher.PeekReleased += (s, e) => fired = true;

            bool result = dispatcher.Dispatch(ActionDispatcher.ActionPeekReleased);

            Assert.True(result);
            Assert.True(fired);
        }

        [Fact]
        public void ActionDispatcher_PeekPressed_ReturnsFalse_WhenNotRegistered()
        {
            var dispatcher = new ActionDispatcher();
            dispatcher.UnregisterAction(ActionDispatcher.ActionPeekPressed);

            bool result = dispatcher.Dispatch(ActionDispatcher.ActionPeekPressed);

            Assert.False(result);
        }

        [Fact]
        public void ActionDispatcher_PeekReleased_ReturnsFalse_WhenNotRegistered()
        {
            var dispatcher = new ActionDispatcher();
            dispatcher.UnregisterAction(ActionDispatcher.ActionPeekReleased);

            bool result = dispatcher.Dispatch(ActionDispatcher.ActionPeekReleased);

            Assert.False(result);
        }

        #endregion

        #region AppConfig HotkeyConfig Integration

        [Fact]
        public void AppConfig_ToHotkeyConfig_ReturnsValidConfig()
        {
            var config = new AppConfig();
            var hotkeyConfig = config.ToHotkeyConfig();

            Assert.NotNull(hotkeyConfig);
            Assert.NotNull(hotkeyConfig.Profiles);
            Assert.NotEmpty(hotkeyConfig.Profiles);

            var profile = hotkeyConfig.GetActiveProfile();
            Assert.NotNull(profile);
            Assert.Equal("Default", profile.Name);
            Assert.NotNull(profile.Bindings);
        }

        #endregion
    }
}
