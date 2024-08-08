using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using share.controller.core;
using share.manager;
using share.model.events.core;
using share.model.user.events.core;
using share.model.user.life;
using UnityEngine;

namespace share.controller.game.events.core {
	public abstract class BaseEventController : BaseController, IGlobalUpdatable, IDisposable, IEventController {
		protected EventConditions Conditions;
		protected EventPacks EventPacks;
		protected ResourceSource CurrentSource;

		public bool HasUnseenRewards { get; protected set; }
		public int CurrentRewards { get; protected set; }
		public int SeenRewards { get; protected set; }

		public virtual void Init() {
			GetData().Init();
		}
		
		protected async Task LoadGroups(string id) {
			if (Conditions != null && Conditions.PacksId.Count > 0 && EventPacks != null) {
				GetData().SetPause(true);
				
				LocalResources localResources = SystemController.ResourceManager.LoadGroups(EventPacks.InMemoryPacks, CurrentSource, id);
				SystemController.ResourceManager.AddLocalResourcesGroup(GetData().LoadingID, localResources);
				await localResources.LoadingTask;

				GetData().SetPause(false);
			}
		}

		public virtual void Dispose() {
			DeActivateListeners();

			GetData().Reset();
		}

		public Tuple<List<ResourceGroup>, string> GetPacksForNextScene(Scene scene) {
			if (EventPacks == null || scene == Scene.Match3 && !EventPacks.ActiveInMatch3) {
				return new Tuple<List<ResourceGroup>, string>(new List<ResourceGroup>(), "");
			}

			return new Tuple<List<ResourceGroup>, string>(EventPacks.InMemoryPacks, GetEventId());
		}

		public Tuple<List<ResourceGroup>, string> GetUnusedPacks(Scene scene) {
			if (EventPacks != null && scene == Scene.Match3 && !EventPacks.ActiveInMatch3) {
				return new Tuple<List<ResourceGroup>, string>(EventPacks.InMemoryPacks, GetEventId());
			}

			return new Tuple<List<ResourceGroup>, string>(new List<ResourceGroup>(), "");
		}

		public void SetRewardsSeen() {
			SeenRewards = CurrentRewards;
			HasUnseenRewards = false;
		}
		
		public abstract Task CreateDataAndLoadGroups();

		public void GlobalUpdate() => Tick();

		protected virtual void Tick() {
		}

		protected virtual void Update() {
		}

		protected virtual void OnLateUpdate() {
		}

		protected virtual void TimesUp() {
			CoroutineHelper.Instance.RemoveFromGlobalUpdate(this);
			CoroutineHelper.Instance.OnLateUpdate -= OnLateUpdate;
		}

		protected virtual void OnLevelCompleted(int oldValue, int newValue) {
		}

		protected virtual void OnLevelAttemptsChanged(int value) {
		}

		protected virtual void OnLivesChanged(LifeDataType lifeDataType, int oldValue, int newValue, int delta) {
		}

		protected virtual void SceneShowing(Scene scene) {
			if (!SystemController.ViewManager.WorldSceneShownOneTime) {
				SetRewardsSeen();
			}
		}

		protected virtual void SceneShown(Scene scene) {
		}

		public bool OnPause() {
			return GetData().IsPaused();
		}

		public string GetEventId() {
			return GetData().id;
		}

		protected abstract ActiveEventBase GetData();

		protected void SaveData() {
			_ = SystemController.GameController.UserController.SaveUserData();
		}

		protected void ActivateListeners() {
			CoroutineHelper.Instance.AddToGlobalUpdate(this);
			CoroutineHelper.Instance.OnLateUpdate += OnLateUpdate;

			SystemController.UserData.gameData.MaxCompletedMatch3LevelChanged += OnLevelCompleted;
			SystemController.UserData.gameData.LevelAttemptsChanged += OnLevelAttemptsChanged;
			SystemController.UserData.additionalLives.Changed += OnLivesChanged;
			SystemController.ViewManager.SceneShown += SceneShown;
			SystemController.ViewManager.SceneShowing += SceneShowing;
		}

		protected void DeActivateListeners() {
			CoroutineHelper.Instance.RemoveFromGlobalUpdate(this);
			CoroutineHelper.Instance.OnLateUpdate -= OnLateUpdate;
			SystemController.UserData.gameData.MaxCompletedMatch3LevelChanged -= OnLevelCompleted;
			SystemController.UserData.gameData.LevelAttemptsChanged -= OnLevelAttemptsChanged;
			SystemController.UserData.additionalLives.Changed -= OnLivesChanged;
			SystemController.ViewManager.SceneShown -= SceneShown;
			SystemController.ViewManager.SceneShowing -= SceneShowing;
		}

		protected void CompleteEvent(string id) {
			DeActivateListeners();
			GetData().Reset();

			if (EventPacks != null) {
				SystemController.ResourceManager.ReleaseGroups(EventPacks.ReleaseOnCompletePacks, CurrentSource, false, id);
			}
		}
	}
}