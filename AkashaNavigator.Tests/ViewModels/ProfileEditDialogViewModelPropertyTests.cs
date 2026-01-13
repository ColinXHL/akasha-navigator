using System;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Profile;
using AkashaNavigator.ViewModels.Dialogs;
using FsCheck;
using FsCheck.Xunit;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.ViewModels
{
/// <summary>
/// ProfileEditDialogViewModel 属性测试
/// </summary>
public class ProfileEditDialogViewModelPropertyTests
{
    private readonly Mock<IProfileManager> _mockProfileManager;

    public ProfileEditDialogViewModelPropertyTests()
    {
        _mockProfileManager = new Mock<IProfileManager>();
        _mockProfileManager.Setup(x => x.ProfileIcons).Returns(new[] { "📦", "🎮", "🎬", "📺", "🎵" });
        _mockProfileManager.Setup(x => x.UpdateProfile(It.IsAny<string>(), It.IsAny<ProfileUpdateData>()))
            .Returns(true);
    }

#region Property 1 : ViewModel Initialization

    [Property(MaxTest = 100)]
    public Property Initialize_PreservesDefaultUrl()
    {
        return Prop.ForAll(Arb.From<NonEmptyString>(),
                           (url) =>
                           {
                               var profile =
                                   CreateTestProfile(defaults: new ProfileDefaults { Url = url.Get, SeekSeconds = 10 });
                               var viewModel = new ProfileEditDialogViewModel(_mockProfileManager.Object);
                               viewModel.Initialize(profile);
                               return viewModel.DefaultUrl == url.Get;
                           });
    }

    [Property(MaxTest = 100)]
    public Property Initialize_PreservesSeekSeconds()
    {
        return Prop.ForAll(Arb.From<PositiveInt>(),
                           (seekSeconds) =>
                           {
                               var validSeekSeconds = Math.Max(1, Math.Min(60, seekSeconds.Get));
                               var profile =
                                   CreateTestProfile(defaults: new ProfileDefaults { Url = "https://test.com",
                                                                                     SeekSeconds = validSeekSeconds });
                               var viewModel = new ProfileEditDialogViewModel(_mockProfileManager.Object);
                               viewModel.Initialize(profile);
                               return viewModel.SeekSeconds == validSeekSeconds;
                           });
    }

    [Fact]
    public void Initialize_WithCursorDetection_PreservesSettings()
    {
        var profile =
            CreateTestProfile(cursorDetection: new CursorDetectionConfig { Enabled = true, MinOpacity = 0.5 });
        var viewModel = new ProfileEditDialogViewModel(_mockProfileManager.Object);

        viewModel.Initialize(profile);

        Assert.True(viewModel.CursorDetectionEnabled);
        Assert.Equal(0.5, viewModel.CursorDetectionMinOpacity);
    }

    [Fact]
    public void Initialize_WithoutCursorDetection_UsesDefaults()
    {
        var profile = CreateTestProfile(cursorDetection: null);
        var viewModel = new ProfileEditDialogViewModel(_mockProfileManager.Object);

        viewModel.Initialize(profile);

        Assert.False(viewModel.CursorDetectionEnabled);
    }

#endregion

#region Property 2 : Opacity Clamping

    [Property(MaxTest = 100)]
    public Property CursorDetectionMinOpacity_IsClamped_ToValidRange()
    {
        return Prop.ForAll(Arb.From<NormalFloat>(), (opacity) =>
                                                    {
                                                        var viewModel =
                                                            new ProfileEditDialogViewModel(_mockProfileManager.Object);
                                                        var profile = CreateTestProfile();
                                                        viewModel.Initialize(profile);
                                                        viewModel.CursorDetectionMinOpacity = opacity.Get;
                                                        return viewModel.CursorDetectionMinOpacity >= 0.1 &&
                                                               viewModel.CursorDetectionMinOpacity <= 0.8;
                                                    });
    }

#endregion

#region Property 3 : Save Behavior

    [Fact]
    public void Save_WithCursorDetectionSettings_SavesConfig()
    {
        ProfileUpdateData? capturedUpdateData = null;
        var mockManager = new Mock<IProfileManager>();
        mockManager.Setup(x => x.ProfileIcons).Returns(new[] { "📦", "🎮" });
        mockManager.Setup(x => x.UpdateProfile(It.IsAny<string>(), It.IsAny<ProfileUpdateData>()))
            .Callback<string, ProfileUpdateData>((id, data) => capturedUpdateData = data)
            .Returns(true);

        var viewModel = new ProfileEditDialogViewModel(mockManager.Object);
        var profile = CreateTestProfile();
        viewModel.Initialize(profile);

        viewModel.CursorDetectionEnabled = true;
        viewModel.CursorDetectionMinOpacity = 0.5;
        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(capturedUpdateData);
        Assert.NotNull(capturedUpdateData.CursorDetection);
        Assert.True(capturedUpdateData.CursorDetection.Enabled);
        Assert.Equal(0.5, capturedUpdateData.CursorDetection.MinOpacity);
    }

    [Fact]
    public void Save_WithNoSettings_ClearsCursorDetection()
    {
        ProfileUpdateData? capturedUpdateData = null;
        var mockManager = new Mock<IProfileManager>();
        mockManager.Setup(x => x.ProfileIcons).Returns(new[] { "📦", "🎮" });
        mockManager.Setup(x => x.UpdateProfile(It.IsAny<string>(), It.IsAny<ProfileUpdateData>()))
            .Callback<string, ProfileUpdateData>((id, data) => capturedUpdateData = data)
            .Returns(true);

        var viewModel = new ProfileEditDialogViewModel(mockManager.Object);
        var profile = CreateTestProfile(cursorDetection: new CursorDetectionConfig { Enabled = true });
        viewModel.Initialize(profile);

        // Reset to defaults
        viewModel.CursorDetectionEnabled = false;
        viewModel.CursorDetectionMinOpacity = 0.3;
        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(capturedUpdateData);
        Assert.True(capturedUpdateData.ClearCursorDetection);
    }

#endregion

#region Property 4 : URL Validation

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://invalid-scheme.com")]
    [InlineData("just some text")]
    public void InvalidUrl_SetsUrlError(string invalidUrl)
    {
        var viewModel = new ProfileEditDialogViewModel(_mockProfileManager.Object);
        var profile = CreateTestProfile();
        viewModel.Initialize(profile);

        viewModel.DefaultUrl = invalidUrl;

        Assert.False(string.IsNullOrEmpty(viewModel.UrlError));
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://test.org/path")]
    [InlineData("")]
    public void ValidUrl_ClearsUrlError(string validUrl)
    {
        var viewModel = new ProfileEditDialogViewModel(_mockProfileManager.Object);
        var profile = CreateTestProfile();
        viewModel.Initialize(profile);

        viewModel.DefaultUrl = validUrl;

        Assert.Null(viewModel.UrlError);
    }

#endregion

#region Property 5 : CanSave

    [Fact]
    public void CanSave_WithValidationErrors_ReturnsFalse()
    {
        var viewModel = new ProfileEditDialogViewModel(_mockProfileManager.Object);
        var profile = CreateTestProfile();
        viewModel.Initialize(profile);

        viewModel.DefaultUrl = "invalid-url";

        Assert.True(viewModel.HasValidationErrors);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void CanSave_WithNoChanges_ReturnsFalse()
    {
        var viewModel = new ProfileEditDialogViewModel(_mockProfileManager.Object);
        var profile = CreateTestProfile();
        viewModel.Initialize(profile);

        Assert.False(viewModel.HasValidationErrors);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void CanSave_WithValidChanges_ReturnsTrue()
    {
        var viewModel = new ProfileEditDialogViewModel(_mockProfileManager.Object);
        var profile = CreateTestProfile();
        viewModel.Initialize(profile);

        viewModel.ProfileName = "New Name";

        Assert.False(viewModel.HasValidationErrors);
        Assert.True(viewModel.SaveCommand.CanExecute(null));
    }

#endregion

#region Property 7 : Process Whitelist

    [Fact]
    public void Initialize_WithProcessWhitelist_PreservesWhitelist()
    {
        var profile = CreateTestProfile(cursorDetection: new CursorDetectionConfig {
            Enabled = true,
            ProcessWhitelist = new System.Collections.Generic.List<string> { "genshin", "eldenring" }
        });
        var viewModel = new ProfileEditDialogViewModel(_mockProfileManager.Object);

        viewModel.Initialize(profile);

        Assert.Equal(2, viewModel.ProcessWhitelist.Count);
        Assert.Contains("genshin", viewModel.ProcessWhitelist);
        Assert.Contains("eldenring", viewModel.ProcessWhitelist);
    }

    [Fact]
    public void AddProcess_WithValidName_AddsToWhitelist()
    {
        var viewModel = new ProfileEditDialogViewModel(_mockProfileManager.Object);
        var profile = CreateTestProfile();
        viewModel.Initialize(profile);

        viewModel.NewProcessName = "testgame";
        viewModel.AddProcessCommand.Execute(null);

        Assert.Single(viewModel.ProcessWhitelist);
        Assert.Contains("testgame", viewModel.ProcessWhitelist);
        Assert.Empty(viewModel.NewProcessName);
    }

    [Fact]
    public void AddProcess_WithExeSuffix_RemovesSuffix()
    {
        var viewModel = new ProfileEditDialogViewModel(_mockProfileManager.Object);
        var profile = CreateTestProfile();
        viewModel.Initialize(profile);

        viewModel.NewProcessName = "testgame.exe";
        viewModel.AddProcessCommand.Execute(null);

        Assert.Single(viewModel.ProcessWhitelist);
        Assert.Contains("testgame", viewModel.ProcessWhitelist);
    }

    [Fact]
    public void AddProcess_WithDuplicateName_DoesNotAddDuplicate()
    {
        var viewModel = new ProfileEditDialogViewModel(_mockProfileManager.Object);
        var profile = CreateTestProfile();
        viewModel.Initialize(profile);

        viewModel.NewProcessName = "testgame";
        viewModel.AddProcessCommand.Execute(null);
        viewModel.NewProcessName = "testgame";
        viewModel.AddProcessCommand.Execute(null);

        Assert.Single(viewModel.ProcessWhitelist);
    }

    [Fact]
    public void RemoveProcess_RemovesFromWhitelist()
    {
        var viewModel = new ProfileEditDialogViewModel(_mockProfileManager.Object);
        var profile = CreateTestProfile(cursorDetection: new CursorDetectionConfig {
            Enabled = true, ProcessWhitelist = new System.Collections.Generic.List<string> { "game1", "game2" }
        });
        viewModel.Initialize(profile);

        viewModel.RemoveProcessCommand.Execute("game1");

        Assert.Single(viewModel.ProcessWhitelist);
        Assert.DoesNotContain("game1", viewModel.ProcessWhitelist);
        Assert.Contains("game2", viewModel.ProcessWhitelist);
    }

    [Fact]
    public void Save_WithProcessWhitelist_IncludesWhitelistInConfig()
    {
        ProfileUpdateData? capturedUpdateData = null;
        var mockManager = new Mock<IProfileManager>();
        mockManager.Setup(x => x.ProfileIcons).Returns(new[] { "📦", "🎮" });
        mockManager.Setup(x => x.UpdateProfile(It.IsAny<string>(), It.IsAny<ProfileUpdateData>()))
            .Callback<string, ProfileUpdateData>((id, data) => capturedUpdateData = data)
            .Returns(true);

        var viewModel = new ProfileEditDialogViewModel(mockManager.Object);
        var profile = CreateTestProfile();
        viewModel.Initialize(profile);

        viewModel.NewProcessName = "testgame";
        viewModel.AddProcessCommand.Execute(null);
        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(capturedUpdateData);
        Assert.NotNull(capturedUpdateData.CursorDetection);
        Assert.NotNull(capturedUpdateData.CursorDetection.ProcessWhitelist);
        Assert.Contains("testgame", capturedUpdateData.CursorDetection.ProcessWhitelist);
    }

    [Fact]
    public void WhitelistChange_TriggersHasChanges()
    {
        var viewModel = new ProfileEditDialogViewModel(_mockProfileManager.Object);
        var profile = CreateTestProfile();
        viewModel.Initialize(profile);

        Assert.False(viewModel.SaveCommand.CanExecute(null));

        viewModel.NewProcessName = "newgame";
        viewModel.AddProcessCommand.Execute(null);

        Assert.True(viewModel.SaveCommand.CanExecute(null));
    }

#endregion

#region Helper Methods

    private static GameProfile CreateTestProfile(string id = "test-profile", string name = "Test Profile",
                                                 string icon = "📦", ProfileDefaults? defaults = null,
                                                 CursorDetectionConfig? cursorDetection = null)
    {
        return new GameProfile { Id = id,
                                 Name = name,
                                 Icon = icon,
                                 Version = 1,
                                 Defaults =
                                     defaults ?? new ProfileDefaults { Url = "https://example.com", SeekSeconds = 5 },
                                 CursorDetection = cursorDetection };
    }

#endregion
}
}
