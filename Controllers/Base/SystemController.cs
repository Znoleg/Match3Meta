using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Controllers.World;
using match3.gamefield.models.gamefield;
using Newtonsoft.Json;
using share.controller.ab_test;
using share.controller.bank;
using share.controller.external;
using share.controller.game;
using share.controller.game.Collectibles;
using share.controller.GUI;
using share.controller.GUI.core;
using share.controller.hard_update;
using share.controller.helpshift;
using share.controller.internet;
using share.controller.locale;
using share.controller.segmentation;
using share.controller.server;
using share.controller.server.CustomExpansionsLoaders;
using share.controller.statistic;
using share.controller.statistic.eventDummies.core;
using share.controller.statistic.sender;
using share.controller.user;
using share.manager;
using share.manager.InputManager;
using share.model.ab_test;
using share.model.AD;
using share.model.balance;
using share.model.bank;
using share.model.boosters;
using share.model.bubbles;
using share.model.collectibles;
using share.model.clans;
using share.model.core;
using share.model.dailyLevels;
using share.model.dailyQuest;
using share.model.dialogs;
using share.model.events.hub.core;
using share.model.events.local.core;
using share.model.events.serverEvents;
using share.model.events.serverEventsAd;
using share.model.expansion;
using share.model.hard_update;
using share.model.level_cap;
using share.model.levelDifficult;
using share.model.luckyBuy;
using share.model.match3level;
using share.model.minigames.Core;
using share.model.notifications;
using share.model.playerForm;
using share.model.profile;
using share.model.quest;
using share.model.return_rewards;
using share.model.screenplay;
using share.model.segmentation;
using share.model.Territory;
using share.model.tutorial;
using share.model.user;
using share.model.user.boosters;
using share.model.user.profile;
using share.utils;
using Sound;
using UI.Rewards;
using UnityEngine;
using UnityEngine.Audio;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using ResourceManager = share.manager.ResourceManager;

namespace share.controller.core {
	public sealed class SystemController : BaseController, ISystemController {
		public const string AcceptPolicy = "AcceptPolicy";
		public const string WatchedCartoon = "WatchedCartoon";
		
		public new SystemModel SystemModel { get; private set; }
		public UserData UserData { get; set; }

		public GameController GameController { get; private set; }
		public ServerController ServerController { get; private set; }
		public StatisticController StatisticController { get; private set; }
		public ExternalController ExternalController { get; private set; }
		public BankController BankController { get; private set; }
		public ViewManager ViewManager { get; private set; }
		public IResourceManager ResourceManager { get; private set; }
		public CameraManager CameraManager { get; private set; }
		public ParticleSystemController ParticleSystemController { get; private set; }
		public LoaderController LoaderController { get; private set; }
		public ABTestController ABTestController { get; private set; }
		public SegmentationController SegmentationController { get; private set; }
		public SoftUpdateController SoftUpdateController { get; private set; }
		public HardUpdateController HardUpdateController { get; private set; }
		public IExpansionController ExpansionController { get; private set; }
		public InternetController InternetController { get; private set; }
		public StateConflictController StateConflictController { get; private set; }
		public FileNameFinder FileNameFinderController { get; private set; }
		public LocaleController LocaleController { get; private set; }

		public bool Silent { get; set; } = true;
		/*public NotificationController NotificationController { get; private set; }*/

		private Scene _startScene;
		
		private AcceptPolicysWindowController _acceptPolicysWindow;

		private bool _started;
		private List<ResourceGroup> _groupsNeedToLoad;
		private List<ResourceGroup> _groupsNeedToLoadWithoutAwait;

		private Canvas _mainCanvas;
		private Canvas _loaderCanvas;
		public Canvas MainCanvas => _mainCanvas;
		private Camera _worldCamera;
		private AudioMixer _audioMixer;
		private Transform _mainTransform;
		private readonly HelpshiftCallbacksReceiver _helpshiftCallbacksReceiver;
		private readonly RewardItemDatabase _rewardDatabase;
		/*public GameNotificationsManager _notificationsManager = default;*/

		private bool _editorBuild = false;
		
