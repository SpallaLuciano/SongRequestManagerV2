using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.SimpleJsons;
using System;
using Zenject;

namespace SongRequestManagerV2.Models
{
    public class GameStateBroadcaster : IInitializable, IDisposable
    {
        private readonly ScoreController _scoreController;
        private readonly StandardLevelScenesTransitionSetupDataSO _transition;
        private readonly GameplayCoreSceneSetupData _sceneData;
        private readonly GameEnergyCounter _energyCounter;
        private readonly IChatManager _chatManager;

        [Inject]
        public GameStateBroadcaster(
            ScoreController scoreController,
            GameEnergyCounter energyCounter,
            StandardLevelScenesTransitionSetupDataSO transition,
            GameplayCoreSceneSetupData sceneData,
            IChatManager chatManager)
        {
            _scoreController = scoreController;
            _energyCounter = energyCounter;
            _transition = transition;
            _sceneData = sceneData;
            _chatManager = chatManager;
        }

        public void Initialize()
        {
            SendStartEvent();
            if (_scoreController != null)
            {
                this._scoreController.scoreDidChangeEvent += OnScoreChanged;
            }
            if (this._transition != null)
            {
                _transition.didFinishEvent += OnLevelFinished;
            }
            if (_energyCounter != null)
            {
                _energyCounter.gameEnergyDidReach0Event += OnLevelFailed;
            }
        }

        private void SendStartEvent()
        {
            var data = CreateSongData();
            _chatManager.SendEventToStreamerbotServer("GameStarted", data);
        }

        private void OnScoreChanged(int multiplier, int modifiedScore)
        {
            var data = CreateSongData();
            data["modifiedScore"] = modifiedScore;
            data["multiplier"] = multiplier;
            _chatManager.SendEventToStreamerbotServer("ScoreChanged", data);
        }

        private void OnLevelFailed()
        {
            var data = CreateSongData();
            _chatManager.SendEventToStreamerbotServer("LevelFailed", data);
            Cleanup();
        }

        private void OnLevelFinished(StandardLevelScenesTransitionSetupDataSO data, LevelCompletionResults results)
        {
            var json = CreateSongData();
            json["cleared"] = results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared;
            json["score"] = results.modifiedScore;
            _chatManager.SendEventToStreamerbotServer("GameEnded", json);
            Cleanup();
        }

        private JSONObject CreateSongData()
        {
            var json = new JSONObject();
            if (SongInfomationProvider.CurrentSongLevel != null)
            {
                json["id"] = SongInfomationProvider.CurrentSongLevel["id"].Value;
                json["name"] = SongInfomationProvider.CurrentSongLevel["metadata"]["songName"].Value;
            }
            else
            {
                json["id"] = _sceneData.beatmapKey.levelId;
                json["name"] = _sceneData.beatmapLevel.songName;
            }
            return json;
        }

        private void Cleanup()
        {
            if (_scoreController != null)
            {
                _scoreController.scoreDidChangeEvent -= OnScoreChanged;
            }
            if (this._transition != null)
            {
                _transition.didFinishEvent -= OnLevelFinished;
            }
            if (_energyCounter != null)
            {
                _energyCounter.gameEnergyDidReach0Event -= OnLevelFailed;
            }
        }

        public void Dispose()
        {
            Cleanup();
        }
    }
}

