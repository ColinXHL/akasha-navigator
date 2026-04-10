using System;
using System.Collections.Generic;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.PioneerNote;
using AkashaNavigator.Services;
using AkashaNavigator.Views.Dialogs;
using Xunit;

namespace AkashaNavigator.Tests.Views;

public class ExitRecordPromptTests
{
    [Fact]
    public void ShouldShowPrompt_ReturnsFalse_WhenUrlAlreadyRecorded()
    {
        var service = new FakePioneerNoteService(isRecorded: true);

        var result = ExitRecordPrompt.ShouldShowPrompt("https://example.com", service);

        Assert.False(result);
    }

    [Fact]
    public void ShouldShowPrompt_ReturnsTrue_WhenUrlNotRecorded()
    {
        var service = new FakePioneerNoteService(isRecorded: false);

        var result = ExitRecordPrompt.ShouldShowPrompt("https://example.com", service);

        Assert.True(result);
    }

    private sealed class FakePioneerNoteService : IPioneerNoteService
    {
        private readonly bool _isRecorded;

        public FakePioneerNoteService(bool isRecorded)
        {
            _isRecorded = isRecorded;
        }

        public SortDirection CurrentSortOrder { get; set; }

        public bool IsUrlRecorded(string url) => _isRecorded;

        public NoteItem RecordNote(string url, string title, string? folderId = null) => throw new NotImplementedException();
        public void UpdateNote(string id, string newTitle, string? newUrl = null) => throw new NotImplementedException();
        public void DeleteNote(string id) => throw new NotImplementedException();
        public void MoveNote(string id, string? targetFolderId) => throw new NotImplementedException();
        public NoteItem? GetNoteById(string id) => throw new NotImplementedException();
        public NoteFolder CreateFolder(string name, string? parentId = null) => throw new NotImplementedException();
        public void UpdateFolder(string id, string newName) => throw new NotImplementedException();
        public void DeleteFolder(string id, bool cascade = true) => throw new NotImplementedException();
        public NoteFolder? GetFolderById(string id) => throw new NotImplementedException();
        public bool FolderExists(string id) => throw new NotImplementedException();
        public PioneerNoteData GetNoteTree() => throw new NotImplementedException();
        public List<NoteItem> GetItemsByFolder(string? folderId) => throw new NotImplementedException();
        public List<NoteFolder> GetFoldersByParent(string? parentId) => throw new NotImplementedException();
        public List<NoteItem> GetSortedItems(SortDirection direction) => throw new NotImplementedException();
        public List<NoteItem> SearchNotes(string keyword) => throw new NotImplementedException();
        public SortDirection ToggleSortOrder() => throw new NotImplementedException();
        public int GetItemCount() => throw new NotImplementedException();
        public int GetFolderCount() => throw new NotImplementedException();
        public List<NoteItem> GetAllNotes() => throw new NotImplementedException();
        public List<NoteItem> GetNotesInFolder(string? folderId) => throw new NotImplementedException();
        public void RenameFolder(string folderId, string newName) => throw new NotImplementedException();
        public void DeleteFolder(string folderId) => throw new NotImplementedException();
        public List<NoteFolder> GetAllFolders() => throw new NotImplementedException();
        public void MoveNoteToFolder(string noteId, string? targetFolderId) => throw new NotImplementedException();
        public void ClearAll() => throw new NotImplementedException();
    }
}
