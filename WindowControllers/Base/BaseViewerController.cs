using System.Threading.Tasks;
using share.controller.core;
using UnityEngine;

namespace share.controller.GUI.core
{
	[DisallowMultipleComponent]
	public abstract class BaseViewerController : MonoBehaviour
	{
		public static ISystemController SystemController { get; set; }

		public abstract Task Hide();

		public virtual Task Show() => Task.CompletedTask;
	}
}