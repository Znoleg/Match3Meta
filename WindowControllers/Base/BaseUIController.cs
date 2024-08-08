using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DG.Tweening;
using GlobalEventAggregator;
using share.controller.game.tutorial;
using share.controller.GUI.core;
using share.manager;
using Sound;
using UnityEngine;
using UnityEngine.UI;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
namespace share.controller.GUI
{
    public interface IWindowClosedCallbackReceiver
    {
        void OnWindowClosed(Window window, bool wasWindowActionSuccessful);
    }
    
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class BaseUIController : BaseViewerController
    {
        public event Action OnShow;
        public event Action OnHide;
        public bool ShowStarted { get; private set; }
        public bool Shown { get; private set; }
        public bool HideInProcess { get; set; }
        public event Action ShowAnimationsComplete;
        public bool AnimationsComplete { get; private set; }
        public bool ShowWithDelay { get; set; }
        public UiPanel ThisPanel { get; set; }
        [SerializeField] protected CanvasGroup _canvasGroup = default;
        public CanvasGroup CanvasGroup => _canvasGroup;
        
        [SerializeField] private bool _needMoveText = default;
        [SerializeField] private bool _unlockClickingAfterAnim = false;

        protected IWindowClosedCallbackReceiver _windowClosedCallbackReceiver;
        protected Window ThisWindow;

        private bool _localLock;

        public bool LocalLock
        {
            get => _localLock;
            set {
                _localLock = value;
                if (ThisWindow != Window.Empty) {
#if !UNITY_EDITOR
                    Debug.Log($"Change local lock, win: {ThisWindow.ToString()}, isLocked: {value}");
#endif
                } else if (ThisPanel != UiPanel.Empty) {
#if !UNITY_EDITOR
                    Debug.Log($"Change local lock, panel: {ThisPanel.ToString()}, isLocked: {value}");
#endif
                }
                UpdateLock();
            }
        }
        
        public bool UnlockClickingAfterAnim 
        {
            get => _unlockClickingAfterAnim;
            set => _unlockClickingAfterAnim = value;
        }

        public void UpdateLock()
        {
            if (_canvasGroup)
            {
                _canvasGroup.interactable = !_localLock;
                EnableScrollRects( !_localLock);
            }
        }

        public virtual bool CustomAnimationDuration => false;

        protected void TriggerShown()
        {
            ShowAnimationsComplete?.Invoke();
        }

        private void EnableScrollRects(bool active)
        {
            if (transform)
            {
                var scrollRects = transform.GetComponentsInChildren<ScrollRect>(true);

                foreach (var scrollRect in scrollRects)
                {
                    scrollRect.enabled = active;
                }  
            }
        }

        protected internal virtual void Reload()
        {
            //for reload opened window/panel
        }

        public virtual SoundManager.AudioClipType GetSoundOnopen()
        {
            return SoundManager.AudioClipType.window_open;
        }
        
        public virtual SoundManager.AudioClipType GetSoundClose()
        {
            return SoundManager.AudioClipType.window_close;
        }

        public virtual async Task Show() {
            LocalLock = true;
            ShowStarted = true;
            
            _windowClosedCallbackReceiver = null;
            
            Sequence showAnimation = DOTween.Sequence();

            if (ShowWithDelay)
            {
                showAnimation.SetDelay(0.3f);
            }
            
            MakeShowAnimation(showAnimation);
            if (!_unlockClickingAfterAnim) 
            {
                const float lockTime = 0.5f;
                showAnimation.InsertCallback(lockTime, () => 
                {
                    LocalLock = false;
                });
            }
            
            await Task.Delay(TimeSpan.FromSeconds(showAnimation.Duration()));
            Shown = true;
            if (_unlockClickingAfterAnim) LocalLock = false;
            if (!HideInProcess)
            {
                OnShow?.Invoke();
            }
        }
        
        public virtual Task Show(object args, IWindowClosedCallbackReceiver windowClosedCallbackReceiver = null, Window window = Window.Empty)
        {
            ThisWindow = window;
            var result = Show();
            _windowClosedCallbackReceiver = windowClosedCallbackReceiver;
            return result;
        }
        
        protected void TriggerCustomEvent(string eventName)
        {
			EventAggregator.Invoke(new TutorialController.TutorialCustomEvent() {EventName =  eventName});
			EventAggregator.Invoke(new TutorialController.TutorialCustomStartEvent() {EventName =  eventName});
        }

        public override async Task Hide()
        {
            LocalLock = true;
            HideInProcess = true;
            Sequence hideAnimation = DOTween.Sequence();
            MakeHideAnimation(hideAnimation);
            
            await Task.Delay(TimeSpan.FromSeconds(hideAnimation.Duration()));
            LocalLock = false;
            OnHide?.Invoke();
        }

        protected void Awake()
        {
            PrepareLinks();
        }

        protected virtual void PrepareLinks()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        public virtual void MakeShowAnimation(Sequence sequence)
        {
        }

        public virtual void MakeHideAnimation(Sequence sequence)
        {
        }

        public bool UiActive() {
            return this != null && gameObject != null && gameObject.activeSelf && !HideInProcess;
        }
    }

    public abstract class BaseUIController<T> : BaseUIController
    {
        public override Task Show(object argument, IWindowClosedCallbackReceiver windowClosedCallbackReceiver = null, Window window = Window.Empty)
        {
            Debug.Assert(argument != null);
            BeforeShow((T) argument);
            var result = base.Show(argument, windowClosedCallbackReceiver, window);
            HandleArguments((T) argument);
            return result;
        }

        protected virtual void BeforeShow(T argument)
        {
        }

        protected abstract void HandleArguments(T argument);
    }
}