		public SystemController( Canvas mainCanvas, Canvas loaderCanvas, Transform mainTransform, Camera worldCamera, AudioMixer audioMixer, HelpshiftCallbacksReceiver helpshiftCallbacksReceiver, RewardItemDatabase rewardDatabase, IInputModule inputModule) {
			_rewardDatabase = rewardDatabase;
			ServerController = new ServerController();
			
			LocaleController = new LocaleController(null);
			LocaleController.LoadDefaultLanguage();
			
			try
			{
				SetLanguageFromPrefs();
				
				if ( !PlayerPrefs.HasKey( StatisticSender.AlreadySendEnterPoint ) ) {
					StatisticController.SendGdprEvent( StatisticEventActionsName.Gdpr, StatisticEventActionsAction.EnterPoint, VersionOrigin.GetIntAppVersion() );
					PlayerPrefs.SetInt( StatisticSender.AlreadySendEnterPoint, 1 );
					PlayerPrefs.Save();
				}
			} catch ( Exception ) {
				// ignored
			}

			SystemController = this;
			BaseViewerController.SystemController = this;

			SoftUpdateController = new SoftUpdateController();
			HardUpdateController = new HardUpdateController();
			ViewManager = new ViewManager( mainCanvas, mainTransform, new ViewConfig() );
			ViewManager.InputModuleOverride = inputModule;
			ResourceManager resourceManager = new ResourceManager(CoroutineHelper.Instance);
			ResourceManager = resourceManager;
			ExpansionController expansionController = new(resourceManager);
			resourceManager.SetExpansionController(expansionController);
			resourceManager.SetSoftUpdateController(SoftUpdateController);
			ExpansionController = expansionController;
			
			_startScene = Scene.World;

			string[] arguments = Environment.GetCommandLineArgs();
			/*
			 * режим работы для редактора уровней
			 */
			for (int i = 0; i < arguments.Length; i++) {
				if (arguments[i].ToLower() == "-testlevel" && i + 1 < arguments.Length) {
					StartLevelController.testLevelURL = arguments[i + 1];
				}
			}

			if (VersionUtility.IsMetaOffVersion()) {
				_startScene = Scene.Match3;

				_groupsNeedToLoad = new List<ResourceGroup> {
					ResourceGroup.core,
					ResourceGroup.configs,
					ResourceGroup.gui,
				};
				_groupsNeedToLoadWithoutAwait = new List<ResourceGroup>()
				{
					ResourceGroup.profile_icons,
					ResourceGroup.vfx
				};
			}
			else
			{
				_groupsNeedToLoad = new List<ResourceGroup> {
					ResourceGroup.core,
					ResourceGroup.configs
				};
				_groupsNeedToLoadWithoutAwait = new List<ResourceGroup>()
				{
					ResourceGroup.gui,
					ResourceGroup.profile_icons,
					ResourceGroup.vfx
				};
			}
			
			_audioMixer = audioMixer;
			_mainCanvas = mainCanvas;
			_loaderCanvas = loaderCanvas;
			_worldCamera = worldCamera;
			_helpshiftCallbacksReceiver = helpshiftCallbacksReceiver;
			/*_notificationsManager = notificationsManager;*/
			
			if (PlayerPrefs.HasKey(AcceptPolicy)) {
				PlayerPrefs.SetString(WatchedCartoon, "Watched");
			}
			
			Start();
		}

		private async void Start() {
			await LocaleController.LoadLanguages();
			LoaderController = new LoaderController( _loaderCanvas );
			Task loadTask = LoaderController.StartLoading(Scene.World, false, true, 0, false);
			// await Task.Delay(1000);
			Task resourceManagerInitTask = ResourceManager.Initialize();
			Debug.Log("check internet");
			if (ServerController.CheckInternet()) {
				Debug.Log("soft");
				await SoftUpdateController.Check();
			}
			SoftUpdateController.CheckFiles();
			Debug.Log("resourceManagerInitTask");
			await resourceManagerInitTask;
			Debug.Log("ExpansionController.Initialize");
			await ExpansionController.Initialize();
			#if UNITY_WEBGL
			// webgl required to download local packs
			await ExpansionController.DownloadLocalPacks();
#endif
			Debug.Log("loadTask");
			await loadTask;
			await ViewManager.Init();
			await LoadResources();
		}
		
		private async Task LoadResources()
		{
			Debug.Log( "Start loading resources" );
			var resources = ResourceManager.LoadGroups( _groupsNeedToLoad, ResourceSource.Constant, loadWithoutLoadsQueue:true );
			Handler_Progress(0);
			while (!resources.IsLoaded)
			{
				await ATask.Yield();	
				Handler_Progress(resources.Progress * 0.6f);
			}
			ResourceManager.LoadGroups( _groupsNeedToLoadWithoutAwait, ResourceSource.Constant, loadWithoutLoadsQueue:true );
			
			AfterResLoaded();
		}

		private bool _loadingStarted;
		private void AfterResLoaded() {
			if ( _loadingStarted ) return;
			_loadingStarted = true;

#if UNITY_EDITOR
			SetConfigsFormat();	
#endif

			SystemModel = LoadBinAsset<SystemModel>("main_config_bin");
			
			// этот конфиг должен грузиться раньше остальных так как в нем содержатся данные котрые могут повлиять на загрузку других конфигов
			SystemModel.ABTestModel = LoadBinAsset<ABTestModel>("ab_test_bin");

			if ( PlayerPrefs.HasKey( AcceptPolicy ) || _startScene == Scene.Match3) {
				// Debug.Log("Prefs Has Key");
				ContinueAfterAcceptGDPR();
			} else {
				// Debug.Log("Need Show GDPR Window");
				_acceptPolicysWindow = Object.Instantiate(
					Resources.Load<AcceptPolicysWindowController>( "AcceptPolicys" ), _loaderCanvas.transform );
				_acceptPolicysWindow.ShowWithParameters( ContinueAfterAcceptGDPR );
			}
		}
		
		private bool _gdprAccepted;

