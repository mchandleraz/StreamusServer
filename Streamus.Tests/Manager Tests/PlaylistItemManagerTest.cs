﻿using FluentValidation;
using NUnit.Framework;
using Streamus.Domain;
using Streamus.Domain.Interfaces;
using System;

namespace Streamus.Tests.Manager_Tests
{
    [TestFixture]
    public class PlaylistItemManagerTest : StreamusTest
    {
        private User User { get; set; }
        private Playlist Playlist { get; set; }

        private IPlaylistItemManager PlaylistItemManager;
        private IPlaylistManager PlaylistManager;
        private IVideoManager VideoManager;

        /// <summary>
        ///     This code is only ran once for the given TestFixture.
        /// </summary>
        [TestFixtureSetUp]
        public new void TestFixtureSetUp()
        {
            try
            {
                PlaylistItemManager = ManagerFactory.GetPlaylistItemManager();
                PlaylistManager = ManagerFactory.GetPlaylistManager();
                VideoManager = ManagerFactory.GetVideoManager();
            }
            catch (TypeInitializationException exception)
            {
                throw exception.InnerException;
            }

            //  Ensure that a User exists.
            User = Helpers.CreateUser();
        }

        /// <summary>
        ///     This code runs before every test.
        /// </summary>
        [SetUp]
        public void SetupContext()
        {
            //  Make a new Playlist object each time to ensure no side-effects from previous test case.
            Playlist = User.CreateAndAddPlaylist();
            PlaylistManager.Save(Playlist);
        }

        /// <summary>
        ///     Create a new PlaylistItem with a Video whose ID is not in the database currently.
        /// </summary>
        [Test]
        public void CreateItem_NoVideoExists_VideoAndItemCreated()
        {
            PlaylistItem playlistItem = Helpers.CreateItemInPlaylist(Playlist);

            //  Ensure that the Video was created.
            Video videoFromDatabase = VideoManager.Get(playlistItem.Video.Id);
            Assert.NotNull(videoFromDatabase);

            //  Ensure that the PlaylistItem was created.
            PlaylistItem itemFromDatabase = PlaylistItemManager.Get(playlistItem.Id);
            Assert.NotNull(itemFromDatabase);

            //  Should have a sequence number after saving for sure.
            Assert.GreaterOrEqual(itemFromDatabase.Sequence, 0);

            //  Pointers should be self-referential with only one item in the Playlist.
            //Assert.AreEqual(itemFromDatabase.NextItem, itemFromDatabase);
            //Assert.AreEqual(itemFromDatabase.PreviousItem, itemFromDatabase);
        }

        /// <summary>
        ///     Create a new PlaylistItem with a Video whose ID is in the database.
        ///     No update should happen to the Video as it is immutable.
        /// </summary>
        [Test]
        public void CreateItem_VideoAlreadyExists_ItemCreatedVideoNotUpdated()
        {
            Video videoNotInDatabase = Helpers.CreateUnsavedVideoWithId();

            VideoManager.Save(videoNotInDatabase);

            //  Change the title for videoInDatabase to check that cascade-update does not affect title. Videos are immutable.
            const string videoTitle = "A video title";
            Video videoInDatabase = Helpers.CreateUnsavedVideoWithId(titleOverride: videoTitle);

            //  Create a new PlaylistItem and write it to the database.
            string title = videoInDatabase.Title;
            var playlistItem = new PlaylistItem(title, videoInDatabase);

            Playlist.AddItem(playlistItem);

            PlaylistItemManager.Save(playlistItem);

            //  Ensure that the Video was NOT updated by comparing the new title to the old one.
            Video videoFromDatabase = VideoManager.Get(videoNotInDatabase.Id);
            Assert.AreNotEqual(videoFromDatabase.Title, videoTitle);

            //  Ensure that the PlaylistItem was created.
            PlaylistItem itemFromDatabase = PlaylistItemManager.Get(playlistItem.Id);
            Assert.NotNull(itemFromDatabase);

            //  Should have a sequence number after saving for sure.
            Assert.GreaterOrEqual(itemFromDatabase.Sequence, 0);
        }

        [Test]
        public void CreateItem_NotAddedToPlaylistBeforeSave_ItemNotAdded()
        {
            Video videoNotInDatabase = Helpers.CreateUnsavedVideoWithId();

            //  Create a new PlaylistItem and write it to the database.
            string title = videoNotInDatabase.Title;
            var playlistItem = new PlaylistItem(title, videoNotInDatabase);

            bool validationExceptionEncountered = false;

            try
            {
                //  Try to save the item without adding to Playlist. This should fail.
                PlaylistItemManager.Save(playlistItem);
            }
            catch (ValidationException)
            {
                validationExceptionEncountered = true;
            }

            Assert.IsTrue(validationExceptionEncountered);
        }

        [Test]
        public void UpdateItemTitle_ItemExistsInDatabase_ItemTitleUpdated()
        {
            //  Create and save a playlistItem to the database.
            PlaylistItem playlistItem = Helpers.CreateItemInPlaylist(Playlist);

            //  Change the item's title.
            const string updatedItemTitle = "Updated PlaylistItem title";
            playlistItem.Title = updatedItemTitle;

            PlaylistItemManager.Update(playlistItem);

            //  Check the title of the item from the database -- make sure it updated.
            PlaylistItem itemFromDatabase = PlaylistItemManager.Get(playlistItem.Id);
            Assert.AreEqual(itemFromDatabase.Title, updatedItemTitle);
        }

        /// <summary>
        ///     Test deleting a single PlaylistItem. The Playlist itself should not have any items in it.
        /// </summary>
        [Test]
        public void DeletePlaylistItem_NoOtherItems_MakesPlaylistEmpty()
        {
            PlaylistItem playlistItem = Helpers.CreateItemInPlaylist(Playlist);

            //  Now delete the created PlaylistItem and ensure it is removed.
            PlaylistItemManager.Delete(playlistItem.Id);

            PlaylistItem deletedPlaylistItem = PlaylistItemManager.Get(playlistItem.Id);
            Assert.IsNull(deletedPlaylistItem);
        }

        /// <summary>
        ///     Remove the first PlaylistItem from a Playlist which has two items in it after adds.
        ///     Leave the second item, but adjust linked list pointers to accomodate.
        /// </summary>
        [Test]
        public void DeleteFirstPlaylistItem_OneOtherItemInPlaylist_LeaveSecondItemAndUpdatePointers()
        {
            //  Create the first PlaylistItem and write it to the database.
            PlaylistItem firstItem = Helpers.CreateItemInPlaylist(Playlist);

            //  Create the second PlaylistItem and write it to the database.
            PlaylistItem secondItem = Helpers.CreateItemInPlaylist(Playlist);

            //  Now delete the first PlaylistItem and ensure it is removed.
            PlaylistItemManager.Delete(firstItem.Id);

            PlaylistItem deletedPlaylistItem = PlaylistItemManager.Get(firstItem.Id);
            Assert.IsNull(deletedPlaylistItem);
        }

        //  TODO: Test case where there are 2 PlaylistItems in the Playlist before deleting.
        //  TODO: Test bulk-delete in one transaction.
    }
}
