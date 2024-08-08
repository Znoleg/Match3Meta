using System;
using System.Threading.Tasks;
using Controllers.World;
using share.controller.ab_test;
using share.controller.bank;
using share.controller.external;
using share.controller.game;
using share.controller.GUI;
using share.controller.hard_update;
using share.controller.internet;
using share.controller.locale;
using share.controller.segmentation;
using share.controller.server;
using share.controller.statistic;
using share.controller.user;
using share.manager;
using share.model.core;
using share.model.user;

namespace share.controller.core {
	public interface ISystemController : IDisposable {
		SystemModel SystemModel { get; }
		UserData UserData { get; set; }
		
		GameController GameController { get; }
		ServerController ServerController { get; }
		StatisticController StatisticController { get; }
		ExternalController ExternalController { get; }
		BankController BankController { get; }
		ViewManager ViewManager { get; }
		IResourceManager ResourceManager { get; }
		CameraManager CameraManager { get; }
		ParticleSystemController ParticleSystemController { get; }
		LoaderController LoaderController { get; }
		ABTestController ABTestController { get; }
		SoftUpdateController SoftUpdateController { get; }
		HardUpdateController HardUpdateController { get; }
		IExpansionController ExpansionController { get; }
		InternetController InternetController { get; }
		SegmentationController SegmentationController { get; }
		StateConflictController StateConflictController { get; }
		FileNameFinder FileNameFinderController { get; }
		LocaleController LocaleController { get; }
		bool Silent { get; set; }

		/*NotificationController NotificationController { get; }*/
		
		Task Restart(UserData data);
	}
}