		private async void ContinueAfterAcceptGDPR() {
			if ( _gdprAccepted ) return;
			_gdprAccepted = true;

			SystemModel.BankModel = LoadBinAsset<BankModel>("bank_bin");
			SystemModel.EventsModel = LoadBinAsset<LocalEventsModel>("local_events_bin");
			SystemModel.HubEventsModel = LoadBinAsset<HubEventsModel>("hub_events_bin");
			SystemModel.ServerEventsModel = LoadBinAsset<ServerEventsModel>("server_events_bin");
			SystemModel.ServerEventsAdModel = LoadBinAsset<ServerEventsAdModel>("server_events_ad_bin");
			SystemModel.MiniGamesModel = LoadBinAsset<MiniGamesModel>("mini_games_bin");
			SystemModel.Quests = LoadBinAsset<Quests>("quests_bin");
			SystemModel.OrganizerModel = LoadBinAsset<OrganizerModel>("organizer_bin");
			SystemModel.WorldModel = LoadBinAsset<WorldModel>("world_bin");
			SystemModel.ProfileModel = LoadBinAsset<ProfileModel>("profile_bin");
			SystemModel.ExpansionsModel = LoadBinAsset<ExpansionsModel>("expansions_bin");
			SystemModel.NotificationModel = LoadBinAsset<NotificationModel>("notifications_bin");
			SystemModel.ScreenplaysModel = LoadBinAsset<ScreenplaysModel>("screenplays_bin");
			SystemModel.LivesModel = LoadBinAsset<LivesModel>("lives_config_bin");
			SystemModel.CollectiblesModel = LoadBinAsset<CollectiblesModel>("collectibles_bin");
			SystemModel.LevelDifficultModel = LoadBinAsset<LevelDifficultModel>("level_difficult_bin");
			SystemModel.Balance = LoadBinAsset<BalanceModel>("balance_bin");
			SystemModel.HardUpdateCondition = LoadBinAsset<HardUpdateCondition>("hard_update_condition_bin");
			SystemModel.LuckyBuyModel = LoadBinAsset<LuckyBuyModel>("lucky_buy_bin");
			SystemModel.PlayerFormModel = LoadBinAsset<PlayerFormModel>("player_form_bin");
			SystemModel.ReturnRewardsModel = LoadBinAsset<ReturnRewardsModel>("return_rewards_bin");
			SystemModel.LocalEventsAnnounceModel = LoadBinAsset<LocalEventsAnnounceModel>("local_events_announce_bin");
			SystemModel.BubblesNotificationModel = LoadBinAsset<BubblesNotificationModel>("bubbles_notification_bin");

			
#if UNITY_WEBGL && !UNITY_EDITOR
			while (!IsGameOpened())
			{
				await ATask.Yield();
			}
#endif
			
			LoadState();
			await SetCurrentLanguage();
			SoundManager.Init(_audioMixer, UserData.isActiveMusic, UserData.isActiveSFX);
			
			CreateExternal();
			
			ServerController.Init(UserData);
			ServerController.TrySyncTime();
/*#if UNITY_ANDROID
			NotificationController = new NotificationController(_notificationsManager);
#endif*/
			StateConflictController = new StateConflictController();
			
#if UNITY_WEBGL && !UNITY_EDITOR
			ExternalController.FacebookController.FBInit += AfterFbInit;
			ExternalController.FacebookController.TryInit();
#else
			if (_startScene != Scene.Match3)
			{
				Auth();
			}
			else
			{
				AfterGDPR();
			}
#endif
		}

		private async void Auth()
		{
			if (UserData.userProfile.ServicesData.IsSynced())
			{
				await StateConflictController.SendState(UserData,start:true);
				var success = await Sync();
				if (success)
				{
					await StateConflictController.SendState(UserData,false);	
					AfterGDPR();
				}
				else
				{
					NoInternet(Auth);
				}
			}
			else
			{
				Silent = false;
				AfterGDPR();
			}
		}

		private async void AfterFbInit()
		{
			ExternalController.FacebookController.FBInit -= AfterFbInit;

			await StateConflictController.SendState(UserData);	
			var success = await Sync();
			if (success)
			{
				await StateConflictController.SendState(UserData,true);	
				AfterGDPR();
			}
		}

		private async void AfterGDPR()
		{
			Silent = false;

			ABTestController = new ABTestController();
			
			InternetController = new InternetController();
			SegmentationController = new SegmentationController();
			FileNameFinderController = new FileNameFinder(ABTestController, SegmentationController);
			
			//это находится тут, потому что дальнейшей логике требуется SystemModel.Match3Levels.Quantity
			SystemModel.Match3Levels = LoadBinAsset<Match3Levels>($"match3{FileNameFinderController.GetConfigPostfix(CONFIG.match3)}_bin");
#if UNITY_EDITOR
			SystemModel.Match3Levels.Quantity = RuntimeConfigUtils.GetMaxMatch3Level();	
#endif
			
			if (_startScene != Scene.Match3)
			{
				await HardUpdateController.Check();
			}
			
			try {
				StatisticController = new StatisticController();
				StatisticController.Init();
			} catch ( Exception e ) {
				Debug.LogError( "stata naebnulas'! Exception ::: " + e.Message );
			}

			if (UserData.raw) FillState();
			List<IExpansionsLoader> loaders = SystemController.ExpansionController.GetAllExpansionLoaders();
			foreach (var loader in loaders)
			{
				loader.Init(_startScene == Scene.Match3);
			}
			
			if (UserData.stateNeedValidate) StateValidController.continueValidate( UserData, SystemModel );
			await UserData.Update();
			StatisticController.SendInstallOrEnter();

			await CheckLanguages();

		}

		private async Task CheckPacks() {
			if (_startScene != Scene.Match3) {
				await CheckExpansionsPacks();
			}
			
			await ExpansionsLoaded();
		}

		private async Task CheckLanguages() {
			LocaleController.CheckState();

			StatisticController.SendLoadingEventByMode(StatisticEventLoadingMode.AfterCheckState);
			if (StatisticController.IsInstall) {
				await CheckPacks();
				StatisticController.SendLoadingEventByMode(StatisticEventLoadingMode.AfterCheckPacks);
			}
			else if (VersionOrigin.GetReleaseVersion() == UserData.languagesData.LocaleVersion) {
				StatisticController.SendLoadingEventByMode(StatisticEventLoadingMode.IsCurrentLocaleVersion);
				await LocaleController.UpdateLocale();
				await CheckPacks();
			} else {
				if (await ServerController.CheckInternetAsync()) {
					await LocaleController.AfterGameUpdate();
					await CheckPacks();
				} else {
					NoInternetTask(CheckLanguages);
				}
			}
		}

