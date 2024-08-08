using System;
using System.Linq;
using System.Threading.Tasks;
using share.controller.game.events.core;
using share.controller.game.events.ServerEvents;
using share.manager;
using share.model.events.local.core;
using share.model.segmentation;
using share.model.user.events.core;
using share.model.user.events.local.core;
using share.utils;

namespace share.controller.game.events.local.core {
	public abstract class BaseLocalEventController<TData, TModel> : BaseEventController, IRemainingTimeProvider, IHaveIconDisplayRewards 
		where TData : ActiveLocalEventBase<TModel> where TModel : BaseLocalEventModel {
		
		protected readonly TModel Proto;
		protected TData Data;

		protected BaseLocalEventController(TModel model) {
			CurrentSource = ResourceSource.Events;
			Proto = model;
		}

		public override async Task CreateDataAndLoadGroups() {
			Data = (TData) SystemController.UserData.newEvents.Active.SingleOrDefault(d => d.GetProto<TModel>() == Proto);
			bool isNew = Data == null;
			if (isNew) {
				Data = NewDataCreator(Proto);
			}

			Conditions = Proto.Conditions;
			EventPacks = Proto.EventPacks;
			
			await LoadGroups(Proto.Id);
			CheckActual();
			
			if (isNew) {
				SystemController.UserData.newEvents.Active.Add(Data);
			}
		}

		public override void Init() {
			base.Init();

			ActivateListeners();
			CheckActual();
		}

		protected override void Tick() {
			base.Tick();
			if (SystemController.ViewManager.CurrentSceneIsMeta) {
				CheckActual();

				if (!Data.IsPaused()) {
					Update();
				}
			}
		}

		protected override void OnLateUpdate() {
			base.OnLateUpdate();

			if (!Data.IsPaused()) {
				if (CheckTimesUp()) {
					TimesUp();
				}
			}
		}

		private void CheckActual() {
			bool needPause = Proto.DeactivateOnOutOfSync && !SystemController.ServerController.GetServerTimeInMilliSeconds().IsSync;
			needPause = needPause || CheckPriority();
			bool isBlockedByScene = !EventsConfig.IsEventActiveAtScene(Data.eventType, SystemController.ViewManager.LastMetaScene);
			needPause = needPause || isBlockedByScene;

			Data.SetBlockByScene(isBlockedByScene);
			Data.SetPause(needPause);
		}

		private bool CheckPriority() {
			if (Proto.IconGroupPriority == null) return false;
			if (Proto.IconGroupPriority.priorityType != IconPriorityType.HideAndPause) return false;

			return !LocalEventsController.CheckPriorityByProto(Proto);
		}

		protected override void SceneShowing(Scene scene) {
			base.SceneShowing(scene);

			if (scene == Scene.Match3) {
				if (SystemController.UserData != null && SystemController.UserData.LevelData != null && SystemController.UserData.LevelData.isDaily && Data != null) {
					Data.DeactivatedForDailyLevel = true;
				}
			} else {
				Data.DeactivatedForDailyLevel = false;
			}
		}

		public T GetProto<T>() where T : TModel {
			return Proto as T;
		}

		public T GetData<T>() where T : TData {
			return Data as T;
		}

		protected abstract TData NewDataCreator(TModel model);

		protected override ActiveEventBase GetData() {
			return Data;
		}

		public Window GetMainWindow() {
			return Proto.MainWindow;
		}

		public Window GetHelpWindow() {
			return Proto.HelpWindow;
		}

		protected void CompleteEvent(bool finish, float currentProgress = -1) {
			base.CompleteEvent(Proto.Id);

			SystemController.GameController.LocalEventsController.CompleteEvent(Data, finish);
			if (currentProgress >= 0) {
				
				if (Enum.TryParse(Proto.Type.ToString(), out SegmentationEntityType segmentationEntityType)){
					SystemController.SegmentationController.Update(segmentationEntityType, currentProgress);
				}
				share.controller.core.SystemController.UpdateLocalEventsModel(Proto.Type);
			}
		}

		public virtual long GetRemainingTime() {
			return GetTime(Conditions.GetCurrentTime());
		}

		private long GetTime(long userTime) {
			return Data.startTimestamp + Proto.DurationInHours * TimeUtility.MillisecondsInHour - userTime;
		}

		protected virtual bool CheckTimesUp() {
			if (Proto == null || Conditions == null) return false;
			if (Proto.DurationInHours == -1) return false;
			return CheckTime(Conditions.GetCurrentTime());
		}

		private bool CheckTime(long time) {
			return time > Data.startTimestamp + Proto.DurationInHours * TimeUtility.MillisecondsInHour;
		}
	}
}