using share.model.core;

namespace share.controller.core {
	public abstract class BaseController {
		public static ISystemController SystemController { get; set; }
		protected SystemModel SystemModel => SystemController.SystemModel;
	}
}