		private async Task CheckExpansionsPacks()
		{
			List<IExpansionsLoader> loaders = SystemController.ExpansionController.GetAllExpansionLoaders();

			List<ResourceGroup> expansionsToUpdate = loaders.SelectMany(x => x.GetPackToUpdate()).Where(x => ExpansionController.IsPackExist(x.ToString())).Distinct().ToList();
			bool packsLoaded = ExpansionController.IsPacksDownloaded(expansionsToUpdate);

			if (packsLoaded)
			{
				if (expansionsToUpdate.Count == 0)
				{
					Handler_Progress(0.8f);
				}
				else
				{

					List<Task> loadTasks = new List<Task>();
					foreach (var loader in loaders)
					{
						loadTasks.Add(loader.AfterPacksPreloaded());
					}
					while (!loadTasks.All(x => x.IsCompleted))
					{
						await ATask.Yield();
						Handler_Progress((loaders.Sum(x => x.LoadToMemoryProgress) / loaders.Count) * 0.4f + 0.4f);	
					}	
				}
				return;
			}
			
			var status = ExpansionController.DownloadsPacks(expansionsToUpdate);
			LoaderController.ShowPacksLoading();
			
			var window = Resources.Load<NoInternetWindowController>(Window.NoInternet.ToString());
			NoInternetWindowController noInternetWindowController = null;

			while (true)
			{
				if (!ServerController.CheckInternet() && noInternetWindowController == null)
				{
					LoaderController.SetTop();	
					 noInternetWindowController = Object.Instantiate(window, _loaderCanvas.transform);
					 noInternetWindowController.Show(new NoInternetWindowParameter(enableWinBackground:true, action: () => {
						 if (noInternetWindowController != null) {
							 Object.Destroy(noInternetWindowController.gameObject);
							 noInternetWindowController = null;
						 }
					 }));
				}

				if (!status.IsPacksLoaded)
				{
					await ATask.Yield();
				}
				else
				{
					break;
				}
			}
			
			await ATask.WaitUntil(() => noInternetWindowController == null);
			foreach (var loader in loaders)
			{
				await loader.AfterPacksPreloaded();
			}
		    LoaderController.SetNotTop();	
			LoaderController.HidePacksLoading();
		}
		
		private async Task ExpansionsLoaded()
		{
			if (_startScene != Scene.Match3)
			{
				Segmentation();
				
				SystemModel.WorldModel = LoadBinAsset<WorldModel>($"world{FileNameFinderController.GetConfigPostfix(CONFIG.world)}_bin");
				SystemModel.ActiveMechanicsModel = LoadBinAsset<ActiveMechanicsModel>($"active_mechanics{FileNameFinderController.GetConfigPostfix(CONFIG.active_mechanics)}_bin");
				SystemModel.BankModel = LoadBinAsset<BankModel>($"bank{FileNameFinderController.GetConfigPostfix(CONFIG.bank)}_bin");
				SystemModel.NotificationModel = LoadBinAsset<NotificationModel>($"notifications{FileNameFinderController.GetConfigPostfix(CONFIG.notifications)}_bin");
				SystemModel.BubblesModel = LoadBinAsset<BubblesModel>("bubbles_bin");
				SystemModel.DialogsModel = LoadBinAsset<DialogsModel>("dialogs_bin");
				SystemModel.LevelCapModel = LoadBinAsset<LevelCapModel>($"levelCap{FileNameFinderController.GetConfigPostfix(CONFIG.levelCap)}_bin");
				SystemModel.ClansModel = LoadBinAsset<ClansModel>("clans_bin");
				SystemModel.DailyQuestsModel = LoadBinAsset<DailyQuestsModel>($"daily_quests{FileNameFinderController.GetConfigPostfix(CONFIG.daily_quests)}_bin");
				SystemModel.CollectiblesModel = LoadBinAsset<CollectiblesModel>($"collectibles{FileNameFinderController.GetConfigPostfix(CONFIG.collectibles)}_bin");
				SystemModel.DailyLevelsModel = LoadBinAsset<DailyLevelsModel>($"daily_levels{FileNameFinderController.GetConfigPostfix(CONFIG.daily_levels)}_bin");
				SystemModel.PlayerFormModel = LoadBinAsset<PlayerFormModel>($"player_form{FileNameFinderController.GetConfigPostfix(CONFIG.player_form)}_bin");
				SystemModel.ServerEventsOptions = LoadBinAsset<ServerEventsOptions>($"server_events_options{FileNameFinderController.GetConfigPostfix(CONFIG.server_events_options)}_bin");
			}
			
			SystemModel.Balance = LoadBinAsset<BalanceModel>($"balance{FileNameFinderController.GetConfigPostfix(CONFIG.balance)}_bin");
			SystemModel.LevelDifficultModel = LoadBinAsset<LevelDifficultModel>($"level_difficult{FileNameFinderController.GetConfigPostfix(CONFIG.level_difficult)}_bin");
			SystemModel.Boosters = LoadBinAsset<BoostersModel>($"boosters{FileNameFinderController.GetConfigPostfix(CONFIG.boosters)}_bin");
			SystemModel.Tutorials = LoadBinAsset<TutorialsModel>($"tutorials{FileNameFinderController.GetConfigPostfix(CONFIG.tutorials)}_bin");	
			SystemModel.LuckyBuyModel = LoadBinAsset<LuckyBuyModel>($"lucky_buy{FileNameFinderController.GetConfigPostfix(CONFIG.lucky_buy)}_bin");


			StatisticController.SendLoadingEventByMode( StatisticEventLoadingMode.ResourcesComplete );

			Advertising();
			await UpdateEventsAndStateData();
			SunsetSecretSoundController.Init(this);

			CollectiblesRewardsFilterUtils.FilterRewards();

			CreateControllers();
			StartSystem();
		}

		private void Segmentation() {
			SystemModel.SegmentationModel = LoadBinAsset<SegmentationModel>($"segmentation{ABTestController.GetConfigPostfix(CONFIG.segmentation)}_bin");
			SegmentationController.Init();
		}

