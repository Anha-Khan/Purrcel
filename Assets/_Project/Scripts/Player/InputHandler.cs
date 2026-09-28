using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatCourier.Player
{
    public sealed class InputHandler : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;

        private InputAction jumpAction;
        private InputAction slideAction;
        private InputAction pauseAction;
        private bool pendingTouchJump;

        public event Action JumpPressed;
        public event Action SlidePressed;
        public event Action PausePressed;

        public void Configure(InputActionAsset actions)
        {
            inputActions = actions;
            BindActions();
        }

        public void SetInputEnabled(bool enabled)
        {
            pendingTouchJump = false;
            if (enabled)
            {
                jumpAction?.Enable();
                slideAction?.Enable();
                pauseAction?.Enable();
            }
            else
            {
                jumpAction?.Disable();
                slideAction?.Disable();
                pauseAction?.Disable();
            }
        }

        public void PressPause() => PausePressed?.Invoke();

        private void Awake()
        {
            BindActions();
            SetInputEnabled(isActiveAndEnabled);
        }

        private void Update()
        {
            if (!pendingTouchJump)
            {
                return;
            }

            pendingTouchJump = false;
            JumpPressed?.Invoke();
        }

        private void OnDestroy()
        {
            Unbind(jumpAction, OnJump);
            Unbind(slideAction, OnSlide);
            Unbind(pauseAction, OnPause);
        }

        private void BindActions()
        {
            Unbind(jumpAction, OnJump);
            Unbind(slideAction, OnSlide);
            Unbind(pauseAction, OnPause);

            if (inputActions == null)
            {
                return;
            }

            var map = inputActions.FindActionMap("Gameplay", true);
            jumpAction = map?.FindAction("Jump", true);
            slideAction = map?.FindAction("Slide", true);
            pauseAction = map?.FindAction("Pause", true);

            if (jumpAction != null)
            {
                jumpAction.performed += OnJump;
            }

            if (slideAction != null)
            {
                slideAction.performed += OnSlide;
            }

            if (pauseAction != null)
            {
                pauseAction.performed += OnPause;
            }
        }

        private static void Unbind(InputAction action, Action<InputAction.CallbackContext> callback)
        {
            if (action != null)
            {
                action.performed -= callback;
            }
        }

        private void OnJump(InputAction.CallbackContext context)
        {
            if (context.control.device is Touchscreen)
            {
                pendingTouchJump = true;
                return;
            }

            JumpPressed?.Invoke();
        }

        private void OnPause(InputAction.CallbackContext context) => PausePressed?.Invoke();

        private void OnSlide(InputAction.CallbackContext context)
        {
            if (context.control.device is Touchscreen)
            {
                if (context.ReadValue<Vector2>().y >= 0f)
                {
                    return;
                }

                pendingTouchJump = false;
                SlidePressed?.Invoke();
                return;
            }

            if (context.ReadValueAsButton())
            {
                SlidePressed?.Invoke();
            }
        }
    }
}
