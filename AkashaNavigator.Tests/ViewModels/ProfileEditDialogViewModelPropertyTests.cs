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
    public void Initialize_WithValidProfile_LoadsBasicInfo()
    {
        var profile = CreateTestProfile(name: "Test Profile", icon: "🎮");
        var viewModel = new ProfileEditDialogViewModel(_mockProfileManager.Object);

        viewModel.Initialize(profile);

        Assert.Equal("Test Profile", viewModel.ProfileName);
        Assert.Equal("🎮", viewModel.SelectedIcon);
    }

    [Fact]
    public void Initialize_WithDefaults_LoadsDefaultSettings()
    {
        var profile =
            CreateTestProfile(defaults: new ProfileDefaults { Url = "https://example.com", SeekSeconds = 15 });
        var viewModel = new ProfileEditDialogViewModel(_mockProfileManager.Object);

        viewModel.Initialize(profile);

        Assert.Equal("https://example.com", viewModel.DefaultUrl);
        Assert.Equal(15, viewModel.SeekSeconds);
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

#region Property 6 : Save Behavior

    [Fact]
    public void Save_WithValidChanges_CallsProfileManager()
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

        viewModel.ProfileName = "Updated Name";
        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(capturedUpdateData);
        Assert.Equal("Updated Name", capturedUpdateData.Name);
    }

    [Fact]
    public void Save_WithDefaultsChange_UpdatesDefaults()
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

        viewModel.DefaultUrl = "https://newurl.com";
        viewModel.SeekSeconds = 20;
        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(capturedUpdateData);
        Assert.NotNull(capturedUpdateData.Defaults);
        Assert.Equal("https://newurl.com", capturedUpdateData.Defaults.Url);
        Assert.Equal(20, capturedUpdateData.Defaults.SeekSeconds);
    }

#endregion

#region Property 7 : Icon Selection

    [Fact]
    public void Initialize_LoadsAvailableIcons()
    {
        var viewModel = new ProfileEditDialogViewModel(_mockProfileManager.Object);
        var profile = CreateTestProfile();

        viewModel.Initialize(profile);

        Assert.NotEmpty(viewModel.AvailableIcons);
        Assert.Contains("📦", viewModel.AvailableIcons);
        Assert.Contains("🎮", viewModel.AvailableIcons);
    }

    [Fact]
    public void IconChange_TriggersHasChanges()
    {
        var viewModel = new ProfileEditDialogViewModel(_mockProfileManager.Object);
        var profile = CreateTestProfile(icon: "📦");
        viewModel.Initialize(profile);

        Assert.False(viewModel.SaveCommand.CanExecute(null));

        viewModel.SelectedIcon = "🎮";

        Assert.True(viewModel.SaveCommand.CanExecute(null));
    }

#endregion

#region Helper Methods

    private static GameProfile CreateTestProfile(string id = "test-profile", string name = "Test Profile",
                                                 string icon = "📦", ProfileDefaults? defaults = null)
    {
        return new GameProfile { Id = id, Name = name, Icon = icon, Version = 1,
                                 Defaults =
                                     defaults ?? new ProfileDefaults { Url = "https://example.com", SeekSeconds = 5 } };
    }

#endregion
}
}