		private void Advertising() {
			SystemModel.ADConfig = LoadBinAsset<ADConfig>($"advertising{FileNameFinderController.GetConfigPostfix(CONFIG.advertising)}_bin");
		}

		private async Task UpdateEventsAndStateData() {
			UpdateEventsModel();
			await UserData.Update();
		}

		private void UpdateEventsModel() {
			UpdateLocalEventsModel();
			UpdateHubEventsModel();
			SegmentationController.UpdateServerEventsModel();
		}

		public static void UpdateLocalEventsModel(LocalEventType eventType = LocalEventType.None, string id = "") {
			List<string> tempIds = new List<string>();
			foreach (BaseLocalEventModel oldModel in SystemController.SystemModel.EventsModel.Events) {
				if (eventType != LocalEventType.None && oldModel.Type != eventType) continue;
				if (Enum.TryParse(oldModel.Type.ToString(), out CONFIG config)) {
					string postfix = SystemController.FileNameFinderController.GetConfigPostfix(config);
					if (postfix != "") {
						string newModelName = $"events_{oldModel.Id}{postfix}.json";
						string newModel = SystemController.ResourceManager.GetDataFile(newModelName);
						Debug.Log($"Update event {newModelName}! File is exist = {!string.IsNullOrEmpty(newModel)}");
						if (!string.IsNullOrEmpty(newModel)) {
							JsonConvert.PopulateObject(newModel, oldModel);
						}
					} else if (eventType != LocalEventType.None) {
						LocalEventsModel baseModel = LoadBinAsset<LocalEventsModel>("local_events_bin");
						foreach (BaseLocalEventModel model in baseModel.Events) {
							if (model.Type == eventType) {
								if (!string.IsNullOrEmpty(id)) {
									if (model.Id == id) {
										JsonConvert.PopulateObject(JsonConvert.SerializeObject(model), oldModel);
										break;
									}
								} else if(model.Id == oldModel.Id){
									JsonConvert.PopulateObject(JsonConvert.SerializeObject(model), oldModel);
									break;
								}
							}
						}
					}
				}

				if (tempIds.Contains(oldModel.Id)) {
					Debug.LogError("Ahtung! Local events model already contains Id = " + oldModel.Id);
				} else {
					tempIds.Add(oldModel.Id);
				}
			}
		}

		private void UpdateHubEventsModel(HubEventType eventType = HubEventType.None) {
			List<string> tempIds = new List<string>();
			foreach (BaseHubEventModel oldModel in SystemController.SystemModel.HubEventsModel.Events)
			{
				if(eventType != HubEventType.None && oldModel.Type != eventType) continue;
				if (Enum.TryParse(oldModel.Type.ToString(), out CONFIG config)) {
					string postfix = SystemController.FileNameFinderController.GetConfigPostfix(config);
					if (postfix != "")
					{
						string newModelName = $"events_{oldModel.Id}{postfix}.json";
						string newModel = SystemController.ResourceManager.GetDataFile(newModelName);
						Debug.Log($"Update event {newModelName}! File is exist = {!string.IsNullOrEmpty(newModel)}");
						if (!string.IsNullOrEmpty(newModel))
						{
							JsonConvert.PopulateObject(newModel, oldModel);
						}
					}
				}

				if (tempIds.Contains(oldModel.Id)) {
					Debug.LogError("Ahtung! Local events model already contains Id = " + oldModel.Id);
				} else {
					foreach (HubEventStage stage in oldModel.Stages) {
						if (tempIds.Contains(stage.Id)) {
							Debug.LogError("Ahtung! Local events model already contains Id = " + stage.Id);
						} else {
							tempIds.Add(stage.Id);
						}
					}
					tempIds.Add(oldModel.Id);
				}
			}
		}
		

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        public static extern bool IsGameOpened();
#endif
		
		private void CreateControllers()
		{
			StatisticController.SendCapabilities();
			InitExternal();
			
			StatisticController.SendLoadingEventByMode( StatisticEventLoadingMode.CreateExternalComplete );

			CameraManager = new CameraManager( _worldCamera );

			BankController ??= new BankController();
			
			GameController = new GameController(_rewardDatabase);
			GameController.Init();
			
			StatisticController.SendLoadingEventByMode( StatisticEventLoadingMode.BankControllerComplete );
			
			// GameController.ProfileController.CheckName();

			ParticleSystemController = new ParticleSystemController();
			StatisticController.SendLoadingEventByMode( StatisticEventLoadingMode.ParticleSystemComplete );

			GameController.UserController.LifeController.Check();
			_ = GameController.UserController.SaveUserData(false);
		}

		private async Task<bool> Sync()
		{
			if (UserData.userProfile.ServicesData.IsSynced() )
			{
				foreach (ServiceData serviceData in UserData.userProfile.ServicesData.ServicesDictionary.Values)
				{
					await StateConflictController.Auth(UserData,true, authId: serviceData.ServiceId);	
					await StateConflictController.Auth(UserData,force: false, authId: serviceData.ServiceId);

					if ( VersionChanged() ) { // версия стейта меньше версии игры
						UserData newState = StateValidController.validateState( UserData, ResourceManager.GetDataFile( "state.json" ));
						UserData = newState;
					}
				}

				Silent = false;
				return true;
			}

			NoInternet(null);
			return false;
		}

