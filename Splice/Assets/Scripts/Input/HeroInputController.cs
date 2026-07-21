using Splice.Characters;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Splice.Input
{
    // Owning-client intent only. Supports keyboard/gamepad in Editor and HeroVirtualJoystick on mobile.
    public class HeroInputController : MonoBehaviour
    {
        [SerializeField] private RaidHeroCharacter hero;
        [SerializeField] private Camera movementCamera;
        [SerializeField] private HeroVirtualJoystick joystick;
        [Min(0.03f)] [SerializeField] private float sendInterval = 0.1f;

        private Vector2 lastSent;
        private float nextSendTime;

        private void Update()
        {
            if (hero == null) hero = RaidHeroCharacter.Instance;
            if (hero == null || !hero.IsOwner) return;
            if (movementCamera == null) movementCamera = Camera.main;

            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) Interact();
            if (hero.ControlMode != HeroControlMode.Manual) return;

            var input = ReadMove();
            var world = CameraRelative(input);
            var world2 = new Vector2(world.x, world.z);
            if (Time.unscaledTime < nextSendTime && (world2 - lastSent).sqrMagnitude < 0.01f) return;

            hero.RequestMoveServerRpc(world2);
            lastSent = world2;
            nextSendTime = Time.unscaledTime + sendInterval;
        }

        public void ToggleControlMode()
        {
            if (hero == null) hero = RaidHeroCharacter.Instance;
            if (hero == null || !hero.IsOwner) return;
            var next = hero.ControlMode == HeroControlMode.Auto ? HeroControlMode.Manual : HeroControlMode.Auto;
            hero.RequestSetControlModeServerRpc(next);
        }

        public void SetAuto()
        {
            if (hero == null) hero = RaidHeroCharacter.Instance;
            if (hero != null && hero.IsOwner) hero.RequestSetControlModeServerRpc(HeroControlMode.Auto);
        }

        public void SetManual()
        {
            if (hero == null) hero = RaidHeroCharacter.Instance;
            if (hero != null && hero.IsOwner) hero.RequestSetControlModeServerRpc(HeroControlMode.Manual);
        }

        public void Interact()
        {
            if (hero == null) hero = RaidHeroCharacter.Instance;
            if (hero != null && hero.IsOwner) hero.RequestInteractServerRpc();
        }

        private Vector2 ReadMove()
        {
            if (joystick != null && joystick.Value.sqrMagnitude > 0.001f) return joystick.Value;

            var value = Vector2.zero;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) value.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) value.x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) value.y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) value.y += 1f;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null && value.sqrMagnitude < 0.001f) value = gamepad.leftStick.ReadValue();
            return Vector2.ClampMagnitude(value, 1f);
        }

        private Vector3 CameraRelative(Vector2 input)
        {
            if (movementCamera == null) return new Vector3(input.x, 0f, input.y);

            var right = movementCamera.transform.right;
            right.y = 0f;
            right.Normalize();

            var forward = movementCamera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = movementCamera.transform.up;
                forward.y = 0f;
            }
            forward.Normalize();
            return Vector3.ClampMagnitude(right * input.x + forward * input.y, 1f);
        }
    }
}
