using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Splice.World
{
    public sealed class ForestHeroController : MonoBehaviour
    {
        [Min(0.1f), SerializeField] private float moveSpeed = 7f;
        [SerializeField] private Camera movementCamera;
        [SerializeField] private LayerMask groundMask = ~0;
        private Vector3 clickDestination;
        private bool hasDestination;

        public Camera MovementCamera { get => movementCamera; set => movementCamera = value; }

        private void Update()
        {
            if (movementCamera == null) movementCamera = Camera.main;
            var input = Vector2.zero;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x--;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x++;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y--;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y++;
            }
            if (Gamepad.current != null && input.sqrMagnitude < 0.001f)
                input = Gamepad.current.leftStick.ReadValue();
            if (input.sqrMagnitude > 0.001f)
            {
                hasDestination = false;
                MoveCameraRelative(Vector2.ClampMagnitude(input, 1f));
            }
            else if (hasDestination)
            {
                var delta = clickDestination - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude < 0.04f) hasDestination = false;
                else MoveWorld(delta.normalized);
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
                movementCamera != null &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                var ray = movementCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out var hit, 500f, groundMask,
                        QueryTriggerInteraction.Ignore))
                {
                    clickDestination = hit.point;
                    hasDestination = true;
                }
            }
        }

        public void MoveCameraRelative(Vector2 input)
        {
            var forward = movementCamera != null ? movementCamera.transform.forward : Vector3.forward;
            var right = movementCamera != null ? movementCamera.transform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            MoveWorld((forward.normalized * input.y + right.normalized * input.x).normalized);
        }

        private void MoveWorld(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.001f) return;
            transform.position += direction * (moveSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(direction), Time.deltaTime * 12f);
        }
    }
}