		private async void StartSystem() {
			if ( _started ) return;
			_started = true;
			Debug.Log("action");

			ActionSystemController.Init();

			SoundManager.LoadSources(ResourceManager);
			ParticleSystemController.Init();
			BankController.Init();
			
			Debug.Log("save");

			_ = GameController.UserController.SaveUserData(false);

			if (VersionOrigin.IsPlayTestVersion() && StartLevelController.testLevelURL is null) {
				if (!int.TryParse(PlayerPrefs.GetString("playTestLevel"), out int lvl)) lvl = 1;
				UserData.MaxCompletedMatch3Level = lvl;
				StartLevelController.testLevelURL = $"{lvl}.json";
			}
			if (VersionUtility.IsMetaOffVersion()) {
				await GameController.StartLevelController.LoadNextLevel();

				// SystemController.UserData.LevelData?.ActivateStartBoosters( new[] { StartBoosterModel.TYPE.BOOSTER_1, StartBoosterModel.TYPE.BOOSTER_2, StartBoosterModel.TYPE.BOOSTER_3 } );
				string[] arguments = Environment.GetCommandLineArgs();
				for ( int i = 0; i < arguments.Length; i++ ) {
					if ( i + 2 < arguments.Length && arguments[i + 2].IndexOf( "-boosters", StringComparison.Ordinal ) == 0 ) {
						string activatedBoosters = arguments[i + 2];
						TYPE[] activated = new TYPE[0];
						
						const string delimiter1 = "sb_1";
						const string delimiter2 = "sb_2";
						const string delimiter3 = "sb_3";
						if (activatedBoosters.IndexOf(delimiter1, StringComparison.Ordinal) >= 0)
						{
							int first = activatedBoosters.IndexOf(delimiter1, StringComparison.Ordinal);
							int second = activatedBoosters.LastIndexOf(delimiter1, StringComparison.Ordinal);
							int startAt = first + delimiter1.Length;
							int len = second - first - delimiter1.Length;
							string result = activatedBoosters.Substring(startAt, len);
							if (int.TryParse(result, out int value1))
							{
								int count = activated.Length + value1;
								for (int j = activated.Length; j < count; j++)
								{
									Array.Resize(ref activated, j + 1);
									activated[j] = TYPE.BOOSTER_1;
								} 
							}
						}
						if (activatedBoosters.IndexOf(delimiter2, StringComparison.Ordinal) >= 0)
						{
							int first = activatedBoosters.IndexOf(delimiter2, StringComparison.Ordinal);
							int second = activatedBoosters.LastIndexOf(delimiter2, StringComparison.Ordinal);
							int startAt = first + delimiter2.Length;
							int len = second - first - delimiter2.Length;
							string result = activatedBoosters.Substring(startAt, len);
							if (int.TryParse(result, out int value))
							{
								int count = activated.Length + value;
								for (int j = activated.Length; j < count; j++)
								{
									Array.Resize(ref activated, j + 1);
									activated[j] = TYPE.BOOSTER_2;
								} 
							}
						}
						if (activatedBoosters.IndexOf(delimiter3, StringComparison.Ordinal) >= 0)
						{
							int first = activatedBoosters.IndexOf(delimiter3, StringComparison.Ordinal);
							int second = activatedBoosters.LastIndexOf(delimiter3, StringComparison.Ordinal);
							int startAt = first + delimiter3.Length;
							int len = second - first - delimiter3.Length;
							string result = activatedBoosters.Substring(startAt, len);
							if (int.TryParse(result, out int value))
							{
								int count = activated.Length + value;
								for (int j = activated.Length; j < count; j++)
								{
									Array.Resize(ref activated, j + 1);
									activated[j] = TYPE.BOOSTER_3;
								} 
							}
						}

						SystemController.UserData.LevelData?.ActivateStartBoosters(LevelData.TestBoostersSourceType, activated);
					}
				}

				UserController.DEPRECATE_SAVE_USER = true;
				foreach (GameFieldUserBoosterData booster in UserData.boosters) booster.value = 100500;
			}

			GameController.UserController.Init();

			if (SystemController.GameController.LevelCapController.StartConditionsCompleted() ||
			    !SystemController.GameController.LevelCapController.CheckLevelCapEnd())
			{
				var loading = GameController.LevelCapController.LoadGuiResources();
				await SystemController.GameController.LevelCapController.TryStartLevelCap();
				await loading;
				if (SystemController.UserData.levelCapData.previewShown &&
					!SystemController.GameController.LevelCapController.CheckLevelCapEnd())
				{
					_startScene = Scene.LevelCap;
					SystemController.LoaderController.ChangeInternalScene(_startScene);
				}
				else
				{
					SystemController.GameController.LevelCapController.OpenPreviewWindow();
				}
			}

			Handler_Progress(0.8f);
			var loader = SystemController.ResourceManager.GetSceneResourceManager(_startScene);
			if (loader != null)
			{
				await loader.Load(ViewManager.CurrentScene);
			}

			await GameController.StartLevelController.LoadNextLevel();
			
			Handler_Progress(1f);
			await ResourceManager.LoadGroups(_groupsNeedToLoadWithoutAwait, ResourceSource.Constant, loadWithoutLoadsQueue:true).LoadingTask;
			ViewManager.Show( _startScene, true );

			CoreApplication.SendComplete();
			StatisticController.SendLoadingEventByMode( StatisticEventLoadingMode.ShowWorld );
		}

#if UNITY_EDITOR
		private static bool _useBinaries;

		// In addressables build, there's not json configs. This method checks what config type need to be used json or binary
		private void SetConfigsFormat() {
			SystemModel systemModel = ResourceManager.GetModelByAssetName<SystemModel>("main_config", ResourceGroup.configs);
			if (systemModel == null) {
				_useBinaries = true;
			}
		}
#endif

