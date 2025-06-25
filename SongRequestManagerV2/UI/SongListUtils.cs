using HMUI;
using IPA.Utilities;
using SongCore;
using SongRequestManagerV2.Configuration;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;
using Zenject;

namespace SongRequestManagerV2.UI
{
    public class SongListUtils
    {
        [Inject]
        private readonly LevelCollectionViewController _levelCollectionViewController;
        [Inject]
        private readonly SelectLevelCategoryViewController _selectLevelCategoryViewController;
        [Inject]
        private readonly GameplaySetupViewController _gameplaySetupViewController;
        [Inject]
        private readonly LevelFilteringNavigationController _levelFilteringNavigationController;
        [Inject]
        private readonly AnnotatedBeatmapLevelCollectionsViewController _annotatedBeatmapLevelCollectionsViewController;
        private void SelectCustomSongPack(int index)
        {
            // get the Level Filtering Nav Controller, the top bar
            // get the tab bar
            var segcontrol = this._selectLevelCategoryViewController.GetField<IconSegmentedControl, SelectLevelCategoryViewController>("_levelFilterCategoryIconSegmentedControl");
            segcontrol.SelectCellWithNumber(index);
            var segControl = this._selectLevelCategoryViewController
                .GetField<IconSegmentedControl, SelectLevelCategoryViewController>("_levelFilterCategoryIconSegmentedControl");
            segControl.SelectCellWithNumber(index);

            var selectCategoryMethod = typeof(SelectLevelCategoryViewController)
                .GetMethod("HandleLevelFilterCategoryIconSegmentedControlDidSelectCell",
                BindingFlags.NonPublic | BindingFlags.Instance);
            _ = selectCategoryMethod?.Invoke(this._selectLevelCategoryViewController, new object[] { index });
        }

        public IEnumerator ScrollToLevel(string levelID, Action callback, bool isWip = false)
        {
            if (this._levelCollectionViewController) {
                // Make sure our custom songpack is selected
                // Index 2 corresponds to the "Custom Levels" tab in newer game versions
                // Using index 1 would select the "Favorites" tab and trigger an
                // ArgumentOutOfRangeException in UpdateCustomSongs
                this.SelectCustomSongPack(2);

                var updateCustomSongsMethod = typeof(LevelFilteringNavigationController)
                    .GetMethod("UpdateCustomSongs", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                try {
                    _ = updateCustomSongsMethod?.Invoke(this._levelFilteringNavigationController, new object[0]);
                }
                catch (TargetInvocationException e) when (e.InnerException is ArgumentOutOfRangeException) {
                    Logger.Debug("UpdateCustomSongs failed due to invalid category");
                }

                yield return new WaitWhile(() => this._levelFilteringNavigationController.GetField<CancellationTokenSource, LevelFilteringNavigationController>("_cancellationTokenSource") != null);
                var gridView = this._annotatedBeatmapLevelCollectionsViewController.GetField<AnnotatedBeatmapLevelCollectionsGridView, AnnotatedBeatmapLevelCollectionsViewController>("_annotatedBeatmapLevelCollectionsGridView");
                gridView.SelectAndScrollToCellWithIdx(isWip ? 1 : 0);
                var customSong = isWip
                    ? gridView._annotatedBeatmapLevelCollections.ElementAt(1)
                    : gridView._annotatedBeatmapLevelCollections.FirstOrDefault();
                var selectCollectionMethod = typeof(AnnotatedBeatmapLevelCollectionsViewController)
                    .GetMethod("HandleDidSelectAnnotatedBeatmapLevelCollection", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                _ = selectCollectionMethod?.Invoke(this._annotatedBeatmapLevelCollectionsViewController, new object[] { customSong });
                var song = isWip ? Loader.GetLevelById($"custom_level_{levelID.Split('_').Last().ToUpper()} WIP") : Loader.GetLevelByHash(levelID.Split('_').Last());
                if (song == null) {
                    yield break;
                }
                // handle if song browser is present
                if (BetterSongListController.BetterSongListPluginPresent) {
                    BetterSongListController.ClearFilter();
                }
                else if (SongBrowserController.SongBrowserPluginPresent) {
                    SongBrowserController.SongBrowserCancelFilter();
                }
                yield return null;
                // get the table view
                var levelsTableView = this._levelCollectionViewController.GetField<LevelCollectionTableView, LevelCollectionViewController>("_levelCollectionTableView");
                levelsTableView.SelectLevel(song);

                StandardLevelDetailViewController detailViewController = null;
                try {
                    detailViewController = this._levelCollectionViewController.GetField<StandardLevelDetailViewController, LevelCollectionViewController>("_levelDetailViewController");
                }
                catch (MissingFieldException) {
                    var propertyInfo = typeof(LevelCollectionViewController).GetProperty("levelDetailViewController", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    detailViewController = propertyInfo?.GetValue(this._levelCollectionViewController) as StandardLevelDetailViewController;
                }

                if (detailViewController != null) {
                    var diffControl = detailViewController.GetField<object, StandardLevelDetailViewController>("_beatmapDifficultySegmentedControl");
                    if (diffControl != null && Enum.TryParse(RequestBotConfig.Instance.DefaultDifficulty, out BeatmapDifficulty diff)) {
                        var diffControlType = diffControl.GetType();
                        var selectDiffMethod = diffControlType.GetMethod("SelectCellWithNumber", BindingFlags.Instance | BindingFlags.Public);
                        selectDiffMethod ??= diffControlType.GetMethod("SelectCellWithNumberWithoutNotify", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        selectDiffMethod?.Invoke(diffControl, new object[] { (int)diff });
                    }
                }
            }
            if (RequestBotConfig.Instance?.ClearNoFail == true) {
                var gameplayModifiersPanelController = this._gameplaySetupViewController
                    .GetField<GameplayModifiersPanelController, GameplaySetupViewController>("_gameplayModifiersPanelController");

                gameplayModifiersPanelController.gameplayModifiers.SetField("_noFailOn0Energy", false);
                var refreshPanelMethod = typeof(GameplayModifiersPanelController)
                    .GetMethod("RefreshActivePanel", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                _ = refreshPanelMethod?.Invoke(gameplayModifiersPanelController, new object[0]);
            }
            callback?.Invoke();
        }
    }
}