		private static T LoadBinAsset<T>(string path) {
			string correctPath = path;
#if UNITY_EDITOR
			if (!_useBinaries) {
				correctPath = path.Replace("_bin", "");
			}
#endif
			
			string jsonPath = path.Replace("_bin", "");
			// Load json configs, only if they exist in soft update
			if (SystemController.SoftUpdateController.Has($"{jsonPath}.json")) {
				correctPath = jsonPath;
			}

			bool needAppendBin = correctPath.Contains("_bin");
			
			T res = SystemController.ResourceManager.GetModelByAssetName<T>(correctPath, ResourceGroup.configs);
			if (res == null)
			{
				while (correctPath.Contains("_"))
				{
					var lastPart = correctPath.Split('_').LastOrDefault();
					correctPath = correctPath.Replace("_" + lastPart, "");
					res = SystemController.ResourceManager.GetModelByAssetName<T>(correctPath + (needAppendBin ? "_bin" : ""), ResourceGroup.configs);
					if(res != null) break;
				}
			}

			StatisticController.SendActionsEvent(StatisticEventActionsName.LoadResources, StatisticEventActionsAction.Load, path, correctPath); 
			return res;
		}

		private void SetLanguageFromPrefs() {
			if (!PlayerPrefs.HasKey(UserData.LanguagePrefName)) return;
			string lang = PlayerPrefs.GetString(UserData.LanguagePrefName);
			if (LocaleController.HasLanguage(lang)) {
				LocaleController.CurrentLanguage = lang;
			}
		}

		private async Task SetCurrentLanguage() {
			string lang = Application.systemLanguage.ToString();
			Debug.Log("System language: " + lang);
			
			if (!string.IsNullOrEmpty(UserData.CurrentLanguage)) {
				lang = UserData.CurrentLanguage;
			}

			if (LocaleController.HasLanguage(lang)) {
				await SetLanguage(lang);
			} else {
				await SetLanguage("English");
			}
			
			LoaderController.UpdateViewTranslations();
		}

		private async Task SetLanguage(string language) {
			string langCode = LocaleController.GetLanguageCode(language);
			if (await LocaleController.LoadLanguage(langCode)) {
				LocaleController.CurrentLanguage = language;
				UserData.CurrentLanguage = language;
				PlayerPrefs.SetString(UserData.LanguagePrefName, language);
			}
		}

		private void LoadState() {
			string statePath = UserController.StateFilePath;
			bool stateAlreadyExist = File.Exists( statePath );
			
			if ( stateAlreadyExist ) { // стейт есть
				try {
					// TODO JSON
					string state = File.ReadAllText( statePath );
					UserData = JsonConvert.DeserializeObject<UserData>( state );
					UserData.stateNeedValidate = false;
					// byte[] state = File.ReadAllBytes( statePath );
					// UserData = MessagePackSerializer.Deserialize<UserData>( state );
					
					if ( VersionChanged() ) { // версия стейта меньше версии игры
						PlayerPrefs.Save();
						stateAlreadyExist = false;
					}
				} catch ( Exception e ) { // что-то стейту хуево
					Debug.LogError( e );
					UserData = null;
					stateAlreadyExist = false;
				}
			}

			if ( !stateAlreadyExist || UserData.ForceValidate ) { // что-то со стейтом не так, надо что-то решать
				if ( UserData != null || UserData.ForceValidate ) { // стейт устарел, нужна валидация
					if ( UserData == null ) {
						try {
							// TODO JSON
							string state = File.ReadAllText( statePath );
							Debug.Log( "temp state" );
							Debug.Log( state );
							UserData = JsonConvert.DeserializeObject<UserData>( state );
							UserData.stateNeedValidate = false;
							// byte[] state = File.ReadAllBytes( statePath );
							// UserData = MessagePackSerializer.Deserialize<UserData>( state );
						} catch ( Exception e ) { // и бэкапу хуево
							Debug.Log( e.Message );
							stateAlreadyExist = false;
						}						
					} else {
						UserData newState = StateValidController.validateState( UserData, ResourceManager.GetDataFile( "state.json" ) );
						UserData = newState;
						UserData.UpdatedFromOldVersion = true;
						stateAlreadyExist = true;
					}
				} else { // пытаемся загрузить стейт из бэкапа
					statePath = UserController.TempStateFilePath;
					stateAlreadyExist = File.Exists( statePath );

					try {
						// TODO JSON
						string state = File.ReadAllText( statePath );
						Debug.Log( "temp state" );
						Debug.Log( state );
						UserData = JsonConvert.DeserializeObject<UserData>( state );
						// byte[] state = File.ReadAllBytes( statePath );
						// UserData = MessagePackSerializer.Deserialize<UserData>( state );
					} catch ( Exception e ) { // и бэкапу хуево
						Debug.Log( e.Message );
						stateAlreadyExist = false;
					}
				}
			}

			if ( !stateAlreadyExist ) { // стейта все еще нет, создаем стартовый стейт
				
				// TODO JSON
				UserData = JsonConvert.DeserializeObject<UserData>( ResourceManager.GetDataFile( "state.json" ) );
				
				UserData.userProfile.deviceId = StatisticController.GetGuid();
				UserData.statisticData.userId = StatisticController.GetGuid();
				UserData.version = VersionOrigin.GetIntAppVersion();
				UserData.statisticData.installVersion = UserData.version;
				UserData.languagesData.LocaleVersion = VersionOrigin.GetReleaseVersion();
			}
		}
		
		private void FillState()
		{
			string configName = "stateRaw" + SystemController.ABTestController.GetConfigPostfix( CONFIG.stateRaw ) + ".json";
			string configValue = ResourceManager.GetDataFile(configName);
			UserData rawData = JsonConvert.DeserializeObject<UserData>( configValue );

			UserData.money = rawData.money;
			UserData.raw = false;
		}
		
		private void CreateExternal() {
#if ((UNITY_IOS || UNITY_ANDROID || UNITY_WEBGL) && !UNITY_EDITOR)
			if(ExternalController == null){
				ExternalController = new ExternalController(_helpshiftCallbacksReceiver);
			}
#endif
		}

		private void InitExternal()
		{
#if ((UNITY_IOS || UNITY_ANDROID || UNITY_WEBGL) && !UNITY_EDITOR)
			ExternalController?.Init(_mainTransform);
#endif
		}

		private bool VersionChanged() => UserData.version < VersionOrigin.GetIntAppVersion();

		private void Handler_Progress(float value) => LoaderController.SetProgress( value );

		public async Task Restart(UserData data) {
			StatisticController.SendActionsEvent(StatisticEventActionsName.System, StatisticEventActionsAction.Restart); 
			if (ViewManager.CurrentScene == Scene.Match3) SystemController.LoaderController.Dispose();

			SystemController.LoaderController.ChangeInternalScene(Scene.World);
			await SystemController.LoaderController.StartLoading(Scene.NONE);
			await SystemController.ViewManager.HideScene();
			string deviceId = UserData.userProfile.deviceId;
			Dictionary<ServiceType, ServiceData> services = UserData.userProfile.ServicesData.ServicesDictionary;
			UserData = data;

			if ( VersionChanged() ) { // версия стейта меньше версии игры
				UserData newState = StateValidController.validateState( UserData, ResourceManager.GetDataFile( "state.json" ) );
				UserData = newState;
			}
			
			ABTestController.UpdateUserGroup();

			await GameController.UserController.SaveUserData(false);
			
			UserData.userProfile.deviceId = deviceId;
			UserData.userProfile.ServicesData ??= new ServicesData();
			UserData.userProfile.ServicesData.ServicesDictionary ??= new Dictionary<ServiceType, ServiceData>();
			UserData.userProfile.ServicesData.ServicesDictionary = services;

			SystemController.ViewManager.HideAll();
			ViewManager.ResetCurrentScene();
			_startScene = Scene.World;
			
			Dispose();

			if (UserData.stateNeedValidate) StateValidController.continueValidate( UserData, SystemModel );
			await UserData.Update();
			
			await SetCurrentLanguage();
			
			await SystemController.StateConflictController.Auth(UserData,force: true);
			await ServerController.RequestController.SyncRequestController.SendState(UserData,true);
			
			List<IExpansionsLoader> loaders = SystemController.ExpansionController.GetAllExpansionLoaders();
			foreach (IExpansionsLoader loader in loaders) {
				loader.Init(_startScene == Scene.Match3);
			}
			
			await SystemController.GameController.UserController.SaveUserData(false);
			
			
			List<ResourceGroup> expansionsToUpdate = loaders.SelectMany(x => x.GetPackToUpdate()).Distinct().ToList();
			Task task = ExpansionController.DownloadsPacks(expansionsToUpdate).Task;
			LoaderController.ShowPacksLoading();

			if (task != null) await task;

			List<Task> loadTasks = new List<Task>();
				
			foreach (IExpansionsLoader loader in loaders) {
				loadTasks.Add(loader.AfterPacksPreloaded());
			}

			await ATask.WhenAll(loadTasks);
			await ResourceManager.LoadGroups(_groupsNeedToLoadWithoutAwait, ResourceSource.Constant, loadWithoutLoadsQueue:true).LoadingTask;
			await RestartLanguagesCheck();
		}

		private async Task RestartLanguagesCheck() {
			LocaleController.CheckState();

			if (VersionOrigin.GetReleaseVersion() == UserData.languagesData.LocaleVersion) {
				await LocaleController.UpdateLocale();
				
				await AfterRestartPacks();
			} else {
				if (await ServerController.CheckInternetAsync()) {
					await LocaleController.AfterGameUpdate();
					
					await AfterRestartPacks();
				} else {
					NoInternetTask(CheckLanguages);
				}
			}
		}

		private async void NoInternet(Action method) {
			NoInternetWindowController window = Resources.Load<NoInternetWindowController>(Window.NoInternet.ToString());
			NoInternetWindowController noInternet = Object.Instantiate(window, _loaderCanvas.transform);

			async void OnNoInternetIsHide() {
				Object.Destroy(noInternet.gameObject);
				await ATask.Delay(TimeSpan.FromSeconds(5));
				if (method != null) {
					method();
				} else {
					NoInternet(null);
				}
			}
			
			LoaderController.SetNotTop();
			await noInternet.Show(new NoInternetWindowParameter(enableWinBackground:true, action: OnNoInternetIsHide));
		}
		
		private async void NoInternetTask(Func<Task> method) {
			NoInternetWindowController window = Resources.Load<NoInternetWindowController>(Window.NoInternet.ToString());
			NoInternetWindowController noInternet = Object.Instantiate(window, _loaderCanvas.transform);

			async void OnNoInternetIsHide() {
				Object.Destroy(noInternet.gameObject);
				await ATask.Delay(TimeSpan.FromSeconds(5));
				if (method != null) {
					await method();
				} else {
					NoInternetTask(null);
				}
			}

			LoaderController.SetNotTop();
			await noInternet.Show(new NoInternetWindowParameter(enableWinBackground:true, action: OnNoInternetIsHide));
		}

		private async Task AfterRestartPacks() {
			SystemController.LoaderController.HidePacksLoading();

			_started = false;
			
			Segmentation();
			Advertising();
			await UpdateEventsAndStateData();
				
			StatisticController.SendLoadingEventByMode(StatisticEventLoadingMode.ResourcesComplete);
			
			CreateControllers();
			StartSystem();
		}
		
		public void Dispose() {
			ParticleSystemController?.Dispose();
			GameController?.Dispose();
			ActionSystemController.Dispose();
		}
	}
}
